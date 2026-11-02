using System;
using System.Collections.Generic;
using GameYunit.Combat;
using GameYunit.Core.Pooling;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace GameYunit.Enemies
{
    /// <summary>Perseguição e colisões no servidor. NetworkTransform apresenta o resultado nos clients.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(CharacterController))]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class EnemyAI : NetworkBehaviour, IPoolable
    {
        [SerializeField, Min(0f)] private float _moveSpeed = 1.5f;
        [SerializeField, Min(0f)] private float _stoppingDistance = 1f;
        [SerializeField, Min(0.02f)] private float _retargetInterval = 0.25f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 540f;
        [SerializeField, Min(0f)] private float _gravity = 25f;
        [SerializeField, Min(0f)] private float _terminalFallSpeed = 40f;

        private static readonly List<EnemyAI> ActiveOnServerList = new List<EnemyAI>();

        private CharacterController _controller;
        private IEnemySteering _steering;
        private HealthComponent _health;
        private NetworkObject _target;
        private float _nextRetargetTime;
        private float _verticalSpeed;
        private float _nextContactDamageTime;

        /// <summary>
        /// [Servidor] Inimigos publicados neste peer. Permite que armas escolham alvos
        /// sem consultas de física nem configuração de layers. Vazio nos clients.
        /// </summary>
        public static IReadOnlyList<EnemyAI> ActiveOnServer => ActiveOnServerList;

        /// <summary>Observer local ao servidor: permite ao spawner acompanhar sua população.</summary>
        public event Action<EnemyAI> OnServerDespawned;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResetSimulation();
            _controller.enabled = IsServer;
            if (IsServer)
            {
                if (!ActiveOnServerList.Contains(this)) ActiveOnServerList.Add(this);
                _health.OnDeath += HandleDeath;
            }
        }

        public override void OnNetworkDespawn()
        {
            ResetSimulation();
            ActiveOnServerList.Remove(this);
            if (IsServer)
            {
                _health.OnDeath -= HandleDeath;
                OnServerDespawned?.Invoke(this);
            }

            base.OnNetworkDespawn();
        }

        private void Awake()
        {
            _steering = GetComponent<IEnemySteering>();
            ResetSimulation();
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !_controller.enabled || GameYunit.Gameplay.GameSessionController.IsPaused)
            {
                return;
            }

            if (Time.time >= _nextRetargetTime)
            {
                FindNearestPlayer();
                _nextRetargetTime = Time.time + _retargetInterval;
            }

            float deltaTime = Time.fixedDeltaTime;
            Vector3 displacement = Vector3.zero;
            if (_target != null && _target.IsSpawned)
            {
                Vector3 toTarget = _target.transform.position - transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance <= _stoppingDistance + 0.5f && Time.time >= _nextContactDamageTime)
                {
                    _target.GetComponent<HealthComponent>()?.ApplyContactDamageServer(8f, _target.OwnerClientId);
                    _nextContactDamageTime = Time.time + 1f;
                }

                // Navegação opcional (ex.: NavMeshSteering): só muda a direção; a parada continua medida até o alvo.
                Vector3 direction = toTarget;
                if (_steering != null)
                {
                    direction = _steering.GetSteeringPoint(transform.position, _target.transform.position) - transform.position;
                    direction.y = 0f;
                }

                float directionLength = direction.magnitude;
                if (distance > 0.001f && directionLength > 0.001f)
                {
                    direction /= directionLength;
                    float step = Mathf.Min(_moveSpeed * deltaTime, Mathf.Max(0f, distance - _stoppingDistance));
                    displacement = direction * step;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(direction, Vector3.up), _rotationSpeed * deltaTime);
                }
            }

            _verticalSpeed = _controller.isGrounded && _verticalSpeed <= 0f
                ? -2f
                : Mathf.Max(_verticalSpeed - _gravity * deltaTime, -_terminalFallSpeed);
            displacement.y = _verticalSpeed * deltaTime;
            _controller.Move(displacement);
        }

        private void FindNearestPlayer()
        {
            _target = null;
            float nearestDistance = float.PositiveInfinity;
            var clients = NetworkManager.ConnectedClientsList;
            for (int i = 0; i < clients.Count; i++)
            {
                NetworkObject player = clients[i].PlayerObject;
                if (player == null || !player.IsSpawned || !player.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (player.TryGetComponent(out HealthComponent health) && health.IsDead)
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    _target = player;
                }
            }
        }

        /// <summary>Para despawn e morte. O handler do NGO fará a devolução ao pool em cada peer.</summary>
        public void ReturnToPoolServer()
        {
            if (IsSpawned && IsServer)
            {
                NetworkObject.Despawn(destroy: true);
            }
        }

        public void OnSpawnedFromPool() => ResetSimulation();

        public void OnReturnedToPool() => ResetSimulation();

        private void HandleDeath(HealthComponent health) => ReturnToPoolServer();

        private void ResetSimulation()
        {
            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
                _health = GetComponent<HealthComponent>();
            }

            _controller.enabled = false;
            _target = null;
            _verticalSpeed = 0f;
            _nextRetargetTime = 0f;
            _nextContactDamageTime = 0f;
        }

        // Com "Enter Play Mode Options" (sem domain reload) estáticos sobrevivem entre sessões de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ActiveOnServerList.Clear();
    }
}
