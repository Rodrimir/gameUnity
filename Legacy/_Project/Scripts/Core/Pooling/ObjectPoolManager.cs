using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameYunit.Core.Pooling
{
    /// <summary>
    /// Implementação base de <see cref="IObjectPoolManager"/>: mantém um pool por prefab,
    /// sobre o <see cref="UnityEngine.Pool.ObjectPool{T}"/> da Unity.
    /// </summary>
    /// <remarks>
    /// <para>Uso: um GameObject com este componente por cena de jogo. Acesse via <see cref="Instance"/>.</para>
    /// <para>
    /// Instâncias ativas ficam na raiz da cena (sem container pai): milhares de objetos móveis sob
    /// um único pai serializam o update de Transform, e NetworkObjects têm restrições de reparent.
    /// </para>
    /// <para>
    /// Esta classe não conhece o Netcode. Objetos de rede serão integrados por um
    /// INetworkPrefabInstanceHandler (registrado no NetworkManager.PrefabHandler) que delega a este serviço.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)] // Disponível antes dos Awake/Start de quem consome.
    public class ObjectPoolManager : MonoBehaviour, IObjectPoolManager
    {
        [Serializable]
        private struct PrewarmEntry
        {
            public GameObject Prefab;
            [Min(0)] public int Count;
        }

        [Header("Capacidade (por prefab)")]
        [Tooltip("Capacidade inicial da pilha interna de cada pool.")]
        [SerializeField, Min(0)] private int _defaultCapacity = 32;

        [Tooltip("Máximo de instâncias INATIVAS guardadas por prefab. O excedente é destruído ao ser devolvido.")]
        [SerializeField, Min(1)] private int _maxSize = 1024;

        [Header("Pré-aquecimento")]
        [Tooltip("Prefabs aquecidos no Awake, evitando Instantiate durante o gameplay.")]
        [SerializeField] private PrewarmEntry[] _prewarmOnAwake = Array.Empty<PrewarmEntry>();

        private readonly Dictionary<GameObject, PrefabPool> _pools = new Dictionary<GameObject, PrefabPool>();
        private Transform _stagingRoot;

        /// <summary>Instância ativa do serviço (null se não houver nenhuma na cena).</summary>
        public static IObjectPoolManager Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && !ReferenceEquals(Instance, this))
            {
                Debug.LogWarning("[ObjectPoolManager] Já existe um ObjectPoolManager ativo; removendo duplicata.", this);
                Destroy(this);
                return;
            }

            Instance = this;

            // Raiz inativa usada só no Instantiate (ver PrefabPool.CreateInstance).
            var staging = new GameObject("[PoolStaging]");
            staging.SetActive(false);
            _stagingRoot = staging.transform;
            _stagingRoot.SetParent(transform, worldPositionStays: false);

            for (int i = 0; i < _prewarmOnAwake.Length; i++)
            {
                Prewarm(_prewarmOnAwake[i].Prefab, _prewarmOnAwake[i].Count);
            }
        }

        protected virtual void OnDestroy()
        {
            if (!ReferenceEquals(Instance, this))
            {
                return;
            }

            ClearAll();
            Instance = null;
        }

        /// <inheritdoc />
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            if (count > _maxSize)
            {
                Debug.LogWarning($"[ObjectPoolManager] Prewarm de '{prefab.name}' ({count}) excede maxSize ({_maxSize}); limitado.", prefab);
            }

            GetOrCreatePool(prefab).Prewarm(count);
        }

        /// <inheritdoc />
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[ObjectPoolManager] Get chamado com prefab nulo.", this);
                return null;
            }

            return GetOrCreatePool(prefab).Get(position, rotation, parent).gameObject;
        }

        /// <inheritdoc />
        public T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (prefab == null)
            {
                Debug.LogError($"[ObjectPoolManager] Get<{typeof(T).Name}> chamado com prefab nulo.", this);
                return null;
            }

            return Get(prefab.gameObject, position, rotation, parent).GetComponent<T>();
        }

        /// <inheritdoc />
        public void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.TryGetComponent(out PooledObject pooled))
            {
                pooled.ReturnToPool();
                return;
            }

            Debug.LogWarning($"[ObjectPoolManager] '{instance.name}' não veio de um pool; destruindo.", instance);
            Destroy(instance);
        }

        /// <inheritdoc />
        public void ClearPool(GameObject prefab)
        {
            if (prefab == null || !_pools.TryGetValue(prefab, out PrefabPool pool))
            {
                return;
            }

            pool.Dispose();
            _pools.Remove(prefab);
        }

        /// <inheritdoc />
        public void ClearAll()
        {
            foreach (PrefabPool pool in _pools.Values)
            {
                pool.Dispose();
            }

            _pools.Clear();
        }

        private PrefabPool GetOrCreatePool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out PrefabPool pool))
            {
                pool = new PrefabPool(prefab, _stagingRoot, Mathf.Min(_defaultCapacity, _maxSize), _maxSize);
                _pools.Add(prefab, pool);
            }

            return pool;
        }

        // Com "Enter Play Mode Options" (sem domain reload) estáticos sobrevivem entre sessões de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
