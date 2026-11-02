using System;
using System.Collections.Generic;
using GameYunit.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Combat
{
    /// <summary>Vida e modificadores do jogador ou inimigo; todo estado é decidido pelo servidor.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class HealthComponent : NetworkBehaviour, IDamageable
    {
        private const float PlayerBaseHealth = 100f;
        private const float EnemyBaseHealth = 60f;
        private readonly NetworkVariable<float> _health = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _maxHealth = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _damageMultiplier = new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _alive = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private static readonly System.Collections.Generic.List<HealthComponent> ActiveList = new();
        private float _nextContactDamageTime;
        public static IReadOnlyList<HealthComponent> Active => ActiveList;

        public static event Action<HealthComponent> OnAnyDeath;
        public static event Action<HealthComponent> OnAnyHealthChanged;
        public float Health => _health.Value;
        public float MaxHealth => _maxHealth.Value;
        public float DamageMultiplier => _damageMultiplier.Value;
        public bool IsAlive => _alive.Value;
        public bool IsPlayer => GetComponent<GameYunit.Player.PlayerNetwork>() != null;

        private void OnEnable() { if (!ActiveList.Contains(this)) ActiveList.Add(this); }
        private void OnDisable() => ActiveList.Remove(this);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _health.OnValueChanged += HandleHealthChanged;
            _maxHealth.OnValueChanged += HandleMaxHealthChanged;
            _alive.OnValueChanged += HandleAliveChanged;
            if (IsServer)
            {
                float starting = IsPlayer ? PlayerBaseHealth : EnemyBaseHealth;
                _maxHealth.Value = starting;
                _health.Value = starting;
                _damageMultiplier.Value = 1f;
                _alive.Value = true;
                _nextContactDamageTime = 0f;
            }
            OnAnyHealthChanged?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            _health.OnValueChanged -= HandleHealthChanged;
            _maxHealth.OnValueChanged -= HandleMaxHealthChanged;
            _alive.OnValueChanged -= HandleAliveChanged;
            base.OnNetworkDespawn();
        }

        public void ApplyDamageServer(float amount, ulong sourceClientId, Vector3 hitPoint)
        {
            if (!IsServer || !IsSpawned || !_alive.Value || amount <= 0f || GameYunit.Gameplay.GameSessionController.IsPaused) return;
            _health.Value = Mathf.Max(0f, _health.Value - amount);
            if (_health.Value > 0f) return;
            _alive.Value = false;
            EnemyAI enemy = GetComponent<EnemyAI>();
            if (enemy != null)
            {
                GameYunit.Gameplay.GameSessionController.Instance?.RegisterKillServer();
                enemy.ReturnToPoolServer();
            }
        }

        public void ApplyUpgradeServer(int upgrade)
        {
            if (!IsServer || !IsSpawned || !IsPlayer) return;
            switch (upgrade)
            {
                case 0: if (_alive.Value) _damageMultiplier.Value += 0.05f; break;
                case 1:
                    if (!_alive.Value) break;
                    float increase = _maxHealth.Value * 0.05f;
                    _maxHealth.Value += increase;
                    _health.Value += increase;
                    break;
                case 2:
                    _health.Value = _maxHealth.Value;
                    _alive.Value = true;
                    break;
            }
        }

        public void ApplyContactDamageServer(float amount, ulong sourceClientId)
        {
            if (Time.time < _nextContactDamageTime) return;
            _nextContactDamageTime = Time.time + 1f;
            ApplyDamageServer(amount, sourceClientId, transform.position);
        }

        private void HandleHealthChanged(float previous, float current) => OnAnyHealthChanged?.Invoke(this);
        private void HandleMaxHealthChanged(float previous, float current) => OnAnyHealthChanged?.Invoke(this);
        private void HandleAliveChanged(bool previous, bool current)
        {
            if (!current) { OnAnyDeath?.Invoke(this); OnAnyHealthChanged?.Invoke(this); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { OnAnyDeath = null; OnAnyHealthChanged = null; ActiveList.Clear(); }
    }
}
