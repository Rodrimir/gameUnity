using System.Linq;
using GameYunit.Characters;
using GameYunit.Network;
using GameYunit.Settings;
using GameYunit.Spells;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static GameYunit.UI.MenuWidgets;

namespace GameYunit.UI
{
    /// <summary>
    /// Menu do cooperativo, em etapas: início, escolha de personagem e magia, e conexão.
    /// </summary>
    /// <remarks>
    /// A escolha tem tela própria em vez de dividir espaço com IP e porta: são decisões de natureza
    /// diferente, e o retrato grande com a descrição só cabe numa etapa dedicada. Tudo é construído em
    /// runtime, sem prefabs de UI, para a tela não depender de assets fora do Git.
    /// </remarks>
    public sealed class CoopMenu : MonoBehaviour
    {
        private enum Step { Start, Select, Connect }

        private CoopSession _session;
        private GameObject _panel, _stepStart, _stepSelect, _stepConnect, _pause, _hud;
        private TMP_InputField _name, _address, _port;
        private TMP_Text _status, _hudText, _cooldownText, _detailName, _detailBody, _fpsText;
        private Button _host, _join;
        private Image _udp, _webSocket, _blue, _orange;
        private Button[] _spellButtons, _characterButtons;
        private SettingsPanel _settings;
        private ConnectionProtocol _protocol;
        private Step _step = Step.Start;
        private int _color, _spell, _character;
        private ISpellCaster _localCaster;
        private float _nextHudRefresh, _fpsAccumulator;
        private int _fpsFrames;

        private void Start()
        {
            _session = GetComponent<CoopSession>();
            if (CoopSession.Instance != _session) return;
            Build();
            _session.MenuChanged += Refresh;
            Refresh();
        }

        private void Update()
        {
            if (_session == null || _panel == null) return;
            var manager = _session.Manager;
            bool hasPlayer = manager != null && manager.LocalClient?.PlayerObject != null;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) HandleEscape(hasPlayer);
            _panel.SetActive(!hasPlayer || _session.LocalMenuOpen);
            bool inMenu = !hasPlayer;
            _stepStart.SetActive(inMenu && _step == Step.Start);
            _stepSelect.SetActive(inMenu && _step == Step.Select);
            _stepConnect.SetActive(inMenu && _step == Step.Connect);
            _pause.SetActive(hasPlayer);
            _hud.SetActive(hasPlayer);
            _host.interactable = _join.interactable = !_session.Busy;
            _status.text = _session.Status;
            UpdateFps();
            if (hasPlayer && Time.unscaledTime >= _nextHudRefresh)
            {
                _nextHudRefresh = Time.unscaledTime + 0.25f;
                var identities = FindObjectsByType<NetworkPlayerIdentity>(FindObjectsSortMode.None);
                _hudText.text = $"{(manager.IsHost ? "HOST" : "CLIENTE")} · {(_session.Protocol == ConnectionProtocol.Udp ? "UDP" : "WebSocket")} · {identities.Length}/2\n" +
                    string.Join("  +  ", identities.Select(p => p.PlayerName.Value.ToString())) +
                    "\nWASD mover   ·   Espaço pular   ·   Clique conjurar   ·   Esc menu";
                _cooldownText.text = DescribeLocalSpell(manager);
            }
        }

        /// <summary>Esc fecha o que estiver por cima: configurações, depois etapa, depois menu da partida.</summary>
        private void HandleEscape(bool hasPlayer)
        {
            if (_settings != null && _settings.IsOpen) { _settings.Close(); return; }
            if (!hasPlayer)
            {
                if (_step == Step.Connect) GoTo(Step.Select);
                else if (_step == Step.Select) GoTo(Step.Start);
                return;
            }
            _session.SetLocalMenu(!_session.LocalMenuOpen);
        }

        private void GoTo(Step step)
        {
            _step = step;
            Refresh();
        }

