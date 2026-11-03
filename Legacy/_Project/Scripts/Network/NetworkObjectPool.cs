using System;
using System.Collections.Generic;
using GameYunit.Core.Pooling;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Network
{
    /// <summary>
    /// Adapta o pool independente de rede ao NGO. Registre os mesmos prefabs em cada peer.
    /// O servidor decide o spawn; os handlers reutilizam os objetos também nos clients.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkObjectPool : MonoBehaviour
    {
        [Serializable]
        private struct Entry
        {
            public NetworkObject Prefab;
            [Min(0)] public int PrewarmCount;
        }

        [SerializeField] private Entry[] _prefabs = Array.Empty<Entry>();

        private readonly Dictionary<NetworkObject, PooledPrefabHandler> _handlers =
            new Dictionary<NetworkObject, PooledPrefabHandler>();
        private NetworkManager _networkManager;
        private IObjectPoolManager _pool;

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _pool = ObjectPoolManager.Instance;
            if (_pool == null)
            {
                Debug.LogError("[NetworkObjectPool] Adicione um ObjectPoolManager antes de iniciar a rede.", this);
                return;
            }

            foreach (Entry entry in _prefabs)
            {
                RegisterPrefab(entry.Prefab, entry.PrewarmCount);
            }
        }

        /// <summary>Registra antes de StartHost/StartClient. A lista de Network Prefabs do NGO também é necessária.</summary>
        public bool RegisterPrefab(NetworkObject prefab, int prewarmCount = 0)
        {
            if (_pool == null || prefab == null)
            {
                return false;
            }

            if (_handlers.ContainsKey(prefab))
            {
                return true;
            }

            if (_networkManager.IsListening)
            {
                Debug.LogError("[NetworkObjectPool] Registre os prefabs antes de iniciar a sessão.", this);
                return false;
            }

            var handler = new PooledPrefabHandler(prefab, _pool);
            if (!_networkManager.PrefabHandler.AddHandler(prefab, handler))
            {
                Debug.LogError($"[NetworkObjectPool] Já existe um handler para '{prefab.name}'.", prefab);
                return false;
            }

            _handlers.Add(prefab, handler);
            _pool.Prewarm(prefab.gameObject, prewarmCount);
            return true;
        }

        public bool IsRegistered(NetworkObject prefab) => prefab != null && _handlers.ContainsKey(prefab);

        /// <summary>Somente o servidor retira uma instância e publica seu spawn para os clients.</summary>
        public NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!_networkManager.IsServer || !_networkManager.IsListening || _networkManager.ShutdownInProgress ||
                !IsRegistered(prefab))
            {
                return null;
            }

            NetworkObject instance = _pool.Get(prefab, position, rotation);
            instance.Spawn(destroyWithScene: true);
            return instance;
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                foreach (NetworkObject prefab in _handlers.Keys)
                {
                    if (prefab != null)
                    {
                        _networkManager.PrefabHandler.RemoveHandler(prefab);
                    }
                }
            }

            _handlers.Clear();
        }

        private sealed class PooledPrefabHandler : INetworkPrefabInstanceHandler
        {
            private readonly NetworkObject _prefab;
            private readonly IObjectPoolManager _pool;

            public PooledPrefabHandler(NetworkObject prefab, IObjectPoolManager pool)
            {
                _prefab = prefab;
                _pool = pool;
            }

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation) =>
                _pool.Get(_prefab, position, rotation);

            public void Destroy(NetworkObject networkObject)
            {
                // O NGO chama depois do despawn, inclusive no Host. Nunca devolver um objeto ainda publicado.
                if (networkObject != null && !networkObject.IsSpawned &&
                    networkObject.TryGetComponent(out PooledObject pooled) && !pooled.IsInPool)
                {
                    _pool.Release(networkObject.gameObject);
                }
            }
        }
    }
}
