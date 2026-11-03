using System.Collections.Generic;
using GameYunit.Core.Pooling;
using GameYunit.Enemies;
using GameYunit.Player;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Combat
{
    /// <summary>
    /// Projétil local (sem NetworkObject), reciclado pelo ObjectPoolManager de cada peer.
    /// No servidor é a instância autoritativa: move, persegue o alvo, detecta colisão por SphereCast
    /// e atravessa inimigos conforme a perfuração. Nos clients é apenas visual: segue a mesma trajetória
    /// e some quando o servidor informa o fim.
    /// </summary>
    /// <remarks>
    /// Colliders do prefab são desligados no Awake; a colisão é feita por consulta de física,
    /// o que evita Rigidbody e callbacks de trigger em centenas de objetos.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        /// <summary>Parâmetros de lançamento. Dano e perfuração só importam no servidor.</summary>
        internal struct LaunchData
        {
            public Vector3 Origin;
            public Vector3 Direction;
            public float Speed;
            public float Lifetime;
            public float HomingTurnRate;
            public NetworkObject Target;
            public float Damage;
            public int PierceCount;
            public float PierceDamageFalloff;
            public WeaponDefinition.ProjectileStyle Style;
        }

        // Compartilhado: consultas de física acontecem só na main thread.
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        // Passo usado ao adiantar o visual pela latência (o teleguiado precisa de curva, não de um salto).
        private const float FastForwardStep = 1f / 30f;

        // Dentro desta distância o teleguiado aponta direto para o alvo, evitando órbitas.
        private const float HomingSnapDistance = 1.5f;

        [Tooltip("Raio da esfera de colisão usada no servidor.")]
        [SerializeField, Min(0.01f)] private float _radius = 0.15f;

        [Tooltip("Camadas atingidas no servidor. Jogadores e outros projéteis são sempre ignorados.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        private readonly List<Collider> _alreadyHit = new List<Collider>(4);
        private AutoAttackSystem _shooter;
        private NetworkObject _target;
        private Vector3 _direction;
        private float _speed;
        private float _turnRate;
        private float _remainingLifetime;
        private float _damage;
        private int _pierceRemaining;
        private float _pierceFalloff;
        private TrailRenderer _trail;
        private LineRenderer _arc;
        private Renderer _renderer;

        public uint ShotId { get; private set; }

        /// <summary>True na instância autoritativa do servidor; false nos visuais dos clients.</summary>
        public bool IsServerSimulated { get; private set; }

        private void Awake()
        {
            // Permite usar uma primitiva (Sphere) como prefab sem que o SphereCast colida consigo mesmo.
            foreach (Collider ownCollider in GetComponentsInChildren<Collider>(includeInactive: true))
            {
                ownCollider.enabled = false;
            }
            _renderer = GetComponent<Renderer>();
            Shader shader = Shader.Find("Sprites/Default");
            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = .16f;
            _trail.startWidth = .11f;
            _trail.endWidth = .015f;
            _trail.material = new Material(shader);
            _arc = gameObject.AddComponent<LineRenderer>();
            _arc.positionCount = 6;
            _arc.useWorldSpace = false;
            _arc.startWidth = .035f;
            _arc.endWidth = .01f;
            _arc.material = new Material(shader);
            _arc.enabled = false;
        }

        internal void Launch(AutoAttackSystem shooter, uint shotId, in LaunchData data, float elapsed, bool serverSimulated)
        {
            _shooter = shooter;
            ShotId = shotId;
            IsServerSimulated = serverSimulated;
            _target = data.Target;
            _direction = data.Direction;
            _speed = data.Speed;
            _turnRate = data.HomingTurnRate;
            _remainingLifetime = data.Lifetime;
            _damage = data.Damage;
            _pierceRemaining = data.PierceCount;
            _pierceFalloff = data.PierceDamageFalloff;
            ApplyVisualStyle(data.Style);
            _alreadyHit.Clear();

            // Visual atrasado: reproduz a trajetória (inclusive curvas) até onde o servidor já está.
            Vector3 position = data.Origin;
            for (float remaining = Mathf.Min(elapsed, _remainingLifetime); remaining > 0f; remaining -= FastForwardStep)
            {
                float step = Mathf.Min(FastForwardStep, remaining);
                Steer(position, step);
                position += _direction * (_speed * step);
                _remainingLifetime -= step;
            }

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(_direction, Vector3.up));
        }

        private void ApplyVisualStyle(WeaponDefinition.ProjectileStyle style)
        {
            Color color = style == WeaponDefinition.ProjectileStyle.ArcaneLightning
                ? new Color(.15f,.85f,1f,1f) : style == WeaponDefinition.ProjectileStyle.Bullet
                    ? new Color(1f,.68f,.2f,1f) : new Color(.78f,.9f,1f,1f);
            transform.localScale = style == WeaponDefinition.ProjectileStyle.Bullet
                ? new Vector3(.12f,.12f,.42f) : new Vector3(.25f,.25f,.25f);
            if (_renderer != null)
            {
                var block = new MaterialPropertyBlock();
                _renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                _renderer.SetPropertyBlock(block);
            }
            _trail.startColor = color;
            _trail.endColor = new Color(color.r,color.g,color.b,0f);
            _arc.enabled = style == WeaponDefinition.ProjectileStyle.ArcaneLightning;
            _arc.startColor = color;
            _arc.endColor = Color.white;
            if (_arc.enabled)
            {
                for (int i = 0; i < _arc.positionCount; i++)
                    _arc.SetPosition(i, new Vector3(UnityEngine.Random.Range(-.12f,.12f), UnityEngine.Random.Range(-.12f,.12f), -.4f + i*.16f));
            }
            _trail.Clear();
        }

        private void Update()
        {
            if (_shooter == null || GameYunit.Gameplay.GameSessionController.IsPaused)
            {
                return;
            }

            float deltaTime = Mathf.Min(Time.deltaTime, _remainingLifetime);
            Vector3 position = transform.position;
            Steer(position, deltaTime);
            float step = _speed * deltaTime;

            // Varredura contínua do trecho percorrido no frame: não atravessa alvos em FPS baixo.
            if (IsServerSimulated && SweepServer(position, step))
            {
                return;
            }

            transform.SetPositionAndRotation(position + _direction * step, Quaternion.LookRotation(_direction, Vector3.up));
            _remainingLifetime -= Time.deltaTime;
            if (_remainingLifetime <= 0f)
            {
                _shooter.HandleProjectileExpired(this);
            }
        }

        /// <summary>Curva em direção ao alvo, no plano XZ. Alvo removido = segue reto.</summary>
        private void Steer(Vector3 position, float deltaTime)
        {
            if (_turnRate <= 0f || _target == null)
            {
                return;
            }

            if (!_target.IsSpawned)
            {
                _target = null;
                return;
            }

            Vector3 toTarget = _target.transform.position - position;
            toTarget.y = 0f;
            float distanceSqr = toTarget.sqrMagnitude;
            if (distanceSqr < 0.0001f)
            {
                return;
            }

            Vector3 desired = toTarget / Mathf.Sqrt(distanceSqr);
            _direction = distanceSqr <= HomingSnapDistance * HomingSnapDistance
                ? desired
                : Vector3.RotateTowards(_direction, desired, _turnRate * Mathf.Deg2Rad * deltaTime, 0f);
        }

        /// <summary>
        /// Processa os contatos do trecho em ordem de distância. Inimigos recebem dano e podem ser
        /// atravessados; cenário sempre encerra o projétil.
        /// </summary>
        /// <returns>True se o projétil terminou (já devolvido ao pool).</returns>
        private bool SweepServer(Vector3 origin, float distance)
        {
            int count = Physics.SphereCastNonAlloc(origin, _radius, _direction, Hits, distance, _hitMask,
                QueryTriggerInteraction.Ignore);
            SortByDistance(count);

            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = Hits[i].collider;
                if (hitCollider.GetComponentInParent<PlayerNetwork>() != null ||
                    hitCollider.GetComponentInParent<Projectile>() != null || _alreadyHit.Contains(hitCollider))
                {
                    continue;
                }

                // Sobreposição no início da varredura: a Unity devolve distance 0 e point zero.
                Vector3 hitPoint = Hits[i].distance <= 0f ? origin : Hits[i].point;
                if (!IsTarget(hitCollider))
                {
                    _shooter.HandleServerProjectileEnded(this, hitPoint);
                    return true;
                }

                _alreadyHit.Add(hitCollider);
                _shooter.HandleServerHit(hitCollider, hitPoint, _damage);
                if (_target != null && hitCollider.GetComponentInParent<NetworkObject>() == _target)
                {
                    // Depois de acertar o alvo, segue reto em vez de voltar para ele.
                    _target = null;
                }

                if (_pierceRemaining <= 0)
                {
                    _shooter.HandleServerProjectileEnded(this, hitPoint);
                    return true;
                }

                _pierceRemaining--;
                _damage *= _pierceFalloff;
            }

            return false;
        }

        /// <summary>Inimigos (e, a partir do Bloco 4, qualquer IDamageable) são alvos; o resto é cenário.</summary>
        private static bool IsTarget(Collider hitCollider) =>
            hitCollider.GetComponentInParent<EnemyAI>() != null || hitCollider.GetComponentInParent<IDamageable>() != null;

        // Insertion sort: o buffer tem no máximo 16 contatos e o NonAlloc não garante ordem.
        private static void SortByDistance(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit current = Hits[i];
                int j = i - 1;
                while (j >= 0 && Hits[j].distance > current.distance)
                {
                    Hits[j + 1] = Hits[j];
                    j--;
                }

                Hits[j + 1] = current;
            }
        }

        public void OnSpawnedFromPool()
        {
        }

        public void OnReturnedToPool()
        {
            _shooter = null;
            _target = null;
            _alreadyHit.Clear();
            ShotId = 0;
            IsServerSimulated = false;
            _remainingLifetime = 0f;
        }
    }
}
