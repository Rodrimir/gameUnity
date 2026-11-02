using System;
using UnityEngine;

namespace GameYunit.Core.Pooling
{
    /// <summary>
    /// Marcador adicionado automaticamente a toda instância criada por um pool.
    /// Guarda o pool de origem e o cache de <see cref="IPoolable"/> da hierarquia,
    /// para que spawn/release não façam buscas de componentes.
    /// </summary>
    /// <remarks>
    /// Não é necessário adicioná-lo manualmente aos prefabs.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        private IPoolable[] _poolables = Array.Empty<IPoolable>();

        /// <summary>Pool dono desta instância.</summary>
        internal PrefabPool Pool { get; private set; }

        /// <summary>
        /// True enquanto a instância está guardada no pool. Protege contra release duplicado,
        /// inclusive em builds (ao contrário do collectionCheck do UnityEngine.Pool, que é caro).
        /// </summary>
        public bool IsInPool { get; private set; }

        /// <summary>
        /// Devolve esta instância ao pool de origem.
        /// Atalho para objetos que se reciclam sozinhos (ex.: projétil ao fim do tempo de vida).
        /// </summary>
        public void ReturnToPool()
        {
            if (Pool == null)
            {
                Debug.LogWarning($"[PooledObject] '{name}' não possui pool de origem; destruindo.", this);
                Destroy(gameObject);
                return;
            }

            Pool.Release(this);
        }

        internal void Initialize(PrefabPool pool)
        {
            Pool = pool;
            _poolables = GetComponentsInChildren<IPoolable>(includeInactive: true);
            IsInPool = true;
        }

        internal void MarkSpawned() => IsInPool = false;

        internal void MarkInPool() => IsInPool = true;

        internal void NotifySpawned()
        {
            for (int i = 0; i < _poolables.Length; i++)
            {
                _poolables[i].OnSpawnedFromPool();
            }
        }

        internal void NotifyReturned()
        {
            for (int i = 0; i < _poolables.Length; i++)
            {
                _poolables[i].OnReturnedToPool();
            }
        }
    }
}
