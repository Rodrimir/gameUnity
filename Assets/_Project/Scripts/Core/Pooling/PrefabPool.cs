using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace GameYunit.Core.Pooling
{
    /// <summary>
    /// Pool de um único prefab. Detalhe interno do <see cref="ObjectPoolManager"/>.
    /// Encapsula <see cref="ObjectPool{T}"/> da Unity e controla a ordem de ativação/desativação.
    /// </summary>
    internal sealed class PrefabPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _stagingRoot;
        private readonly int _maxSize;
        private readonly ObjectPool<PooledObject> _pool;
        private bool _disposed;

        /// <param name="prefab">Prefab de origem.</param>
        /// <param name="stagingRoot">
        /// Transform inativo usado só no Instantiate: como a instância nasce inativa na hierarquia,
        /// Awake/OnEnable só rodam no primeiro Get, já na posição correta.
        /// </param>
        /// <param name="defaultCapacity">Capacidade inicial da pilha interna.</param>
        /// <param name="maxSize">Máximo de instâncias inativas guardadas; o excedente é destruído.</param>
        public PrefabPool(GameObject prefab, Transform stagingRoot, int defaultCapacity, int maxSize)
        {
            _prefab = prefab;
            _stagingRoot = stagingRoot;
            _maxSize = maxSize;

            // Get/Release ficam nulos de propósito: a ativação é feita aqui, na ordem certa
            // (posicionar -> ativar -> notificar), algo que os callbacks do ObjectPool não permitem.
            _pool = new ObjectPool<PooledObject>(
                createFunc: CreateInstance,
                actionOnGet: null,
                actionOnRelease: null,
                actionOnDestroy: DestroyInstance,
                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        public PooledObject Get(Vector3 position, Quaternion rotation, Transform parent)
        {
            PooledObject pooled = _pool.Get();

            // Instâncias destruídas por fora enquanto estavam no pool são descartadas.
            while (pooled == null)
            {
                pooled = _pool.Get();
            }

            Transform t = pooled.transform;
            if (parent != null)
            {
                t.SetParent(parent, worldPositionStays: false);
            }

            // Posiciona ANTES de ativar para que OnEnable/Physics já vejam a posição final.
            t.SetPositionAndRotation(position, rotation);
            pooled.MarkSpawned();
            pooled.gameObject.SetActive(true);
            pooled.NotifySpawned();
            return pooled;
        }

        public void Release(PooledObject pooled)
        {
            if (pooled.IsInPool)
            {
                Debug.LogWarning($"[ObjectPoolManager] '{pooled.name}' já está no pool; release duplicado ignorado.", pooled);
                return;
            }

            pooled.NotifyReturned();
            pooled.gameObject.SetActive(false);
            pooled.MarkInPool();

            if (_disposed)
            {
                Object.Destroy(pooled.gameObject);
                return;
            }

            // Volta para a raiz: evita que a instância morra junto com um parent temporário.
            Transform t = pooled.transform;
            if (t.parent != null)
            {
                t.SetParent(null, worldPositionStays: false);
            }

            // Se o pool já estiver cheio (maxSize), o ObjectPool chama DestroyInstance.
            _pool.Release(pooled);
        }

        public void Prewarm(int count)
        {
            count = Mathf.Min(count, _maxSize);
            if (count <= _pool.CountInactive)
            {
                return;
            }

            // Retira 'count' (reaproveitando as existentes e criando o restante) e devolve tudo.
            // As instâncias nunca são ativadas, então nenhum callback de gameplay é disparado.
            List<PooledObject> buffer = ListPool<PooledObject>.Get();
            for (int i = 0; i < count; i++)
            {
                buffer.Add(_pool.Get());
            }

            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i] != null)
                {
                    _pool.Release(buffer[i]);
                }
            }

            ListPool<PooledObject>.Release(buffer);
        }

        /// <summary>
        /// Destrói as instâncias inativas e marca o pool como descartado.
        /// Instâncias ativas serão destruídas quando forem devolvidas.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            _pool.Clear();
        }

        private PooledObject CreateInstance()
        {
            GameObject instance = Object.Instantiate(_prefab, _stagingRoot);
            instance.SetActive(false);
            instance.transform.SetParent(null, worldPositionStays: false);

            if (!instance.TryGetComponent(out PooledObject pooled))
            {
                pooled = instance.AddComponent<PooledObject>();
            }

            pooled.Initialize(this);
            return pooled;
        }

        private static void DestroyInstance(PooledObject pooled)
        {
            // Checagem de fake-null: a instância pode já ter sido destruída no unload da cena.
            if (pooled != null)
            {
                Object.Destroy(pooled.gameObject);
            }
        }
    }
}
