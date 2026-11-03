using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.Combat
{
    /// <summary>Vida autoritativa: só o servidor altera HP, cura, upgrades e estado de morte.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class HealthComponent : NetworkBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int _maxHealth = 100;

        private readonly NetworkVariable<int> _currentHealth = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _maxHealthValue = new(100,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _damageMultiplier = new(1f,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private static readonly List<HealthComponent> ActiveList = new();
        private float _nextContactDamageTime;
        private bool _deathRaised;

        public static IReadOnlyList<HealthComponent> Active => ActiveList;
        public static event Action<HealthComponent> OnAnyDeath;
        public static event Action<HealthComponent> OnAnyHealthChanged;

        public int MaxHealth => _maxHealthValue.Value;
        public int CurrentHealth => _currentHealth.Value;
        public int Health => CurrentHealth;
        public float NormalizedHealth => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;
        public float DamageMultiplier => _damageMultiplier.Value;
        public bool IsDead => CurrentHealth <= 0;
        public bool IsAlive => !IsDead;
        public bool IsPlayer => GetComponent<GameYunit.Player.PlayerNetwork>() != null;

        /// <summary>Replicado em cada peer quando o valor de HP muda.</summary>
        public event Action<int, int> OnHealthChanged;

        /// <summary>Emitido somente no servidor, uma vez por vida.</summary>
        public event Action<HealthComponent> OnDeath;

        private void OnEnable()
        {
            if (!ActiveList.Contains(this)) ActiveList.Add(this);
        }

        private void OnDisable() => ActiveList.Remove(this);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _currentHealth.OnValueChanged += HandleHealthChanged;
            _maxHealthValue.OnValueChanged += HandleMaxHealthChanged;
            _deathRaised = false;
            if (IsServer)
            {
                _maxHealthValue.Value = Mathf.Max(1, _maxHealth);
                _currentHealth.Value = _maxHealthValue.Value;
                _damageMultiplier.Value = 1f;
                _nextContactDamageTime = 0f;
            }
            OnAnyHealthChanged?.Invoke(this);
        }

        public override void OnNetworkDespawn()
        {
            _currentHealth.OnValueChanged -= HandleHealthChanged;
            _maxHealthValue.OnValueChanged -= HandleMaxHealthChanged;
            base.OnNetworkDespawn();
        }

        /// <summary>Aplica dano confirmado pelo servidor; o valor é arredondado para cima para preservar impactos fracionários.</summary>
        public void ApplyDamageServer(float amount, ulong sourceClientId, Vector3 hitPoint)
        {
            if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            TakeDamageServer(Mathf.Max(1, Mathf.CeilToInt(amount)));
        }

        public bool TakeDamageServer(int amount)
        {
            if (!IsSpawned || !IsServer || amount <= 0 || IsDead || _deathRaised ||
                GameYunit.Gameplay.GameSessionController.IsPaused)
            {
                return false;
            }

            int remaining = Mathf.Max(0, CurrentHealth - amount);
            _deathRaised = remaining == 0;
            _currentHealth.Value = remaining;
            if (_deathRaised) OnDeath?.Invoke(this);
            return true;
        }

        public bool HealServer(int amount)
        {
            if (!IsSpawned || !IsServer || amount <= 0 || IsDead || CurrentHealth >= MaxHealth)
                return false;

            _currentHealth.Value += Mathf.Min(amount, MaxHealth - CurrentHealth);
            return true;
        }

        public void ApplyContactDamageServer(float amount, ulong sourceClientId)
        {
            if (Time.time < _nextContactDamageTime) return;
            _nextContactDamageTime = Time.time + 1f;
            ApplyDamageServer(amount, sourceClientId, transform.position);
        }

        public void ApplyUpgradeServer(int upgrade)
        {
            if (!IsSpawned || !IsServer || !IsPlayer) return;
            switch (upgrade)
            {
                case 0:
                    if (!IsDead) _damageMultiplier.Value += 0.05f;
                    break;
                case 1:
                    if (IsDead) break;
                    int increase = Mathf.Max(1, Mathf.CeilToInt(MaxHealth * 0.05f));
                    _maxHealthValue.Value += increase;
                    _currentHealth.Value = Mathf.Min(MaxHealth, CurrentHealth + increase);
                    break;
                case 2:
                    _currentHealth.Value = MaxHealth;
                    _deathRaised = false;
                    break;
            }
        }

        private void HandleHealthChanged(int previous, int current)
        {
            OnHealthChanged?.Invoke(previous, current);
            OnAnyHealthChanged?.Invoke(this);
            if (previous > 0 && current == 0) OnAnyDeath?.Invoke(this);
        }

        private void HandleMaxHealthChanged(int previous, int current) => OnAnyHealthChanged?.Invoke(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnAnyDeath = null;
            OnAnyHealthChanged = null;
            ActiveList.Clear();
        }
    }
}
