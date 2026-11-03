using GameYunit.Core.Pooling;
using UnityEngine;
using UnityEngine.AI;

namespace GameYunit.Enemies
{
    /// <summary>
    /// Desvio de obstáculos estáticos via NavMesh: calcula o caminho até o alvo e devolve o próximo canto.
    /// Usa apenas consultas (NavMesh.CalculatePath), sem NavMeshAgent: o CharacterController do EnemyAI
    /// continua movendo, com a mesma gravidade e colisão, e o NetworkTransform segue replicando o resultado.
    /// </summary>
    /// <remarks>
    /// Executa só no servidor, pois só o servidor chama EnemyAI.FixedUpdate. Sem NavMesh na cena,
    /// ou fora dele, devolve o próprio destino (linha reta), então o prefab funciona em qualquer mapa.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NavMeshSteering : MonoBehaviour, IEnemySteering, IPoolable
    {
        [Tooltip("Intervalo entre recálculos do caminho. Cada inimigo começa com um deslocamento aleatório para espalhar o custo.")]
        [SerializeField, Min(0.05f)] private float _repathInterval = 0.5f;

        [Tooltip("Distância horizontal para considerar um canto alcançado.")]
        [SerializeField, Min(0.05f)] private float _cornerReachedDistance = 0.6f;

        [Tooltip("Distância máxima para projetar inimigo e alvo sobre o NavMesh.")]
        [SerializeField, Min(0.1f)] private float _sampleDistance = 2f;

        [Tooltip("Áreas do NavMesh permitidas (bitmask). -1 = todas.")]
        [SerializeField] private int _areaMask = NavMesh.AllAreas;

        private readonly Vector3[] _corners = new Vector3[32];
        private NavMeshPath _path;
        private int _cornerCount;
        private int _cornerIndex;
        private float _nextRepathTime;
        private bool _hasPath;

        /// <summary>True se o último cálculo encontrou um caminho (completo ou parcial).</summary>
        public bool HasPath => _hasPath;

        private void Awake() => ResetPath();

        public Vector3 GetSteeringPoint(Vector3 position, Vector3 destination)
        {
            if (Time.time >= _nextRepathTime)
            {
                Repath(position, destination);
            }

            if (!_hasPath)
            {
                return destination;
            }

            float reachedSqr = _cornerReachedDistance * _cornerReachedDistance;
            while (_cornerIndex < _cornerCount - 1 && PlanarDistanceSqr(_corners[_cornerIndex], position) <= reachedSqr)
            {
                _cornerIndex++;
            }

            // Último trecho em linha reta: persegue a posição atual do alvo, não a do cálculo.
            return _cornerIndex >= _cornerCount - 1 ? destination : _corners[_cornerIndex];
        }

        private void Repath(Vector3 position, Vector3 destination)
        {
            // Jitter de ±25%: inimigos nascidos juntos não recalculam sempre no mesmo frame.
            _nextRepathTime = Time.time + _repathInterval * Random.Range(0.75f, 1.25f);
            _hasPath = false;
            _path ??= new NavMeshPath();

            if (!NavMesh.SamplePosition(position, out NavMeshHit from, _sampleDistance, _areaMask) ||
                !NavMesh.SamplePosition(destination, out NavMeshHit to, _sampleDistance, _areaMask) ||
                !NavMesh.CalculatePath(from.position, to.position, _areaMask, _path) ||
                _path.status == NavMeshPathStatus.PathInvalid)
            {
                return;
            }

            // Caminho parcial (alvo fora do alcance) ainda leva o inimigo o mais perto possível.
            _cornerCount = _path.GetCornersNonAlloc(_corners);
            _cornerIndex = 1;
            _hasPath = _cornerCount >= 2;
        }

        private static float PlanarDistanceSqr(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }

        private void ResetPath()
        {
            _hasPath = false;
            _cornerCount = 0;
            _cornerIndex = 0;
            _nextRepathTime = Time.time + Random.Range(0f, _repathInterval);
        }

        public void OnSpawnedFromPool()
        {
            // Primeiro caminho imediato; os seguintes ficam espalhados pelo deslocamento aleatório.
            ResetPath();
            _nextRepathTime = 0f;
        }

        public void OnReturnedToPool() => ResetPath();
    }
}
