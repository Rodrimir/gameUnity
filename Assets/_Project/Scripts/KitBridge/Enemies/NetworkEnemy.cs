using System.Collections.Generic;
using GameYunit.Network;
using Gamekit3D;
using Gamekit3D.Message;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

namespace GameYunit.KitBridge
{
    /// <summary>
    /// Inimigo autoritativo do servidor. A aparência pode vir de qualquer pacote, mas o
    /// movimento, dano, vida e morte são sempre decididos no servidor.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    public sealed class NetworkEnemy : NetworkBehaviour, IMessageReceiver
    {
        [SerializeField, Min(1)] private int _maxHealth = 40;
        [SerializeField, Min(0.1f)] private float _moveSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float _attackRange = 1.7f;
        [SerializeField, Min(0.1f)] private float _attackCooldown = 1.4f;
        [SerializeField, Min(1)] private int _attackDamage = 8;
        [SerializeField] private string _speedParameter = "Speed";
        [SerializeField] private string _attackTrigger = "Attack";
        [SerializeField] private string _deadParameter = "Dead";

        public readonly NetworkVariable<int> Health = new();
        public readonly NetworkVariable<bool> Dead = new();

        private NavMeshAgent _agent;
        private Animator _animator;
        private Damageable _damageable;
        private float _nextAttack;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Coroutine _respawnRoutine;
        private static readonly List<NetworkPlayerIdentity> Players = new();

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _animator = GetComponentInChildren<Animator>();
            _damageable = GetComponent<Damageable>();
            if (GetComponent<Collider>() == null)
            {
                var hitbox = gameObject.AddComponent<CapsuleCollider>();
                hitbox.center = new Vector3(0, 0.9f, 0);
                hitbox.height = 1.8f;
                hitbox.radius = 0.35f;
            }
            if (_damageable == null)
            {
                _damageable = gameObject.AddComponent<Damageable>();
                _damageable.maxHitPoints = _maxHealth;
            }
            EnsureDamageableEvents();
            if (_damageable.onDamageMessageReceivers == null)
                _damageable.onDamageMessageReceivers = new List<MonoBehaviour>();
            if (_damageable != null && !_damageable.onDamageMessageReceivers.Contains(this))
                _damageable.onDamageMessageReceivers.Add(this);
        }

        private void EnsureDamageableEvents()
        {
            _damageable.OnDeath ??= new UnityEngine.Events.UnityEvent();
            _damageable.OnReceiveDamage ??= new UnityEngine.Events.UnityEvent();
            _damageable.OnHitWhileInvulnerable ??= new UnityEngine.Events.UnityEvent();
            _damageable.OnBecomeVulnerable ??= new UnityEngine.Events.UnityEvent();
            _damageable.OnResetDamage ??= new UnityEngine.Events.UnityEvent();
        }

        public override void OnNetworkSpawn()
        {
            Dead.OnValueChanged += OnDeadChanged;
            if (IsServer)
            {
                _spawnPosition = transform.position;
                _spawnRotation = transform.rotation;
                Health.Value = _maxHealth;
                Dead.Value = false;
                if (_agent != null)
                {
                    _agent.speed = _moveSpeed;
                    _agent.stoppingDistance = _attackRange * 0.75f;
                    _agent.Warp(transform.position);
                }
            }
            ApplyVisualState(Dead.Value);
        }

        private void Update()
        {
            if (!IsServer || Dead.Value) return;
            NetworkPlayerIdentity target = FindNearestPlayer();
            if (target == null) return;

            Vector3 delta = target.transform.position - transform.position;
            delta.y = 0;
            float distance = delta.magnitude;
            if (distance > _attackRange)
            {
                if (_agent != null && _agent.isOnNavMesh)
                    _agent.SetDestination(target.transform.position);
                else
                    transform.position += delta.normalized * (_moveSpeed * Time.deltaTime);
                if (delta.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(delta);
                SetSpeed(_moveSpeed);
            }
            else
            {
                if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();
                SetSpeed(0);
                if (delta.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(delta);
                if (Time.time >= _nextAttack)
                {
                    _nextAttack = Time.time + _attackCooldown;
                    _animator?.SetTrigger(_attackTrigger);
                    DamagePlayer(target, delta.normalized);
                }
            }
        }

        private NetworkPlayerIdentity FindNearestPlayer()
        {
            Players.Clear();
            Players.AddRange(FindObjectsByType<NetworkPlayerIdentity>(FindObjectsSortMode.None));
            NetworkPlayerIdentity result = null;
            float best = float.MaxValue;
            foreach (var player in Players)
            {
                if (player == null || !player.IsSpawned) continue;
                float distance = (player.transform.position - transform.position).sqrMagnitude;
                if (distance < best) { best = distance; result = player; }
            }
            return result;
        }

        private void DamagePlayer(NetworkPlayerIdentity target, Vector3 direction)
        {
            Damageable damageable = target.GetComponentInChildren<Damageable>();
            if (damageable == null || damageable.isInvulnerable) return;
            damageable.ApplyDamage(new Damageable.DamageMessage
            {
                damager = this,
                amount = _attackDamage,
                direction = direction,
                damageSource = transform.position,
                throwing = false
            });
        }

        public void OnReceiveMessage(MessageType type, object sender, object message)
        {
            if (type != MessageType.DAMAGED && type != MessageType.DEAD) return;
            if (message is not Damageable.DamageMessage damage) return;
            if (IsServer) ReceiveDamageServer(damage.amount);
            else RequestDamageServerRpc(damage.amount);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDamageServerRpc(int amount)
        {
            ReceiveDamageServer(amount);
        }

        private void ReceiveDamageServer(int amount)
        {
            if (Dead.Value) return;
            Health.Value = Mathf.Max(0, Health.Value - Mathf.Max(1, amount));
            if (Health.Value == 0) Dead.Value = true;
            if (Health.Value == 0 && _respawnRoutine == null) _respawnRoutine = StartCoroutine(RespawnServer());
        }

        private System.Collections.IEnumerator RespawnServer()
        {
            yield return new WaitForSeconds(4f);
            if (!IsSpawned) yield break;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            _damageable.ResetDamage();
            Health.Value = _maxHealth;
            Dead.Value = false;
            _respawnRoutine = null;
        }

        private void OnDeadChanged(bool previous, bool current) => ApplyVisualState(current);

        private void ApplyVisualState(bool dead)
        {
            if (_agent != null) _agent.enabled = !dead;
            if (_damageable != null) _damageable.enabled = !dead;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = !dead;
            if (_animator != null) _animator.SetBool(_deadParameter, dead);
        }

        private void SetSpeed(float speed)
        {
            if (_animator != null) _animator.SetFloat(_speedParameter, speed);
        }

        public override void OnNetworkDespawn() => Dead.OnValueChanged -= OnDeadChanged;
    }
}
