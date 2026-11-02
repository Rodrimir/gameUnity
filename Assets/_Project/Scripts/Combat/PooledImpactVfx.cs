using GameYunit.Core.Pooling;
using UnityEngine;

namespace GameYunit.Combat
{
    [DisallowMultipleComponent]
    public sealed class PooledImpactVfx : MonoBehaviour, IPoolable
    {
        private ParticleSystem _particles;
        private float _remaining;
        private IObjectPoolManager _pool;

        public void Configure(ParticleSystem particles) => _particles = particles;

        public void Play(Color color, int count)
        {
            if (_particles == null) _particles = GetComponent<ParticleSystem>();
            _pool ??= ObjectPoolManager.Instance;
            var main = _particles.main;
            main.startColor = color;
            _particles.Emit(count);
            _remaining = .28f;
        }

        private void Update()
        {
            if (_remaining <= 0f) return;
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) _pool?.Release(gameObject);
        }

        public void OnSpawnedFromPool() { _remaining = 0f; _particles?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        public void OnReturnedToPool() { _remaining = 0f; _particles?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    }
}
