using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameYunit.Spells;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameYunit.Network
{
    /// <summary>Fluxo de conexão e cena; deriva das responsabilidades do NetworkManagerSetup legado.</summary>
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class CoopSession : MonoBehaviour
    {
        public const string MenuScene = "CoopMenu";
        public const string LevelScene = "Level1_Coop";
        public static CoopSession Instance { get; private set; }
        [SerializeField] private GameObject _playerPrefab;
        [Tooltip("Catálogo de magias; a ordem precisa ser idêntica nas duas máquinas.")]
        [SerializeField] private SpellCatalog _catalog;
        private readonly Dictionary<ulong, Guest> _guests = new();
        private NetworkManager _manager;
        private UnityTransport _transport;
        private bool _levelReady;
        private bool _returning;
        private bool _quitting;

        [Serializable] private sealed class Guest
        {
            public int version;
            public string name;
            public int color;
            public int spell;
            [NonSerialized] public int slot;
        }

        public NetworkManager Manager => _manager;
        public SpellCatalog Catalog => _catalog;
        public string Status { get; private set; } = "Crie uma sala ou entre pelo IP do parceiro.";
        public ConnectionProtocol Protocol { get; private set; }
        public string Address { get; private set; } = "127.0.0.1";
        public ushort Port { get; private set; } = 7777;
        public bool Busy => _manager != null && (_manager.IsListening || _manager.ShutdownInProgress || _returning);
        public bool LocalMenuOpen { get; private set; }
        public event Action MenuChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _manager = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
            _manager.NetworkConfig.NetworkTransport = _transport;
            _manager.NetworkConfig.ConnectionApproval = true;
            _manager.NetworkConfig.EnableSceneManagement = true;
            _manager.NetworkConfig.PlayerPrefab = null;
            _manager.NetworkConfig.TickRate = 30;
            bool registered = _manager.NetworkConfig.Prefabs.NetworkPrefabsLists
                .Any(list => list != null && list.PrefabList.Any(entry => entry.Prefab == _playerPrefab));
            if (!registered) _manager.AddNetworkPrefab(_playerPrefab);
            _manager.ConnectionApprovalCallback = ApproveServer;
            _manager.OnClientConnectedCallback += Connected;
            _manager.OnClientDisconnectCallback += Disconnected;
            _manager.OnClientStopped += Stopped;
            _manager.OnTransportFailure += TransportFailed;
            SetLocalMenu(true);
        }

        public bool StartSession(bool host, string address, string port, string playerName, int color, ConnectionProtocol protocol, int spell = 0)
        {
            if (Busy) return false;
            if (!ConnectionRules.TryEndpoint(address, port, out string ip, out ushort parsedPort))
            {
                Status = "Informe um IPv4 válido e uma porta entre 1 e 65535.";
                return false;
            }
            _guests.Clear();
            _levelReady = false;
            Protocol = protocol;
            Address = ip;
            Port = parsedPort;
            _transport.UseWebSockets = protocol == ConnectionProtocol.WebSocket;
            _transport.MaxConnectAttempts = 5;
            _transport.ConnectTimeoutMS = 1000;
            _transport.DisconnectTimeoutMS = 5000;
            _transport.SetConnectionData(true, Address, Port, host ? "0.0.0.0" : null);
            var guest = new Guest
            {
                version = ConnectionRules.Version,
                name = ConnectionRules.CleanName(playerName),
                color = color,
                spell = ConnectionRules.CleanSpell(spell, _catalog != null ? _catalog.Count : 0)
            };
            _manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(guest));
            Status = host ? "Abrindo sala…" : $"Conectando a {Address}:{Port}…";
            SetLocalMenu(true);
            bool started = host ? _manager.StartHost() : _manager.StartClient();
            if (!started) { Status = "Não foi possível iniciar a conexão. Confira a porta e tente novamente."; return false; }
            _manager.SceneManager.OnSceneEvent += SceneEvent;
            if (host) StartCoroutine(LoadLevelServer());
            Debug.Log($"[CoopSession] {(host ? "Host" : "Client")} {protocol} {Address}:{Port}; escuta={(host ? "0.0.0.0" : "—")}");
            return true;
        }

        private IEnumerator LoadLevelServer()
        {
            yield return null;
            var result = _manager.SceneManager.LoadScene(LevelScene, LoadSceneMode.Single);
            if (result != SceneEventProgressStatus.Started)
            {
                Status = $"Falha ao carregar a fase: {result}.";
                Leave();
            }
            else Status = "Carregando Level1…";
        }

        private void ApproveServer(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false;
            response.Pending = false;
            Guest guest = null;
            try
            {
                if (request.Payload != null && request.Payload.Length <= 512)
                    guest = JsonUtility.FromJson<Guest>(Encoding.UTF8.GetString(request.Payload));
            }
            catch (ArgumentException) { }
            string rejection = guest == null ? "Dados de conexão inválidos." : ConnectionRules.Rejection(_guests.Count, guest.version, guest.color);
            response.Approved = rejection == null;
            response.Reason = rejection ?? "";
            if (!response.Approved) { Debug.Log($"[CoopSession] Conexão recusada: {response.Reason}"); return; }
            guest.name = ConnectionRules.CleanName(guest.name);
            guest.spell = ConnectionRules.CleanSpell(guest.spell, _catalog != null ? _catalog.Count : 0);
            guest.slot = 0;
            foreach (var other in _guests.Values) if (other.slot == 0) guest.slot = 1;
            _guests[request.ClientNetworkId] = guest;
        }

        private void Connected(ulong id)
        {
            Status = _manager.IsHost ? "Sala aberta. Aguardando parceiro…" : "Conectado. Carregando fase…";
            if (_manager.IsServer && _levelReady) SpawnPlayerServer(id);
        }

        private void SceneEvent(SceneEvent sceneEvent)
        {
            if (!_manager.IsServer || sceneEvent.SceneName != LevelScene) return;
            if (sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                _levelReady = true;
                foreach (ulong id in _manager.ConnectedClientsIds) SpawnPlayerServer(id);
            }
            else if (_levelReady && sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete)
                SpawnPlayerServer(sceneEvent.ClientId);
        }

        private void SpawnPlayerServer(ulong id)
        {
            if (!_manager.ConnectedClients.TryGetValue(id, out var client) || client.PlayerObject != null || !_guests.TryGetValue(id, out var guest)) return;
            if (!PlayerSpawnPoint.TryGetSpawn(guest.slot, out Vector3 position, out Quaternion rotation))
            {
                Debug.LogError("[CoopSession] A fase não tem pontos de nascimento.");
                return;
            }
            var player = Instantiate(_playerPrefab, position, rotation);
            player.GetComponent<NetworkPlayerIdentity>().InitializeServer(guest.name, guest.color, guest.slot, guest.spell);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
            Debug.Log($"[CoopSession] Jogador {id} no ponto {guest.slot}: {position}.");
        }

        private void Disconnected(ulong id)
        {
            if (_manager.IsServer) { _guests.Remove(id); Status = "Parceiro desconectado. A sala continua aberta."; }
            else if (!string.IsNullOrWhiteSpace(_manager.DisconnectReason)) Status = _manager.DisconnectReason;
            else Status = "Conexão encerrada. Você pode criar ou entrar em outra sala.";
        }

        public void PlayerReady()
        {
            Status = "Conectado";
            SetLocalMenu(false);
        }

        public void SetLocalMenu(bool open)
        {
            LocalMenuOpen = open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            MenuChanged?.Invoke();
        }

        public void Leave()
        {
            if (!Busy || _returning) return;
            Status = "Você saiu da sala.";
            _manager.Shutdown();
        }

        private void Stopped(bool wasHost)
        {
            _levelReady = false;
            _guests.Clear();
            SetLocalMenu(true);
            if (!_quitting && !_returning) StartCoroutine(ReturnToMenu());
        }

        private IEnumerator ReturnToMenu()
        {
            _returning = true;
            while (_manager.ShutdownInProgress) yield return null;
            if (SceneManager.GetActiveScene().name != MenuScene) yield return SceneManager.LoadSceneAsync(MenuScene);
            _returning = false;
        }

        private void TransportFailed() { Status = "Falha de rede. Confira o endereço, a porta e o protocolo dos dois jogadores."; }
        private void OnApplicationQuit() => _quitting = true;

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (_manager == null) return;
            _manager.ConnectionApprovalCallback -= ApproveServer;
            _manager.OnClientConnectedCallback -= Connected;
            _manager.OnClientDisconnectCallback -= Disconnected;
            _manager.OnClientStopped -= Stopped;
            _manager.OnTransportFailure -= TransportFailed;
        }
    }
}
