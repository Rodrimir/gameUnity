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
    /// <para>Este componente NÃO move o jogador. Sistemas futuros assinam <see cref="OnServerInputReceived"/>.</para>
    /// <para>
    /// Fluxo: Owner.Update -> SubmitInputRpc (Client -> Server) -> validação -> ServerInput -> OnServerInputReceived.
    /// No Host o RPC executa localmente, pelo mesmo caminho.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerNetwork : NetworkBehaviour
    {
        private const string LogPrefix = "[PlayerNetwork]";

        [Header("Input (lido apenas pelo Owner)")]
        [Tooltip("Action Vector2 de movimento (ex.: Player/Move do InputSystem_Actions).")]
        [SerializeField] private InputActionReference _moveAction;

        [Tooltip("Variação mínima do input para gerar um novo envio. Evita spam de RPC por ruído de analógico.")]
        [SerializeField, Range(0f, 0.25f)] private float _inputChangeThreshold = 0.02f;

        [Header("Debug")]
        [Tooltip("Loga no servidor cada input recebido.")]
        [SerializeField] private bool _logReceivedInput;

        private PlayerInputData _lastSentInput;

        #region Eventos (Observer)

        /// <summary>
        /// O jogador desta máquina foi spawnado. Câmera de terceira pessoa e HUD local assinam aqui.
        /// Dispara apenas na máquina do Owner.
        /// </summary>
        public static event Action<PlayerNetwork> OnLocalPlayerSpawned;

        /// <summary>O jogador desta máquina foi removido da rede.</summary>
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

            if (IsOwner)
            {
                EnableLocalInput();
                OnLocalPlayerSpawned?.Invoke(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                DisableLocalInput();
                OnLocalPlayerDespawned?.Invoke(this);
            }

            if (IsServer)
            {
                ServerInput = default;
            }

            base.OnNetworkDespawn();
        }

        #endregion

        #region Owner (input local)

        private void Update()
        {
            // Validação de autoridade: só a instância do jogador local lê e envia input.
            // O componente não é desabilitado nos demais para não interferir na recepção de RPCs no servidor.
            if (!IsSpawned || !IsOwner)
            {
                return;
            }

            PlayerInputData input = ReadLocalInput();
            if (!ShouldSend(input))
            {
                return;
            }

            _lastSentInput = input;
            SubmitInputRpc(input);
        }

        private PlayerInputData ReadLocalInput()
        {
            Vector2 move = _moveAction != null ? _moveAction.action.ReadValue<Vector2>() : Vector2.zero;
            return new PlayerInputData(move);
        }

        /// <summary>
        /// Envia apenas quando o input muda de forma relevante. Como o envio é por mudança,
        /// o RPC usa entrega confiável (padrão): um "parar" perdido deixaria o jogador andando.
        /// </summary>
        private bool ShouldSend(in PlayerInputData input)
        {
            float threshold = _inputChangeThreshold;
            if ((input.Move - _lastSentInput.Move).sqrMagnitude > threshold * threshold)
            {
                return true;
            }

            // Garante que o retorno ao repouso sempre chegue ao servidor, mesmo abaixo do limiar.
            return input.Move == Vector2.zero && _lastSentInput.Move != Vector2.zero;
        }

        private void EnableLocalInput()
        {
            if (_moveAction == null)
            {
                Debug.LogWarning($"{LogPrefix} Move Action não atribuída; o jogador local não enviará input.", this);
                return;
            }

            _moveAction.action.Enable();
        }

        private void DisableLocalInput()
        {
            if (_moveAction != null)
            {
                _moveAction.action.Disable();
            }

            _lastSentInput = default;
        }

        #endregion

        #region Servidor (validação e estado)

        /// <summary>
        /// [Client -> Server] Entrega o input do Owner. Executa somente no servidor.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void SubmitInputRpc(PlayerInputData input, RpcParams rpcParams = default)
        {
            // Só o dono deste jogador pode comandá-lo.
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (senderId != OwnerClientId)
            {
                Debug.LogWarning($"{LogPrefix} Input recusado: client {senderId} não é dono do jogador {OwnerClientId}.", this);
                return;
            }

            ServerInput = input.Sanitized();

            if (_logReceivedInput)
            {
                Debug.Log($"{LogPrefix} [Server] Input do client {OwnerClientId}: {ServerInput}", this);
            }

            OnServerInputReceived?.Invoke(ServerInput);
        }

        #endregion

        // Com "Enter Play Mode Options" (sem domain reload) eventos estáticos sobrevivem entre sessões de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OnLocalPlayerSpawned = null;
            OnLocalPlayerDespawned = null;
        }
    }
}
