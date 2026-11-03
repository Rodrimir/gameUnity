using Unity.Netcode;
using UnityEngine;

namespace GameYunit.KitBridge
{
    /// <summary>Primeira onda mínima: uma cópia de cada asset novo por partida.</summary>
    public sealed class EnemyWaveDirector : NetworkBehaviour
    {
        [SerializeField] private GameObject[] _enemyPrefabs;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField, Min(0)] private int _enemiesPerType = 1;
        private bool _spawned;

        public override void OnNetworkSpawn()
        {
            if (IsServer && !_spawned) SpawnInitialWave();
        }

        private void SpawnInitialWave()
        {
            _spawned = true;
            if (_enemyPrefabs == null || _spawnPoints == null || _spawnPoints.Length == 0) return;
            int slot = 0;
            foreach (var prefab in _enemyPrefabs)
            {
                if (prefab == null) continue;
                for (int i = 0; i < _enemiesPerType; i++)
                {
                    Transform point = _spawnPoints[slot++ % _spawnPoints.Length];
                    GameObject enemy = Instantiate(prefab, point.position, point.rotation);
                    enemy.GetComponent<NetworkObject>().Spawn(true);
                }
            }
        }
    }
}
