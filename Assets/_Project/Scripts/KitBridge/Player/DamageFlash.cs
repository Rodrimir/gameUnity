using System.Collections.Generic;
using GameYunit.Combat;
using UnityEngine;

namespace GameYunit.KitBridge
{
    /// <summary>
    /// Pisca o corpo de vermelho quando a vida cai.
    /// </summary>
    /// <remarks>
    /// Usa <see cref="MaterialPropertyBlock"/> em vez de trocar o material: não cria instâncias de
    /// material em runtime e não briga com a cor do personagem, que é aplicada pelo mesmo caminho —
    /// o bloco é reescrito inteiro a cada quadro do piscar e devolvido à cor base ao fim.
    /// </remarks>
    [RequireComponent(typeof(NetworkHealth))]
    public sealed class DamageFlash : MonoBehaviour
    {
        [SerializeField] private Color _flashColor = new(1f, 0.15f, 0.12f);
        [SerializeField, Min(0.05f)] private float _duration = 0.25f;

        private readonly List<SkinnedMeshRenderer> _renderers = new();
        private MaterialPropertyBlock _block;
        private NetworkHealth _health;
        private Color _baseColor = Color.white;
        private float _until;

        /// <summary>Cor de descanso, definida por quem pinta o personagem.</summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            if (Time.time >= _until) Paint(color);
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            GetComponentsInChildren(true, _renderers);
            _health = GetComponent<NetworkHealth>();
            _health.Damaged += OnDamaged;
        }

        private void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(int amount) => _until = Time.time + _duration;

        private void Update()
        {
            if (_until <= 0f) return;
            float remaining = _until - Time.time;
            if (remaining <= 0f)
            {
                _until = 0f;
                Paint(_baseColor);
                return;
            }
            // Pulsa entre a cor base e o vermelho, mais forte no começo.
            float t = Mathf.Clamp01(remaining / _duration);
            Paint(Color.Lerp(_baseColor, _flashColor, t));
        }

        private void Paint(Color color)
        {
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            foreach (var renderer in _renderers)
                if (renderer != null) renderer.SetPropertyBlock(_block);
        }
    }
}
