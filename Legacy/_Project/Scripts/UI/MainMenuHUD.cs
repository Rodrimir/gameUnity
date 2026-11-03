using GameYunit.Characters;
using GameYunit.Combat;
using GameYunit.Network;
using GameYunit.Progression;
using GameYunit.Rendering;
using GameYunit.Gameplay;
using GameYunit.Player;
using GameYunit.Core.Rendering;
using UnityEngine;

namespace GameYunit.UI
{
    /// <summary>Menu funcional de Coop, seleção inicial e vídeo sobre a arena atual.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuHUD : MonoBehaviour
    {
        private static readonly Vector2Int[] PsxModes =
        {
            new Vector2Int(320, 240), new Vector2Int(426, 240), new Vector2Int(640, 360)
        };

        [SerializeField] private NetworkManagerSetup _network;
        [SerializeField] private PlayerAppearance _playerCatalog;
        [SerializeField] private AutoAttackSystem _weaponCatalog;
        [SerializeField] private PsxRenderTextureSetup _psx;

        private enum Page { Coop, Video }
        private Page _page;
        private string _address = "127.0.0.1";
        private string _status = "Escolha personagem e arma; depois crie ou entre em uma sala.";
        private int _character;
        private int _weapon;
        private int _psxMode;
        private int _resolutionIndex;
        private bool _fullscreen;
        private bool _vSync = true;
        private bool _snapping = true;
        private float _affine = 1f;
        private bool _upgradeChosen;
        private bool _pauseVideo;
        private int _lastPauseMode;

        private void Awake()
        {
            if (_network == null) _network = FindFirstObjectByType<NetworkManagerSetup>();
            if (_playerCatalog == null) _playerCatalog = FindFirstObjectByType<PlayerAppearance>();
            if (_weaponCatalog == null) _weaponCatalog = FindFirstObjectByType<AutoAttackSystem>();
            if (_psx == null) _psx = FindFirstObjectByType<PsxRenderTextureSetup>();
            _character = Mathf.Clamp(PlayerAppearance.PreferredCharacterIndex, 0,
                Mathf.Max(0, (_playerCatalog != null ? _playerCatalog.CharacterCount : 1) - 1));
            _weapon = Mathf.Clamp(AutoAttackSystem.PreferredStartingWeaponIndex, 0,
                Mathf.Max(0, (_weaponCatalog != null ? _weaponCatalog.Weapons.Count : 1) - 1));
            _fullscreen = Screen.fullScreen;
            _vSync = QualitySettings.vSyncCount > 0;
            if (_psx != null)
            {
                _snapping = _psx.VertexSnapping;
                _affine = _psx.AffineStrength;
            }
            CacheCurrentResolution();
        }

        private void OnEnable()
        {
            GameSessionController.OnPauseChanged += HandlePauseChanged;
            if (_network == null) return;
            _network.OnHostStarted += HandleHostStarted;
            _network.OnClientStarted += HandleClientStarted;
            _network.OnClientConnected += HandleClientConnected;
            _network.OnConnectionFailed += HandleConnectionFailed;
            _network.OnShutdown += HandleShutdown;
        }

        private void OnDisable()
        {
            GameSessionController.OnPauseChanged -= HandlePauseChanged;
            if (_network == null) return;
            _network.OnHostStarted -= HandleHostStarted;
            _network.OnClientStarted -= HandleClientStarted;
            _network.OnClientConnected -= HandleClientConnected;
            _network.OnConnectionFailed -= HandleConnectionFailed;
            _network.OnShutdown -= HandleShutdown;
        }

        private void OnGUI()
        {
            if (_network == null) return;
            if (_network.IsRunning && PlayerNetwork.LocalPlayer != null && PlayerNetwork.LocalPlayer.IsSpawned)
            {
                DrawGameplayUI();
                return;
            }
            const float panelWidth = 440f;
            const float panelHeight = 470f;
            float scale = Mathf.Min((Screen.width - 24f) / panelWidth, (Screen.height - 16f) / panelHeight);
#if UNITY_EDITOR
            // O Game View pode mostrar a RenderTexture PSX ampliada, mas Screen.height continua
            // refletindo a janela do Editor. Use a resolução interna para manter a prévia inteira.
            if (_psx != null)
                scale = Mathf.Min(scale, (_psx.InternalResolution.y - 16f) / panelHeight);
#endif
            scale = Mathf.Clamp(scale, 0.25f, 1f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            var area = new Rect(logicalWidth * 0.5f - panelWidth * 0.5f,
                logicalHeight * 0.5f - panelHeight * 0.5f, panelWidth, panelHeight);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("GAMEYUNIT  /  COOP LOCAL", HeaderStyle());
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_page == Page.Coop, "PARTIDA", GUI.skin.button)) _page = Page.Coop;
            if (GUILayout.Toggle(_page == Page.Video, "VÍDEO", GUI.skin.button)) _page = Page.Video;
            GUILayout.EndHorizontal();

            if (_network.IsRunning)
            {
                GUILayout.Space(20f);
                GUILayout.Label(_status);
                GUILayout.Label("Sala para 2 jogadores  ·  UDP 7777");
                if (GUILayout.Button("ENCERRAR SESSÃO", GUILayout.Height(38f))) _network.Shutdown();
            }
            else if (_page == Page.Coop)
            {
                DrawCoopPage();
            }
            else
            {
                DrawVideoPage();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(_status, GUI.skin.box);
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void DrawGameplayUI()
        {
            PlayerNetwork localPlayer = PlayerNetwork.LocalPlayer;
            HealthComponent health = localPlayer.GetComponent<HealthComponent>();
            PlayerLevel level = localPlayer.GetComponent<PlayerLevel>();
            GameSessionController session = GameSessionController.Instance;
            Camera camera = Camera.main;
            Rect viewport = GetViewportGuiRect(camera);
            float uiScale = _psx != null && _psx.RenderTexture != null
                ? Mathf.Min(viewport.width / _psx.InternalResolution.x, viewport.height / _psx.InternalResolution.y)
                : 1f;
            uiScale = Mathf.Max(0.25f, uiScale);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = previous * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(uiScale, uiScale, 1f));
            GUILayout.BeginArea(new Rect(viewport.x / uiScale + 12f, viewport.y / uiScale + 12f, 270, 92), GUI.skin.box);
            GUILayout.Label($"VIDA  {(health != null ? health.Health : 0):0}/{(health != null ? health.MaxHealth : 0):0}");
            GUILayout.Label($"NÍVEL  {(level != null ? level.Level : 1)}     XP  {(level != null ? level.Xp : 0)}/{(level != null ? level.XpToNextLevel : 0)}");
            float xp = level != null && level.XpToNextLevel > 0 ? (float)level.Xp / level.XpToNextLevel : 0f;
            Rect xpRect = GUILayoutUtility.GetRect(240, 16);
            GUI.Box(xpRect, GUIContent.none);
            GUI.color = new Color(.15f,.8f,1f);
            GUI.DrawTexture(new Rect(xpRect.x + 2, xpRect.y + 2, Mathf.Max(0, (xpRect.width - 4) * xp), xpRect.height - 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.EndArea();

            DrawWorldHealthBars(camera, uiScale);
            GUI.matrix = previous;
            if (session == null || session.PauseMode == 0) return;
            DrawPausePanel(session);
        }

        private Rect GetViewportGuiRect(Camera camera)
        {
            if (!CameraViewportMapping.TryGetScreenRect(camera, out Rect screenRect))
                screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
            return new Rect(screenRect.x, Screen.height - screenRect.yMax, screenRect.width, screenRect.height);
        }

        private void DrawWorldHealthBars(Camera camera, float uiScale)
        {
            if (camera == null) return;
            foreach (HealthComponent item in HealthComponent.Active)
            {
                if (item == null || !item.IsSpawned || item.MaxHealth <= 0f || !item.IsAlive) continue;
                Vector3 cameraPoint = camera.WorldToScreenPoint(item.transform.position + Vector3.up * (item.IsPlayer ? 2.3f : 2f));
                if (cameraPoint.z <= 0f || !CameraViewportMapping.TryCameraPixelToScreen(camera,
                    new Vector2(cameraPoint.x, cameraPoint.y), out Vector2 screenPoint)) continue;
                float ratio = Mathf.Clamp01(item.Health / item.MaxHealth);
                Rect bar = new Rect(screenPoint.x / uiScale - 27f, (Screen.height - screenPoint.y) / uiScale, 54f, 7f);
                GUI.color = Color.black;
                GUI.Box(bar, GUIContent.none);
                GUI.color = item.IsPlayer ? new Color(.18f,.9f,.35f) : new Color(.9f,.18f,.14f);
                GUI.DrawTexture(new Rect(bar.x + 1, bar.y + 1, Mathf.Max(0f, (bar.width - 2) * ratio), bar.height - 2), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        private void DrawPausePanel(GameSessionController session)
        {
            const float width = 440f;
            const float height = 390f;
            float scale = Mathf.Min((Screen.width - 24f) / width, (Screen.height - 16f) / height);
#if UNITY_EDITOR
            if (_psx != null) scale = Mathf.Min(scale, (_psx.InternalResolution.y - 16f) / height);
#endif
            scale = Mathf.Clamp(scale, .25f, 1f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;
            GUILayout.BeginArea(new Rect(w*.5f-width*.5f,h*.5f-height*.5f,width,height), GUI.skin.box);
            if (session.PauseMode == 2)
            {
                GUILayout.Label("MARCO DE 30 ABATES", HeaderStyle());
                if (_upgradeChosen) GUILayout.Label("Aguardando o outro jogador escolher...");
                else
                {
                    GUILayout.Label("Escolha uma melhoria para seu personagem:");
                    if (GUILayout.Button("+5% de dano", GUILayout.Height(42))) ChooseUpgrade(session, 0);
                    if (GUILayout.Button("+5% de vida máxima", GUILayout.Height(42))) ChooseUpgrade(session, 1);
                    if (GUILayout.Button("Curar toda a vida", GUILayout.Height(42))) ChooseUpgrade(session, 2);
                }
            }
            else if (_pauseVideo)
            {
                GUILayout.Label("VÍDEO", HeaderStyle());
                DrawVideoPage();
                if (GUILayout.Button("VOLTAR À PAUSA")) _pauseVideo = false;
            }
            else
            {
                GUILayout.Label("JOGO PAUSADO", HeaderStyle());
                if (GUILayout.Button("VOLTAR AO JOGO", GUILayout.Height(44))) session.TogglePause();
                if (GUILayout.Button("VÍDEO", GUILayout.Height(38))) _pauseVideo = true;
                _fullscreen = GUILayout.Toggle(_fullscreen, "Tela cheia");
                if (GUILayout.Button("APLICAR TELA CHEIA")) ApplyFullscreen();
            }
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private void ChooseUpgrade(GameSessionController session, int option)
        {
            session.ChooseUpgrade(option);
            _upgradeChosen = true;
        }

        private void HandlePauseChanged(int mode, int kills)
        {
            if (mode == 2 && _lastPauseMode != 2) _upgradeChosen = false;
            if (mode == 0) _pauseVideo = false;
            _lastPauseMode = mode;
        }

        private void ApplyFullscreen()
        {
            Resolution resolution = Screen.currentResolution;
            Screen.SetResolution(resolution.width, resolution.height, _fullscreen);
        }

        private void DrawCoopPage()
        {
            GUILayout.Label("PREPARAÇÃO  ·  MAPA 01: VALE MEDIEVAL");
            DrawCharacterChoice();
            DrawWeaponChoice();
            GUILayout.Space(8f);
            if (GUILayout.Button("CRIAR SALA COOP", GUILayout.Height(42f)))
            {
                _network.ConfigureConnection("127.0.0.1", 7777, true);
                _status = _network.StartHost() ? "Criando sala local..." : "Não foi possível iniciar o Host.";
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP do Host", GUILayout.Width(75f));
            _address = GUILayout.TextField(_address);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("ENTRAR NA SALA", GUILayout.Height(38f)))
            {
                _network.ConfigureConnection(_address, 7777, false);
                _status = _network.StartClient() ? "Conectando ao Host..." : "Não foi possível iniciar o Client.";
            }
            if (GUILayout.Button("SAIR", GUILayout.Height(30f))) Application.Quit();
        }

        private void DrawCharacterChoice()
        {
            if (_playerCatalog == null || _playerCatalog.CharacterCount == 0) return;
            GUILayout.Label("PERSONAGEM");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(45f))) ChangeCharacter(-1);
            GUILayout.Label(_playerCatalog.GetCharacterName(_character), GUI.skin.box, GUILayout.Height(32f));
            if (GUILayout.Button("▶", GUILayout.Width(45f))) ChangeCharacter(1);
            GUILayout.EndHorizontal();
        }

        private void DrawWeaponChoice()
        {
            if (_weaponCatalog == null || _weaponCatalog.Weapons.Count == 0) return;
            GUILayout.Label("ARMA INICIAL");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(45f))) ChangeWeapon(-1);
            WeaponDefinition selected = _weaponCatalog.Weapons[_weapon];
            GUILayout.Label(selected != null ? selected.DisplayName : "Arma", GUI.skin.box, GUILayout.Height(32f));
            if (GUILayout.Button("▶", GUILayout.Width(45f))) ChangeWeapon(1);
            GUILayout.EndHorizontal();
            if (selected != null) GUILayout.Label(selected.Description, GUILayout.MinHeight(28f));
        }

        private void DrawVideoPage()
        {
            GUILayout.Label("RESOLUÇÃO DE SAÍDA");
            Resolution[] modes = Screen.resolutions;
            string[] labels = new string[modes.Length];
            for (int i = 0; i < modes.Length; i++) labels[i] = $"{modes[i].width} × {modes[i].height}";
            if (labels.Length > 0)
            {
                _resolutionIndex = Mathf.Clamp(_resolutionIndex, 0, labels.Length - 1);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◀", GUILayout.Width(45f)))
                    _resolutionIndex = (_resolutionIndex + labels.Length - 1) % labels.Length;
                GUILayout.Label(labels[_resolutionIndex], GUI.skin.box, GUILayout.Height(32f));
                if (GUILayout.Button("▶", GUILayout.Width(45f)))
                    _resolutionIndex = (_resolutionIndex + 1) % labels.Length;
                GUILayout.EndHorizontal();
            }
            _fullscreen = GUILayout.Toggle(_fullscreen, "Tela cheia");
            _vSync = GUILayout.Toggle(_vSync, "VSync");
            if (GUILayout.Button("APLICAR VÍDEO")) ApplyOutputVideo(modes);
            GUILayout.Space(8f);
            GUILayout.Label("RESOLUÇÃO INTERNA PSX");
            string[] psxLabels = { "320 × 240", "426 × 240", "640 × 360" };
            _psxMode = GUILayout.SelectionGrid(_psxMode, psxLabels, 3);
            _snapping = GUILayout.Toggle(_snapping, "Vertex snapping");
            GUILayout.Label($"Textura afim  {_affine:0.0}");
            _affine = GUILayout.HorizontalSlider(_affine, 0f, 1f);
            if (GUILayout.Button("APLICAR PSX") && _psx != null)
                _psx.ConfigureRuntime(PsxModes[_psxMode], _snapping, _affine);
        }

        private void ApplyOutputVideo(Resolution[] modes)
        {
            if (modes.Length == 0) return;
            _resolutionIndex = Mathf.Clamp(_resolutionIndex, 0, modes.Length - 1);
            Resolution selected = modes[_resolutionIndex];
            Screen.SetResolution(selected.width, selected.height, _fullscreen);
            QualitySettings.vSyncCount = _vSync ? 1 : 0;
            _status = $"Vídeo aplicado: {selected.width} × {selected.height}.";
        }

        private void ChangeCharacter(int delta)
        {
            int count = _playerCatalog.CharacterCount;
            _character = (_character + delta + count) % count;
            PlayerAppearance.PreferredCharacterIndex = _character;
            _status = "Personagem inicial selecionado.";
        }

        private void ChangeWeapon(int delta)
        {
            int count = _weaponCatalog.Weapons.Count;
            _weapon = (_weapon + delta + count) % count;
            AutoAttackSystem.PreferredStartingWeaponIndex = _weapon;
            _status = "Arma inicial selecionada.";
        }

        private void CacheCurrentResolution()
        {
            Resolution[] modes = Screen.resolutions;
            for (int i = 0; i < modes.Length; i++)
                if (modes[i].width == Screen.width && modes[i].height == Screen.height) _resolutionIndex = i;
            for (int i = 0; i < PsxModes.Length; i++)
                if (_psx != null && _psx.InternalResolution == PsxModes[i]) _psxMode = i;
        }

        private void HandleHostStarted() => _status = "Sala criada. Aguardando outro jogador.";
        private void HandleClientStarted() => _status = "Conectando ao Host...";
        private void HandleClientConnected(ulong id) => _status = id == 0 ? "Conectado como Host." : $"Jogador {id} conectado.";
        private void HandleConnectionFailed(string reason) => _status = reason;
        private void HandleShutdown() => _status = "Sessão encerrada.";

        private static GUIStyle HeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 22, fontStyle = FontStyle.Bold };
            return style;
        }
    }
}
