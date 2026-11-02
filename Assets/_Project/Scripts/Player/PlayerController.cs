using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace GameYunit.Player
{
    /// <summary>
    /// Movimento autoritativo: consome a intenção validada de PlayerNetwork no servidor.
    /// NetworkTransform replica o resultado; os clients nunca executam CharacterController.Move.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(PlayerNetwork), typeof(NetworkTransform))]
    public sealed class PlayerController : NetworkBehaviour
    {
        [Header("Movimento (simulado no servidor)")]
        [SerializeField, Min(0f)] private float _moveSpeed = 6f;

        [Tooltip("Aceleração para baixo, em unidades/s².")]
        [SerializeField, Min(0f)] private float _gravity = 25f;

        [SerializeField, Min(0f)] private float _terminalFallSpeed = 40f;

        [Tooltip("Pequena velocidade para baixo para manter contato com rampas e chão.")]
        [SerializeField, Min(0f)] private float _groundedFallSpeed = 2f;

        [Header("Mira independente (simulada no servidor)")]
        [Tooltip("Velocidade máxima de rotação em graus/s. Zero impede a rotação.")]
        [SerializeField, Min(0f)] private float _rotationSpeed = 720f;

        private CharacterController _characterController;
        private PlayerNetwork _playerNetwork;
        private Vector2 _moveInput;
        private Vector2 _aimInput;
        private float _verticalSpeed;
        private bool _subscribed;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _playerNetwork = GetComponent<PlayerNetwork>();
            _characterController.enabled = false;
        }

        private void OnEnable()
        {
            if (IsSpawned)
            {
                ConfigureServerMovement();
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ConfigureServerMovement();
        }

        public override void OnNetworkDespawn()
        {
            StopServerMovement();
            base.OnNetworkDespawn();
        }

        private void OnDisable() => StopServerMovement();

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !_characterController.enabled ||
                GameYunit.Gameplay.GameSessionController.IsPaused ||
                GetComponent<GameYunit.Combat.HealthComponent>() is { IsAlive: false })
            {
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            if (_characterController.isGrounded && _verticalSpeed <= 0f)
            {
                _verticalSpeed = -_groundedFallSpeed;
            }
            else
            {
                _verticalSpeed = Mathf.Max(_verticalSpeed - _gravity * deltaTime, -_terminalFallSpeed);
            }

            // A intenção usa eixos do mundo: X = lateral, Z = frente. Não depende da câmera.
            Vector3 velocity = new Vector3(_moveInput.x * _moveSpeed, _verticalSpeed, _moveInput.y * _moveSpeed);
            CollisionFlags collisions = _characterController.Move(velocity * deltaTime);

            if ((collisions & CollisionFlags.Above) != 0 && _verticalSpeed > 0f)
            {
                _verticalSpeed = 0f;
            }

            if (_aimInput.sqrMagnitude > 0.0001f)
            {
                Vector3 direction = new Vector3(_aimInput.x, 0f, _aimInput.y);
                Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation,
                    _rotationSpeed * deltaTime);
            }
        }

        private void ConfigureServerMovement()
        {
            _characterController.enabled = IsServer && isActiveAndEnabled;
            if (!_characterController.enabled || _subscribed)
            {
                return;
            }

            HandleServerInputReceived(_playerNetwork.ServerInput);
            _playerNetwork.OnServerInputReceived += HandleServerInputReceived;
            _subscribed = true;
        }

        private void StopServerMovement()
        {
            if (_subscribed)
            {
                _playerNetwork.OnServerInputReceived -= HandleServerInputReceived;
                _subscribed = false;
            }

            _moveInput = Vector2.zero;
            _aimInput = Vector2.zero;
            _verticalSpeed = 0f;
            if (_characterController != null)
            {
                _characterController.enabled = false;
            }
        }

        private void HandleServerInputReceived(PlayerInputData input)
        {
            _moveInput = input.Move;
            _aimInput = input.Aim;
        }
    }
}
