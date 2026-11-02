using System;
using GameYunit.Core.Pooling;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace GameYunit.Enemies
{
    /// <summary>Perseguição e colisões no servidor. NetworkTransform apresenta o resultado nos clients.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(CharacterController))]
    public sealed class EnemyAI : NetworkBehaviour, IPoolable
    {
        [SerializeField, Min(0f)] private float _moveSpeed = 3f;
        [SerializeField, Min(0f)] private float _stoppingDistance = 1f;
        [SerializeField, Min(0.02f)] private float _retargetInterval = 0.25f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 540f;
        [SerializeField, Min(0f)] private float _gravity = 25f;
        [SerializeField, Min(0f)] private float _terminalFallSpeed = 40f;

        private CharacterController _controller;
        private NetworkObject _target;
        private float _nextRetargetTime;
        private float _verticalSpeed;

        /// <summary>Observer local ao servidor: permite ao spawner acompanhar sua população.</summary>
        public event Action<EnemyAI> OnServerDespawned;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ResetSimulation();
            _controller.enabled = IsServer;
        }

        public override void OnNetworkDespawn()
        {
            ResetSimulation();
            if (IsServer)
            {
                OnServerDespawned?.Invoke(this);
            }

            base.OnNetworkDespawn();
        }

        private void Awake() => ResetSimulation();

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !_controller.enabled)
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
                Vector3 direction = _target.transform.position - transform.position;
                direction.y = 0f;
                float distance = direction.magnitude;
                if (distance > 0.001f)
                {
                    direction /= distance;
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

                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    _target = player;
                }
            }
        }

        /// <summary>Para despawn e, no Bloco 4, morte. O handler do NGO fará a devolução ao pool em cada peer.</summary>
        public void ReturnToPoolServer()
        {
            if (IsSpawned && IsServer)
            {
                NetworkObject.Despawn(destroy: true);
            }
        }

        public void OnSpawnedFromPool() => ResetSimulation();

        public void OnReturnedToPool() => ResetSimulation();

        private void ResetSimulation()
        {
            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
            }

            _controller.enabled = false;
            _target = null;
            _verticalSpeed = 0f;
            _nextRetargetTime = 0f;
        }
    }
}