        private void UpdateFps()
        {
            if (_fpsText == null) return;
            bool show = GameSettings.ShowFps;
            if (_fpsText.gameObject.activeSelf != show) _fpsText.gameObject.SetActive(show);
            if (!show) return;
            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccumulator < 0.5f) return;
            _fpsText.text = $"{_fpsFrames / _fpsAccumulator:0} fps";
            _fpsAccumulator = 0f;
            _fpsFrames = 0;
        }

        private void Build()
        {
            var canvas = new GameObject("Interface cooperativa", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 100;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);

            _panel = Box("Menu", canvas.transform, Vector2.zero, new Vector2(1920, 1080), Panel);
            Text("Marca", _panel.transform, "GAMEYUNIT  /  EXPEDIÇÃO", new Vector2(0, 470), new Vector2(1100, 54), 26, Accent);
            Text("Título", _panel.transform, "Explore em dupla", new Vector2(0, 404), new Vector2(1200, 86), 56, Color.white);

            BuildStart();
            BuildSelect();
            BuildConnect();
            BuildPause();

            _status = Text("Status", _panel.transform, "", new Vector2(0, -486), new Vector2(1400, 60), 24, Accent);

            _settings = gameObject.AddComponent<SettingsPanel>();
            _settings.Build(canvas.transform);

            _hud = Box("HUD", canvas.transform, new Vector2(-590, 448), new Vector2(690, 160), new Color(0.025f, 0.06f, 0.09f, 0.82f));
            _hudText = Text("Jogadores", _hud.transform, "", new Vector2(0, 20), new Vector2(650, 100), 20, Color.white,
                TextAlignmentOptions.MidlineLeft);
            _cooldownText = Text("Magia ativa", _hud.transform, "", new Vector2(0, -52), new Vector2(650, 34), 20, Accent,
                TextAlignmentOptions.MidlineLeft);

            _fpsText = Text("FPS", canvas.transform, "", new Vector2(860, 510), new Vector2(180, 40), 22, Accent,
                TextAlignmentOptions.MidlineRight);
            _fpsText.gameObject.SetActive(GameSettings.ShowFps);
        }

        private void BuildStart()
        {
            _stepStart = Panelled("Início", new Vector2(0, -40), new Vector2(760, 520));
            var root = _stepStart.transform;
            Button("Jogar", root, new Vector2(0, 150), new Vector2(560, 84), () => GoTo(Step.Select), 28);
            Button("Configurações", root, new Vector2(0, 50), new Vector2(560, 72), () => _settings.Open(), 24);
            Button("Sair do jogo", root, new Vector2(0, -42), new Vector2(560, 72), Quit, 24);
            Text("Rodapé", root, "Cooperativo para 2 jogadores · Host e convidado por IP direto",
                new Vector2(0, -150), new Vector2(760, 44), 20, Label);
        }

        private void BuildSelect()
        {
            _stepSelect = Panelled("Seleção", new Vector2(0, -30), new Vector2(1300, 700));
            var root = _stepSelect.transform;

            Text("Personagem rótulo", root, "ESCOLHA SEU PERSONAGEM", new Vector2(0, 300), new Vector2(1200, 36), 22, Accent);
            _characterButtons = BuildPicker(root, "Personagem", 228f,
                _session.Characters != null ? _session.Characters.Count : 0,
                i => _session.Characters[i]?.Portrait,
                i => _session.Characters[i]?.DisplayName,
                i => { _character = i; Refresh(); });

            Text("Magia rótulo", root, "ESCOLHA SUA MAGIA", new Vector2(0, 142), new Vector2(1200, 36), 22, Accent);
            _spellButtons = BuildPicker(root, "Magia", 70f,
                _session.Catalog != null ? _session.Catalog.Count : 0,
                i => _session.Catalog[i]?.Icon,
                i => _session.Catalog[i]?.DisplayName,
                i => { _spell = i; Refresh(); });

            var detail = Box("Detalhe", root, new Vector2(0, -92), new Vector2(1100, 170), new Color(1f, 1f, 1f, 0.04f));
            _detailName = Text("Nome", detail.transform, "", new Vector2(0, 54), new Vector2(1060, 48), 30, Color.white);
            _detailBody = Text("Corpo", detail.transform, "", new Vector2(0, -22), new Vector2(1060, 110), 20, Label);

            Button("Voltar", root, new Vector2(-290, -258), new Vector2(520, 72), () => GoTo(Step.Start), 24);
            Button("Confirmar", root, new Vector2(290, -258), new Vector2(520, 72), () => GoTo(Step.Connect), 24);
        }

        private void BuildConnect()
        {
            _stepConnect = Panelled("Conexão", new Vector2(0, -30), new Vector2(1200, 620));
            var root = _stepConnect.transform;

            _name = Field("Nome", "Seu nome", "Explorador", root, new Vector2(-300, 206), new Vector2(520, 62));
            _name.characterLimit = 24;
            _blue = Button("Azul", root, new Vector2(170, 206), new Vector2(190, 62),
                () => { _color = 0; Refresh(); }, 22).GetComponent<Image>();
            _orange = Button("Laranja", root, new Vector2(380, 206), new Vector2(190, 62),
                () => { _color = 1; Refresh(); }, 22).GetComponent<Image>();

            _address = Field("IP", "IP do Host", "127.0.0.1", root, new Vector2(-420, 100), new Vector2(260, 62));
            _port = Field("Porta", "Porta", "7777", root, new Vector2(-140, 100), new Vector2(180, 62));
            _port.contentType = TMP_InputField.ContentType.IntegerNumber;
            _udp = Button("UDP", root, new Vector2(170, 100), new Vector2(190, 62),
                () => { _protocol = ConnectionProtocol.Udp; Refresh(); }, 22).GetComponent<Image>();
            _webSocket = Button("WebSocket", root, new Vector2(380, 100), new Vector2(190, 62),
                () => { _protocol = ConnectionProtocol.WebSocket; Refresh(); }, 21).GetComponent<Image>();

            // Resumo da escolha feita na etapa anterior, localizado por nome em RefreshSummary.
            Text("Escolha", root, "", new Vector2(0, 20), new Vector2(1100, 36), 19, Accent).name = "Resumo";

            _host = Button("Criar sala", root, new Vector2(-300, -62), new Vector2(520, 76), () => Connect(true), 25);
            _join = Button("Entrar na sala", root, new Vector2(300, -62), new Vector2(520, 76), () => Connect(false), 25);
            Button("Voltar", root, new Vector2(-300, -148), new Vector2(520, 56), () => GoTo(Step.Select), 22);
            var cancel = Button("Cancelar conexão", root, new Vector2(300, -148), new Vector2(520, 56),
                () => _session.Leave(), 22);
            cancel.GetComponent<Image>().color = Muted;
            Text("Limite", root, "Até 2 jogadores · Mesmo protocolo nos dois computadores",
                new Vector2(0, -214), new Vector2(1100, 44), 20, Label);
        }

        /// <summary>Caixa de etapa, filha do painel, para todas compartilharem o mesmo enquadramento.</summary>
        private GameObject Panelled(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_panel.transform, false);
            Place((RectTransform)go.transform, position, size);
            return go;
        }

        /// <summary>
        /// Fileira de escolhas com retrato ou ícone. Sem arte importada, cai no nome, para o menu continuar
        /// utilizável antes de os pacotes da Asset Store chegarem.
        /// </summary>
        private Button[] BuildPicker(Transform root, string label, float y, int count,
            System.Func<int, Sprite> sprite, System.Func<int, string> name, System.Action<int> choose)
        {
            if (count <= 0)
            {
                Text(label + " vazio", root, $"Nenhum item no catálogo de {label.ToLowerInvariant()}.",
                    new Vector2(0, y), new Vector2(1100, 40), 19, new Color(0.9f, 0.6f, 0.35f));
                return new Button[0];
            }
            var buttons = new Button[count];
            const float size = 104f, gap = 16f;
            float total = count * size + (count - 1) * gap;
            float start = -total * 0.5f + size * 0.5f;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var go = Box($"{label} {i}", root, new Vector2(start + i * (size + gap), y), new Vector2(size, size), Muted);
                var button = go.AddComponent<Button>();
                button.targetGraphic = go.GetComponent<Image>();
                button.onClick.AddListener(() => choose(index));
                var art = sprite(i);
                if (art != null)
                {
                    var iconGo = new GameObject("Arte", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(go.transform, false);
                    Place((RectTransform)iconGo.transform, Vector2.zero, new Vector2(size - 14, size - 14));
                    var image = iconGo.GetComponent<Image>();
                    image.sprite = art;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
                else
                {
                    Text("Nome", go.transform, name(i) ?? "?", Vector2.zero, new Vector2(size - 10, size - 10), 17, Color.white);
                }
                buttons[i] = button;
            }
            return buttons;
        }

        private void BuildPause()
        {
            _pause = Panelled("Menu local", Vector2.zero, new Vector2(1100, 460));
            Text("Aviso", _pause.transform, "A partida continua enquanto este menu está aberto.",
                new Vector2(0, 150), new Vector2(1100, 60), 26, Color.white);
            Button("Continuar", _pause.transform, new Vector2(0, 50), new Vector2(600, 74),
                () => _session.SetLocalMenu(false), 24);
            Button("Configurações", _pause.transform, new Vector2(0, -40), new Vector2(600, 74),
                () => _settings.Open(), 24);
            Button("Sair da sala", _pause.transform, new Vector2(0, -130), new Vector2(600, 74),
                () => _session.Leave(), 24);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Connect(bool host) =>
            _session.StartSession(host, _address.text, _port.text, _name.text, _color, _protocol, _spell, _character);

        private void Refresh()
        {
            if (_udp == null) return;
            _udp.color = _protocol == ConnectionProtocol.Udp ? Accent : Muted;
            _webSocket.color = _protocol == ConnectionProtocol.WebSocket ? Accent : Muted;
            _blue.color = _color == 0 ? Accent : Muted;
            _orange.color = _color == 1 ? Accent : Muted;
            Highlight(_spellButtons, _spell);
            Highlight(_characterButtons, _character);
            RefreshDetail();
            RefreshSummary();
        }

        private static void Highlight(Button[] buttons, int selected)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null)
                    buttons[i].GetComponent<Image>().color = i == selected ? Accent : Muted;
        }

        /// <summary>Retrato grande da etapa de seleção: personagem e magia escolhidos, com os números.</summary>
        private void RefreshDetail()
        {
            if (_detailName == null) return;
            var characters = _session.Characters;
            var spells = _session.Catalog;
            var character = characters != null && characters.Count > 0
                ? characters[Mathf.Clamp(_character, 0, characters.Count - 1)] : null;
            var spell = spells != null && spells.Count > 0
                ? spells[Mathf.Clamp(_spell, 0, spells.Count - 1)] : null;

            _detailName.text = character != null ? character.DisplayName : "Sem personagem";
            if (spell == null)
            {
                _detailBody.text = character != null ? character.Description : "Prepare os catálogos no Editor.";
                return;
            }
            string area = spell.Shape == SpellShape.Sphere
                ? $"esfera de {spell.Radius:0.#} m a {spell.Range:0.#} m"
                : $"cone de {spell.ConeAngle:0}° e {spell.Range:0.#} m";
            string alvos = spell.MaxTargets > 0 ? $"até {spell.MaxTargets} alvos" : "sem limite de alvos";
            _detailBody.text =
                (string.IsNullOrWhiteSpace(character?.Description) ? "" : character.Description + "\n") +
                $"<b>{spell.DisplayName}</b> · {area} · {alvos} · recarga {spell.Cooldown:0.#}s" +
                (string.IsNullOrWhiteSpace(spell.Description) ? "" : $"\n{spell.Description}");
        }

        /// <summary>Linha de confirmação na etapa de conexão, para a escolha não sumir de vista.</summary>
        private void RefreshSummary()
        {
            var summary = _stepConnect != null ? _stepConnect.transform.Find("Resumo") : null;
            if (summary == null) return;
            var characters = _session.Characters;
            var spells = _session.Catalog;
            string who = characters != null && characters.Count > 0
                ? characters[Mathf.Clamp(_character, 0, characters.Count - 1)]?.DisplayName : null;
            string what = spells != null && spells.Count > 0
                ? spells[Mathf.Clamp(_spell, 0, spells.Count - 1)]?.DisplayName : null;
            summary.GetComponent<TMP_Text>().text = who == null && what == null
                ? ""
                : $"{who ?? "—"}  ·  {what ?? "sem magia"}";
        }

        private string DescribeLocalSpell(NetworkManager manager)
        {
            if (_localCaster == null)
            {
                var player = manager.LocalClient?.PlayerObject;
                if (player != null) _localCaster = player.GetComponent<ISpellCaster>();
            }
            var spell = _localCaster != null ? _localCaster.CurrentSpell : null;
            if (spell == null) return "Sem magia equipada";
            float progress = _localCaster.CooldownProgress;
            if (progress >= 1f) return $"{spell.DisplayName} · pronta (clique esquerdo)";
            int filled = Mathf.RoundToInt(progress * 10f);
            return $"{spell.DisplayName} · [{new string('|', filled)}{new string('.', 10 - filled)}]";
        }

        private TMP_InputField Field(string name, string placeholder, string value, Transform parent,
            Vector2 position, Vector2 size)
        {
            Text(name + " rótulo", parent, placeholder, position + Vector2.up * 46, new Vector2(size.x, 28), 18, Label);
            var go = Box(name, parent, position, size, Muted);
            var field = go.AddComponent<TMP_InputField>();
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(go.transform, false);
            Place((RectTransform)viewport.transform, Vector2.zero, size - new Vector2(25, 8));
            field.textViewport = (RectTransform)viewport.transform;
            field.textComponent = Text("Valor", viewport.transform, "", Vector2.zero, size - new Vector2(30, 8), 24,
                Color.white, TextAlignmentOptions.MidlineLeft);
            field.text = value;
            field.targetGraphic = go.GetComponent<Image>();
            return field;
        }

        private void OnDestroy()
        {
            if (_session != null) _session.MenuChanged -= Refresh;
        }
    }
}
