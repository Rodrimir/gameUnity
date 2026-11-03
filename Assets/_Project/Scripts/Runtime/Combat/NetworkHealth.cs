using System;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Combat
{
    /// <summary>
    /// Vida replicada, decidida pelo servidor.
    /// </summary>
    /// <remarks>
    /// Não usa o <c>Damageable</c> do kit: ele fica desabilitado no prefab cooperativo, carrega o fluxo
    /// single-player (respawn por checkpoint, invulnerabilidade que expira no Update) e não é um
    /// <c>NetworkBehaviour</c>. Aqui só o servidor escreve; os clientes leem e reagem.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NetworkHealth : NetworkBehaviour
    {
        [SerializeField, Min(1)] private int _maxHealth = 100;
        [Tooltip("Segundos de invulnerabilidade após levar dano, para não morrer num único tique de área.")]
        [SerializeField, Min(0f)] private float _invulnerability = 0.35f;

        private readonly NetworkVariable<int> _current = new();
        private float _vulnerableAt;

        public int Max => _maxHealth;
        public int Current => _current.Value;
        public bool IsAlive => _current.Value > 0;
        public float Fraction => _maxHealth <= 0 ? 0f : Mathf.Clamp01((float)_current.Value / _maxHealth);

        /// <summary>Dano recebido, em todos os peers: usado pelo piscar e pelo HUD.</summary>
        public event Action<int> Damaged;
        /// <summary>Vida mudou por qualquer motivo, em todos os peers.</summary>
        public event Action Changed;
        /// <summary>Chegou a zero. Só dispara no servidor, que é quem decide.</summary>
        public event Action<NetworkHealth> DiedServer;

        public override void OnNetworkSpawn()
        {
            if (IsServer) _current.Value = _maxHealth;
            _current.OnValueChanged += OnChanged;
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn() => _current.OnValueChanged -= OnChanged;

        private void OnChanged(int previous, int current)
        {
            if (current < previous) Damaged?.Invoke(previous - current);
            Changed?.Invoke();
        }

        /// <summary>Aplica dano. Só o servidor pode chamar; ignora alvo morto ou invulnerável.</summary>
        public void ApplyDamageServer(int amount)
        {
            if (!IsServer || amount <= 0 || !IsAlive || Time.time < _vulnerableAt) return;
            _vulnerableAt = Time.time + _invulnerability;
            _current.Value = Mathf.Max(0, _current.Value - amount);
            if (_current.Value == 0) DiedServer?.Invoke(this);
        }

        public void HealServer(int amount)
        {
            if (!IsServer || amount <= 0 || !IsAlive) return;
            _current.Value = Mathf.Min(_maxHealth, _current.Value + amount);
        }

        public void ReviveServer()
        {
            if (!IsServer) return;
            _current.Value = _maxHealth;
            _vulnerableAt = Time.time + _invulnerability;
        }
    }
}
