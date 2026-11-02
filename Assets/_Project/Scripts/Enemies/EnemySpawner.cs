using System;
using System.Collections.Generic;
using GameYunit.Network;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Enemies
{
    /// <summary>Fluxo contínuo de inimigos. Nenhum cálculo ou spawn é executado no Client.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class EnemySpawner : NetworkBehaviour
    {
        [SerializeField] private EnemyAI _enemyPrefab;
        [SerializeField] private NetworkObjectPool _networkPool;
        [SerializeField, Min(0.05f)] private float _spawnInterval = 1f;
        [SerializeField, Min(1)] private int _enemiesPerSpawn = 3;
        [SerializeField, Min(1)] private int _maxActiveEnemies = 128;
        [SerializeField, Min(1f)] private float _minSpawnRadius = 12f;
        [SerializeField, Min(1f)] private float _maxSpawnRadius = 18f;
        [SerializeField, Min(1)] private int _positionAttempts = 12;

        private readonly HashSet<EnemyAI> _activeEnemies = new HashSet<EnemyAI>();
        private NetworkObject _prefabObject;
        private float _nextSpawnTime;
        private int _anchorCursor;
        private bool _ready;

        /// <summary>Quantidade local ao servidor; não é uma NetworkVariable.</summary>
        public int ActiveEnemyCount => _activeEnemies.Count;

        public event Action<EnemyAI> OnEnemySpawned;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer)
            {
                return;
            }

            if (_networkPool == null)
            {
                _networkPool = NetworkManager.GetComponent<NetworkObjectPool>();
            }

            _prefabObject = _enemyPrefab != null ? _enemyPrefab.GetComponent<NetworkObject>() : null;
            _ready = _networkPool != null && _networkPool.IsRegistered(_prefabObject);
            if (!_ready)
            {
                Debug.LogError("[EnemySpawner] Atribua um EnemyAI cujo prefab esteja registrado no NetworkObjectPool.", this);
                return;
            }

            _nextSpawnTime = Time.time;
            _anchorCursor = 0;
        }

        public override void OnNetworkDespawn()
        {
            _ready = false;
            foreach (EnemyAI enemy in _activeEnemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                enemy.OnServerDespawned -= HandleEnemyDespawned;
                if (IsServer && !NetworkManager.ShutdownInProgress)
                {
                    enemy.ReturnToPoolServer();
                }
            }

            _activeEnemies.Clear();
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !_ready || Time.time < _nextSpawnTime)
            {
                return;
            }

            // Não acumula rajadas de compensação após um frame lento ou pausa.
            _nextSpawnTime = Time.time + _spawnInterval;
            int count = Mathf.Min(_enemiesPerSpawn, _maxActiveEnemies - _activeEnemies.Count);
            for (int i = 0; i < count; i++)
            {
                if (!TryFindSpawnPosition(out Vector3 position))
                {
                    break;
                }

                NetworkObject spawned = _networkPool.Spawn(_prefabObject, position, Quaternion.identity);
                if (spawned == null)
                {
                    break;
                }

                EnemyAI enemy = spawned.GetComponent<EnemyAI>();
                _activeEnemies.Add(enemy);
                enemy.OnServerDespawned += HandleEnemyDespawned;
                OnEnemySpawned?.Invoke(enemy);
            }
        }

        private bool TryFindSpawnPosition(out Vector3 position)
        {
            position = default;
            var clients = NetworkManager.ConnectedClientsList;
            if (clients.Count == 0)
            {
                return false;
            }

            float minRadius = Mathf.Min(_minSpawnRadius, _maxSpawnRadius);
            float maxRadius = Mathf.Max(_minSpawnRadius, _maxSpawnRadius);
            for (int attempt = 0; attempt < _positionAttempts; attempt++)
            {
                NetworkObject player = clients[_anchorCursor % clients.Count].PlayerObject;
                _anchorCursor = (_anchorCursor + 1) % clients.Count;
                if (player == null || !player.IsSpawned || !player.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float radius = Mathf.Sqrt(UnityEngine.Random.Range(minRadius * minRadius, maxRadius * maxRadius));
                Vector3 candidate = player.transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                bool safe = true;
                for (int i = 0; i < clients.Count; i++)
                {
                    NetworkObject other = clients[i].PlayerObject;
                    if (other != null && other.IsSpawned && other.gameObject.activeInHierarchy &&
                        Vector3.Distance(candidate, other.transform.position) < minRadius)
                    {
                        safe = false;
                        break;
                    }
                }

                if (safe)
                {
                    position = candidate;
                    return true;
                }
            }

            return false;
        }

        private void HandleEnemyDespawned(EnemyAI enemy)
        {
            enemy.OnServerDespawned -= HandleEnemyDespawned;
            _activeEnemies.Remove(enemy);
        }
    }
}
