using GameYunit.Core.Pooling;
using UnityEngine;

namespace GameYunit.Combat
{
    /// <summary>Rajadas curtas locais para boca da arma e impacto, recicladas pelo pool comum.</summary>
    [DisallowMultipleComponent]
    public sealed class WeaponImpactVfx : MonoBehaviour
    {
        private AutoAttackSystem _attack;
        private GameObject _template;
        private PooledImpactVfx _effectPrefab;
        private IObjectPoolManager _pool;

        public void Bind(AutoAttackSystem attack)
        {
            _attack = attack;
            _pool = ObjectPoolManager.Instance;
            if (_pool == null) return;
            CreateTemplate();
            _pool.Prewarm(_template, 6);
            attack.OnVolleyFired += HandleVolley;
            attack.OnProjectileImpact += HandleImpact;
        }

        private void OnDestroy()
        {
            if (_attack != null)
            {
                _attack.OnVolleyFired -= HandleVolley;
                _attack.OnProjectileImpact -= HandleImpact;
            }
            if (_template != null)
            {
                _pool?.ClearPool(_template);
                Destroy(_template);
            }
        }

        private void CreateTemplate()
        {
            _template = new GameObject("PooledWeaponBurstTemplate");
            ParticleSystem particles = _template.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .22f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.1f,.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f,3.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f,.11f);
            main.maxParticles = 24;
            var emission = particles.emission;
            emission.enabled = false;
            var renderer = _template.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null) renderer.sharedMaterial = new Material(shader);
            _effectPrefab = _template.AddComponent<PooledImpactVfx>();
            _effectPrefab.Configure(particles);
            _template.SetActive(false);
        }

        private void HandleVolley(ProjectileVolley volley)
        {
            if (_pool == null || _effectPrefab == null) return;
            WeaponDefinition weapon = _attack.CurrentWeapon;
            Color color = GetColor(weapon != null ? weapon.VisualStyle : WeaponDefinition.ProjectileStyle.Bullet);
            Spawn(volley.Origin, color, 8);
        }

        private void HandleImpact(Vector3 position)
        {
            if (_pool == null || _effectPrefab == null) return;
            WeaponDefinition weapon = _attack.CurrentWeapon;
            Color color = GetColor(weapon != null ? weapon.VisualStyle : WeaponDefinition.ProjectileStyle.Bullet);
            Spawn(position, color, 12);
        }

        private void Spawn(Vector3 position, Color color, int count)
        {
            PooledImpactVfx instance = _pool.Get(_effectPrefab, position, Quaternion.identity);
            if (instance != null) instance.Play(color, count);
        }

        private static Color GetColor(WeaponDefinition.ProjectileStyle style) => style switch
        {
            WeaponDefinition.ProjectileStyle.ArcaneLightning => new Color(.15f,.85f,1f,1f),
            WeaponDefinition.ProjectileStyle.PiercingBolt => new Color(.78f,.9f,1f,1f),
            _ => new Color(1f,.68f,.2f,1f)
        };
    }

}
