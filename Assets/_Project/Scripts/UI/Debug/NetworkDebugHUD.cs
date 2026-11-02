using GameYunit.Network;
using UnityEngine;

// Namespace propositalmente sem ".Debug": um namespace com esse nome sombrearia UnityEngine.Debug.
namespace GameYunit.UI
{
    /// <summary>
    /// HUD mínimo de desenvolvimento (IMGUI) para iniciar Host/Client e ver o status da sessão.
    /// Visível apenas no Editor e em Development Builds.
    /// </summary>
    /// <remarks>
    /// Consome somente os eventos do <see cref="NetworkManagerSetup"/> (Observer), sem acessar o
    /// NetworkManager. A classe não é removida por #if para não gerar "Missing Script" em builds de release.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class NetworkDebugHUD : MonoBehaviour
    {
        [Tooltip("Se vazio, procura no mesmo GameObject e depois na cena.")]
        [SerializeField] private NetworkManagerSetup _networkSetup;

        [SerializeField] private Rect _area = new Rect(10f, 10f, 240f, 130f);

        // Atualizado apenas nos eventos, não a cada frame, para não gerar lixo no OnGUI.
        private string _status = "Offline";
        private string _clientButtonLabel = "Client";

        private void Awake()
        {
            if (_networkSetup == null && !TryGetComponent(out _networkSetup))
            {
                _networkSetup = FindFirstObjectByType<NetworkManagerSetup>();
            }

            if (_networkSetup == null)
            {
                Debug.LogWarning("[NetworkDebugHUD] NetworkManagerSetup não encontrado; HUD desativado.", this);
                enabled = false;
                return;
            }

            _clientButtonLabel = $"Client ({_networkSetup.ConnectAddress}:{_networkSetup.Port})";
        }

        private void OnEnable()
        {
            if (_networkSetup == null)
            {
                return;
            }

            _networkSetup.OnHostStarted += HandleHostStarted;
            _networkSetup.OnClientStarted += HandleClientStarted;
            _networkSetup.OnClientConnected += HandleClientConnected;
            _networkSetup.OnClientDisconnected += HandleClientDisconnected;
            _networkSetup.OnConnectionFailed += HandleConnectionFailed;
            _networkSetup.OnShutdown += HandleShutdown;
        }

        private void OnDisable()
        {
            if (_networkSetup == null)
            {
                return;
            }

            _networkSetup.OnHostStarted -= HandleHostStarted;
            _networkSetup.OnClientStarted -= HandleClientStarted;
            _networkSetup.OnClientConnected -= HandleClientConnected;
            _networkSetup.OnClientDisconnected -= HandleClientDisconnected;
            _networkSetup.OnConnectionFailed -= HandleConnectionFailed;
            _networkSetup.OnShutdown -= HandleShutdown;
        }

        private void OnGUI()
        {
            if (!Debug.isDebugBuild)
            {
                return;
            }

            GUILayout.BeginArea(_area, GUI.skin.box);

            if (!_networkSetup.IsRunning)
            {
                if (GUILayout.Button("Host"))
                {
                    _networkSetup.StartHost();
                }

                if (GUILayout.Button(_clientButtonLabel))
                {
                    _networkSetup.StartClient();
                }
            }
            else if (GUILayout.Button("Shutdown"))
            {
                _networkSetup.Shutdown();
            }

            GUILayout.Label(_status);
            GUILayout.EndArea();
        }

        private void HandleHostStarted() =>
            _status = $"Host em {_networkSetup.ListenAddress}:{_networkSetup.Port}";

        private void HandleClientStarted() =>
            _status = $"Conectando a {_networkSetup.ConnectAddress}:{_networkSetup.Port}...";

        private void HandleClientConnected(ulong clientId) => _status = $"Client {clientId} conectado";

        private void HandleClientDisconnected(ulong clientId) => _status = $"Client {clientId} desconectado";

        private void HandleConnectionFailed(string reason) => _status = reason;

        private void HandleShutdown() => _status = "Offline";
    }
}
