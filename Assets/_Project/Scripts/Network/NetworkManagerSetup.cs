using System;
using GameYunit.Map;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace GameYunit.Network
{
    /// <summary>
    /// Ponto único de inicialização do Netcode for GameObjects: inicia como Host ou Client,
    /// configura o transporte e limita a sessão a <see cref="MaxPlayers"/> jogadores.
    /// </summary>
    /// <remarks>
    /// <para>Adicione no mesmo GameObject do <see cref="NetworkManager"/> e do <see cref="UnityTransport"/>.</para>
    /// <para>
    /// Observer: os callbacks do NGO são reexpostos como eventos C# desta classe, para que UI e
    /// sistemas de jogo não dependam diretamente do NetworkManager.
    /// </para>
    /// <para>O UnityTransport usa UDP; o endereço e a porta são configuráveis no Inspector.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class NetworkManagerSetup : MonoBehaviour
    {
        private const string LogPrefix = "[NetworkManagerSetup]";

        [Header("Conexão")]
        [Tooltip("Endereço IPv4 ao qual o Client se conecta.")]
        [SerializeField] private string _connectAddress = "127.0.0.1";

        [Tooltip("Porta UDP do UnityTransport (Host escuta, Client conecta).")]
        [SerializeField] private ushort _port = 7777;

        [Tooltip("Interface em que o Host escuta. 127.0.0.1 = só esta máquina; 0.0.0.0 = toda a rede local.")]
        [SerializeField] private string _listenAddress = "127.0.0.1";

        [Header("Sessão")]
        [Tooltip("Número máximo de jogadores conectados, incluindo o Host.")]
        [SerializeField, Min(1)] private int _maxPlayers = 2;

        private NetworkManager _networkManager;
        private UnityTransport _transport;

        #region Eventos (Observer)

        /// <summary>Servidor local iniciado em modo Host.</summary>
        public event Action OnHostStarted;

        /// <summary>Client puro iniciou a tentativa de conexão (ainda não conectado).</summary>
        public event Action OnClientStarted;

        /// <summary>
        /// Um client conectou. No servidor dispara para todos (inclusive o próprio Host, id 0);
        /// no Client dispara apenas para a própria conexão.
        /// </summary>
        public event Action<ulong> OnClientConnected;

        /// <summary>Um client desconectou (mesma regra de escopo de <see cref="OnClientConnected"/>).</summary>
        public event Action<ulong> OnClientDisconnected;

        /// <summary>Falha ao iniciar a sessão ou falha de transporte. O argumento descreve o motivo.</summary>
        public event Action<string> OnConnectionFailed;

        /// <summary>A sessão local foi encerrada (Host ou Client).</summary>
        public event Action OnShutdown;

        #endregion

        /// <summary>True enquanto há uma sessão ativa ou encerrando.</summary>
        public bool IsRunning => _networkManager != null && (_networkManager.IsListening || _networkManager.ShutdownInProgress);

        public bool IsHost => _networkManager != null && _networkManager.IsHost;

        public string ConnectAddress => _connectAddress;

        public string ListenAddress => _listenAddress;

        public ushort Port => _port;

        public int MaxPlayers => _maxPlayers;

        #region Unity Lifecycle

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();

            if (_networkManager.NetworkConfig.NetworkTransport == null)
            {
                _networkManager.NetworkConfig.NetworkTransport = _transport;
            }

            // A aprovação precisa estar ativa antes de StartHost/StartClient, nos dois lados.
            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.ConnectionApprovalCallback = ApproveConnection;
        }

        private void OnEnable()
        {
            _networkManager.OnServerStarted += HandleServerStarted;
            _networkManager.OnClientStarted += HandleClientStarted;
            _networkManager.OnClientConnectedCallback += HandleClientConnected;
            _networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            _networkManager.OnClientStopped += HandleClientStopped;
            _networkManager.OnTransportFailure += HandleTransportFailure;
        }

        private void OnDisable()
        {
            if (_networkManager == null)
            {
                return;
            }

            _networkManager.OnServerStarted -= HandleServerStarted;
            _networkManager.OnClientStarted -= HandleClientStarted;
            _networkManager.OnClientConnectedCallback -= HandleClientConnected;
            _networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _networkManager.OnClientStopped -= HandleClientStopped;
            _networkManager.OnTransportFailure -= HandleTransportFailure;
        }

        private void OnDestroy()
        {
            if (_networkManager != null)
            {
                _networkManager.ConnectionApprovalCallback -= ApproveConnection;
            }
        }

        #endregion

        #region API Pública

        /// <summary>Inicia a sessão como Host (servidor + client local).</summary>
        /// <returns>True se o NGO iniciou o Host.</returns>
        public bool StartHost()
        {
            if (!CanStart())
            {
                return false;
            }

            _transport.SetConnectionData(_connectAddress, _port, _listenAddress);

            bool started = _networkManager.StartHost();
            if (!started)
            {
                RaiseConnectionFailed($"Não foi possível iniciar o Host em {_listenAddress}:{_port}.");
            }

            return started;
        }

        /// <summary>Conecta como Client em <see cref="ConnectAddress"/>:<see cref="Port"/>.</summary>
        /// <returns>True se o NGO iniciou a tentativa de conexão.</returns>
        public bool StartClient()
        {
            if (!CanStart())
            {
                return false;
            }

            _transport.SetConnectionData(_connectAddress, _port);

            bool started = _networkManager.StartClient();
            if (!started)
            {
                RaiseConnectionFailed($"Não foi possível iniciar o Client para {_connectAddress}:{_port}.");
            }

            return started;
        }

        /// <summary>Encerra a sessão atual, se houver.</summary>
        public void Shutdown()
        {
            if (_networkManager == null || !_networkManager.IsListening)
            {
                return;
            }

            _networkManager.Shutdown();
        }

        #endregion

        #region Servidor

        /// <summary>
        /// Executa somente no servidor (inclusive para o próprio Host ao iniciar).
        /// Recusa conexões além de <see cref="MaxPlayers"/>.
        /// </summary>
        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            int playerIndex = _networkManager.ConnectedClientsIds.Count;
            bool hasRoom = playerIndex < _maxPlayers;

            response.Approved = hasRoom;
            response.CreatePlayerObject = hasRoom;
            response.Pending = false;

            // Sem PlayerSpawnPoint na cena, o NGO mantém o comportamento padrão (origem).
            if (hasRoom && PlayerSpawnPoint.TryGetSpawn(playerIndex, out Vector3 position, out Quaternion rotation))
            {
                response.Position = position;
                response.Rotation = rotation;
            }

            if (!hasRoom)
            {
                response.Reason = $"Sala cheia ({_maxPlayers}/{_maxPlayers}).";
                Debug.Log($"{LogPrefix} Conexão do client {request.ClientNetworkId} recusada: sala cheia.");
            }
        }

        #endregion

        #region Handlers do NGO

        private void HandleServerStarted()
        {
            if (!_networkManager.IsHost)
            {
                return;
            }

            Debug.Log($"{LogPrefix} Host iniciado em {_listenAddress}:{_port}.");
            OnHostStarted?.Invoke();
        }

        private void HandleClientStarted()
        {
            // O NGO também dispara este callback no Host; aqui só interessa o Client puro.
            if (_networkManager.IsHost)
            {
                return;
            }

            Debug.Log($"{LogPrefix} Conectando a {_connectAddress}:{_port}...");
            OnClientStarted?.Invoke();
        }

        private void HandleClientConnected(ulong clientId)
        {
            Debug.Log($"{LogPrefix} Client {clientId} conectado.");
            OnClientConnected?.Invoke(clientId);
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            // No Client, DisconnectReason explica recusas do servidor (ex.: sala cheia).
            if (!_networkManager.IsServer && !string.IsNullOrEmpty(_networkManager.DisconnectReason))
            {
                Debug.LogWarning($"{LogPrefix} Desconectado pelo servidor: {_networkManager.DisconnectReason}");
            }
            else
            {
                Debug.Log($"{LogPrefix} Client {clientId} desconectado.");
            }

            OnClientDisconnected?.Invoke(clientId);
        }

        private void HandleClientStopped(bool wasHost)
        {
            // Dispara tanto no Host quanto no Client; não há servidor dedicado neste projeto.
            Debug.Log($"{LogPrefix} Sessão encerrada ({(wasHost ? "Host" : "Client")}).");
            OnShutdown?.Invoke();
        }

        private void HandleTransportFailure()
        {
            RaiseConnectionFailed("Falha no transporte de rede.");
        }

        #endregion

        private bool CanStart()
        {
            if (!IsRunning)
            {
                return true;
            }

            Debug.LogWarning($"{LogPrefix} Já existe uma sessão ativa (ou encerrando); chamada ignorada.");
            return false;
        }

        private void RaiseConnectionFailed(string reason)
        {
            Debug.LogError($"{LogPrefix} {reason}");
            OnConnectionFailed?.Invoke(reason);
        }
    }
}
