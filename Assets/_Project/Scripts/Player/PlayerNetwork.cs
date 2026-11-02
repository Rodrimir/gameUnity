using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameYunit.Player
{
    /// <summary>
    /// Ponte de rede do jogador. O Owner (jogador local) lê o input e o envia ao servidor;
    /// o servidor valida, guarda e notifica os sistemas server-authoritative.
    /// </summary>
    /// <remarks>
    /// <para>Este componente não move o jogador. PlayerController assina <see cref="OnServerInputReceived"/>.</para>
    /// <para>
    /// Fluxo: Owner.Update -> SubmitInputRpc (Client -> Server) -> validação -> ServerInput -> OnServerInputReceived.
    /// No Host o RPC executa localmente, pelo mesmo caminho.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(PlayerAimInput))]
    public sealed class PlayerNetwork : NetworkBehaviour
    {
        private const string LogPrefix = "[PlayerNetwork]";

        [Header("Input (lido apenas pelo Owner)")]
        [Tooltip("Action Vector2 de movimento (ex.: Player/Move do InputSystem_Actions).")]
        [SerializeField] private InputActionReference _moveAction;

        [Tooltip("Variação mínima do input para gerar um novo envio. Evita spam de RPC por ruído de analógico.")]
        [SerializeField, Range(0f, 0.25f)] private float _inputChangeThreshold = 0.02f;

        [Tooltip("Reenvia o estado mesmo sem mudanças para manter o comando ativo no servidor.")]
        [SerializeField, Range(0.05f, 0.5f)] private float _inputHeartbeatInterval = 0.2f;

        [Tooltip("Interrompe a intenção de movimento se o Owner parar de enviar input.")]
        [SerializeField, Min(0.6f)] private float _serverInputTimeout = 1f;

        [Header("Debug")]
        [Tooltip("Loga no servidor cada input recebido.")]
        [SerializeField] private bool _logReceivedInput;

        private PlayerInputData _lastSentInput;
        private double _lastSentTime;
        private double _lastServerInputTime;
        private InputAction _localMoveAction;
        private PlayerAimInput _localAimInput;

        /// <summary>Jogador local atual. Permite que novos observers recuperem um spawn já ocorrido.</summary>
        public static PlayerNetwork LocalPlayer { get; private set; }

        #region Eventos (Observer)

        /// <summary>
        /// O jogador local foi spawnado ou recebeu ownership. Câmera e HUD local assinam aqui.
        /// Dispara apenas na máquina do Owner.
        /// </summary>
        public static event Action<PlayerNetwork> OnLocalPlayerSpawned;

        /// <summary>O jogador local foi removido da rede ou perdeu ownership.</summary>
        public static event Action<PlayerNetwork> OnLocalPlayerDespawned;

        /// <summary>
        /// [Servidor] Novo input validado deste jogador. Ponto de entrada da lógica server-authoritative
        /// (movimento, mira etc.).
        /// </summary>
        public event Action<PlayerInputData> OnServerInputReceived;

        #endregion

        /// <summary>[Servidor] Último input validado. Em clients permanece no valor padrão.</summary>
        public PlayerInputData ServerInput { get; private set; }

        #region Ciclo de Rede

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                SetServerInput(default);
            }

            RegisterLocalPlayer();
        }

        public override void OnNetworkDespawn()
        {
            UnregisterLocalPlayer();

            if (IsServer)
            {
                SetServerInput(default);
            }

            base.OnNetworkDespawn();
        }

        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            RegisterLocalPlayer();
        }

        public override void OnLostOwnership()
        {
            UnregisterLocalPlayer();
            base.OnLostOwnership();
        }

        private void OnEnable()
        {
            if (IsSpawned && IsClient && IsOwner)
            {
                EnableLocalInput();
            }
        }

        private void OnDisable()
        {
            if (IsSpawned && IsServer)
            {
                SetServerInput(default);
            }
            else if (IsSpawned && IsClient && IsOwner)
            {
                SubmitInputRpc(default);
            }

            DisableLocalInput();
        }

        #endregion

        #region Owner (input local)

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer && (ServerInput.Move != Vector2.zero || ServerInput.Aim != Vector2.zero) &&
                Time.unscaledTimeAsDouble - _lastServerInputTime >= _serverInputTimeout)
            {
                SetServerInput(default);
            }

            // Validação de autoridade: só a instância do jogador local lê e envia input.
            // O componente não é desabilitado nos demais para não interferir na recepção de RPCs no servidor.
            if (!IsClient || !IsOwner)
            {
                return;
            }

            PlayerInputData input = ReadLocalInput();
            if (!ShouldSend(input) && Time.unscaledTimeAsDouble - _lastSentTime < _inputHeartbeatInterval)
            {
                return;
            }

            _lastSentInput = input;
            _lastSentTime = Time.unscaledTimeAsDouble;
            SubmitInputRpc(input);
        }

        private PlayerInputData ReadLocalInput()
        {
            if (!Application.isFocused)
            {
                return default;
            }

            Vector2 move = _localMoveAction != null ? _localMoveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector2 aim = _localAimInput != null ? _localAimInput.ReadAim() : Vector2.zero;
            return new PlayerInputData(move, aim).Sanitized();
        }

        /// <summary>
        /// Envia mudanças relevantes imediatamente, incluindo a parada. Um heartbeat periódico
        /// mantém comandos constantes ativos. O RPC usa entrega confiável (padrão).
        /// </summary>
        private bool ShouldSend(in PlayerInputData input)
        {
            float threshold = _inputChangeThreshold;
            if ((input.Move - _lastSentInput.Move).sqrMagnitude > threshold * threshold ||
                (input.Aim - _lastSentInput.Aim).sqrMagnitude > threshold * threshold)
            {
                return true;
            }

            // Garante que o retorno ao repouso sempre chegue ao servidor, mesmo abaixo do limiar.
            return (input.Move == Vector2.zero && _lastSentInput.Move != Vector2.zero) ||
                (input.Aim == Vector2.zero && _lastSentInput.Aim != Vector2.zero);
        }

        private void EnableLocalInput()
        {
            if (_localAimInput == null)
            {
                _localAimInput = GetComponent<PlayerAimInput>();
            }

            _localAimInput.EnableInput();
            if (_localMoveAction != null)
            {
                return;
            }

            if (_moveAction == null || _moveAction.action == null)
            {
                Debug.LogWarning($"{LogPrefix} Move Action não atribuída; o jogador local não enviará input.", this);
                return;
            }

            // Cada Owner controla sua cópia; despawn de um clone remoto não desativa a action local.
            _localMoveAction = _moveAction.action.Clone();
            _localMoveAction.Enable();
            _lastSentTime = double.NegativeInfinity;
        }

        private void DisableLocalInput()
        {
            if (_localAimInput != null)
            {
                _localAimInput.DisableInput();
            }

            if (_localMoveAction != null)
            {
                _localMoveAction.Disable();
                _localMoveAction.Dispose();
                _localMoveAction = null;
            }

            _lastSentInput = default;
            _lastSentTime = double.NegativeInfinity;
        }

        private void RegisterLocalPlayer()
        {
            if (!IsSpawned || !IsClient || !IsOwner || LocalPlayer == this)
            {
                return;
            }

            LocalPlayer = this;
            if (isActiveAndEnabled)
            {
                EnableLocalInput();
            }

            OnLocalPlayerSpawned?.Invoke(this);
        }

        private void UnregisterLocalPlayer()
        {
            DisableLocalInput();
            if (LocalPlayer != this)
            {
                return;
            }

            LocalPlayer = null;
            OnLocalPlayerDespawned?.Invoke(this);
        }

        #endregion

        #region Servidor (validação e estado)

        /// <summary>
        /// [Client -> Server] Entrega o input do Owner. Executa somente no servidor.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void SubmitInputRpc(PlayerInputData input, RpcParams rpcParams = default)
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            // Só o dono deste jogador pode comandá-lo.
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (senderId != OwnerClientId)
            {
                Debug.LogWarning($"{LogPrefix} Input recusado: client {senderId} não é dono do jogador {OwnerClientId}.", this);
                return;
            }

            SetServerInput(input.Sanitized());

            if (_logReceivedInput)
            {
                Debug.Log($"{LogPrefix} [Server] Input do client {OwnerClientId}: {ServerInput}", this);
            }
        }

        private void SetServerInput(PlayerInputData input)
        {
            ServerInput = input;
            _lastServerInputTime = Time.unscaledTimeAsDouble;
            OnServerInputReceived?.Invoke(ServerInput);
        }

        #endregion

        // Com "Enter Play Mode Options" (sem domain reload) eventos estáticos sobrevivem entre sessões de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnLocalPlayerSpawned = null;
            OnLocalPlayerDespawned = null;
            LocalPlayer = null;
        }
    }
}
