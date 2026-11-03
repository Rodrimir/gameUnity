using System.Collections;
using Gamekit3D;
using Gamekit3D.Message;
using Unity.Netcode;
using UnityEngine;

namespace GameYunit.KitBridge
{
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerCombat : NetworkBehaviour, IMessageReceiver
    {
        [SerializeField, Min(1)] private int _maxHealth = 100;
        [SerializeField, Min(0.1f)] private float _respawnDelay = 3f;
        public readonly NetworkVariable<int> Health = new();
        public readonly NetworkVariable<bool> Downed = new();

        private Damageable _damageable;
        private PlayerController _controller;
        private PlayerInput _input;
        private CharacterController _characterController;
        private Animator _animator;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Coroutine _respawnRoutine;

        private void Awake()
        {
            _damageable = GetComponent<Damageable>() ?? gameObject.AddComponent<Damageable>();
            _damageable.enabled = true;
            _damageable.maxHitPoints = _maxHealth;
            EnsureDamageableEvents();
            if (_damageable.onDamageMessageReceivers == null)
                _damageable.onDamageMessageReceivers = new System.Collections.Generic.List<MonoBehaviour>();
            if (!_damageable.onDamageMessageReceivers.Contains(this)) _damageable.onDamageMessageReceivers.Add(this);
            _controller = GetComponent<PlayerController>();
            _input = GetComponent<PlayerInput>();
            _characterController = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
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
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            if (IsServer)
            {
                _damageable.ResetDamage();
                Health.Value = _maxHealth;
                Downed.Value = false;
            }
            ApplyLocalState(Downed.Value, false);
        }

        public void OnReceiveMessage(MessageType type, object sender, object data)
        {
            if (!IsServer || data is not Damageable.DamageMessage damage) return;
            Health.Value = Mathf.Clamp(_damageable.currentHitPoints, 0, _maxHealth);
            if (type == MessageType.DEAD || Health.Value <= 0)
            {
                if (Downed.Value) return;
                Downed.Value = true;
                ApplyLocalState(true, true);
                _respawnRoutine ??= StartCoroutine(RespawnServer());
            }
            else NotifyDamageClientRpc(damage.direction);
        }

        [ClientRpc]
        private void NotifyDamageClientRpc(Vector3 direction)
        {
            if (_animator != null) _animator.SetTrigger("Hurt");
        }

        private IEnumerator RespawnServer()
        {
            yield return new WaitForSeconds(_respawnDelay);
            if (!IsSpawned) yield break;
            if (_characterController != null) _characterController.enabled = false;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            _damageable.ResetDamage();
            Health.Value = _maxHealth;
            Downed.Value = false;
            NotifyRespawnClientRpc();
            ApplyLocalState(false, false);
            _respawnRoutine = null;
        }

        [ClientRpc]
        private void NotifyRespawnClientRpc()
        {
            if (_animator != null) _animator.SetTrigger("Respawn");
            ApplyLocalState(false, false);
        }

        private void ApplyLocalState(bool downed, bool playDeath)
        {
            if (_input != null && IsOwner) _input.enabled = !downed;
            if (_characterController != null && IsOwner) _characterController.enabled = !downed;
            if (_controller != null) _controller.canAttack = !downed;
            if (playDeath && _animator != null) _animator.SetTrigger("Death");
        }

        public override void OnNetworkDespawn()
        {
            if (_damageable != null) _damageable.onDamageMessageReceivers.Remove(this);
            if (_respawnRoutine != null) StopCoroutine(_respawnRoutine);
        }
    }
}
