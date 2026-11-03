using System.Linq;
using GameYunit.Network;
using GameYunit.Spells;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameYunit.UI
{
    public sealed class CoopMenu : MonoBehaviour
    {
        private CoopSession _session;
        private GameObject _panel, _connection, _pause, _hud;
        private TMP_InputField _name, _address, _port;
        private TMP_Text _status, _hudText;
        private Button _host, _join, _udp, _webSocket, _blue, _orange;
        private ConnectionProtocol _protocol;
        private int _color;
        private int _spell;
        private Button[] _spellButtons;
        private TMP_Text _spellInfo, _cooldownText;
        private ISpellCaster _localCaster;
        private float _nextHudRefresh;
        private readonly Color _accent = new(0.25f, 0.77f, 0.85f);
        private readonly Color _muted = new(0.13f, 0.20f, 0.25f);

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
            if (hasPlayer && UnityEngine.Input.GetKeyDown(KeyCode.Escape)) _session.SetLocalMenu(!_session.LocalMenuOpen);
            _panel.SetActive(!hasPlayer || _session.LocalMenuOpen);
            _connection.SetActive(!hasPlayer);
            _pause.SetActive(hasPlayer);
            _hud.SetActive(hasPlayer);
            _host.interactable = _join.interactable = !_session.Busy;
            _status.text = _session.Status;
            if (hasPlayer && Time.unscaledTime >= _nextHudRefresh)
            {
                _nextHudRefresh = Time.unscaledTime + 0.25f;
                var identities = FindObjectsByType<NetworkPlayerIdentity>(FindObjectsSortMode.None);
                _hudText.text = $"{(manager.IsHost ? "HOST" : "CLIENTE")} · {(_session.Protocol == ConnectionProtocol.Udp ? "UDP" : "WebSocket")} · {identities.Length}/2\n" +
                    string.Join("  +  ", identities.Select(p => p.PlayerName.Value.ToString())) + "\nWASD mover   ·   Espaço pular   ·   Mouse câmera   ·   Esc menu";
                _cooldownText.text = DescribeLocalSpell(manager);
            }
        }

        private void Build()
        {
            var canvas = new GameObject("Interface cooperativa", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 100;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);

            _panel = Box("Menu", canvas.transform, Vector2.zero, new Vector2(1920, 1080), new Color(0.025f, 0.06f, 0.09f, 0.95f));
            Text("Marca", _panel.transform, "GAMEYUNIT  /  EXPEDIÇÃO", new Vector2(0, 390), new Vector2(1100, 60), 28, _accent);
            Text("Título", _panel.transform, "Explore em dupla", new Vector2(0, 305), new Vector2(1200, 100), 66, Color.white);
            _connection = new GameObject("Conexão", typeof(RectTransform));
            _connection.transform.SetParent(_panel.transform, false);
            Place((RectTransform)_connection.transform, Vector2.zero, new Vector2(1100, 550));
            Text("Ajuda", _connection.transform, "Crie uma sala e compartilhe seu IP com o parceiro.", new Vector2(0, 225), new Vector2(1100, 50), 26, new Color(0.7f, 0.8f, 0.84f));
            _name = Field("Nome", "Seu nome", "Explorador", _connection.transform, new Vector2(-270, 120), new Vector2(500, 65));
            _name.characterLimit = 24;
            _address = Field("IP", "IP do Host", "127.0.0.1", _connection.transform, new Vector2(-390, 15), new Vector2(260, 65));
            _port = Field("Porta", "Porta", "7777", _connection.transform, new Vector2(-110, 15), new Vector2(180, 65));
            _port.contentType = TMP_InputField.ContentType.IntegerNumber;
            _blue = Button("Azul", "Azul", _connection.transform, new Vector2(170, 120), new Vector2(190, 65), () => { _color = 0; Refresh(); });
            _orange = Button("Laranja", "Laranja", _connection.transform, new Vector2(380, 120), new Vector2(190, 65), () => { _color = 1; Refresh(); });
            _udp = Button("UDP", "UDP", _connection.transform, new Vector2(170, 15), new Vector2(190, 65), () => { _protocol = ConnectionProtocol.Udp; Refresh(); });
            _webSocket = Button("WebSocket", "WebSocket", _connection.transform, new Vector2(380, 15), new Vector2(190, 65), () => { _protocol = ConnectionProtocol.WebSocket; Refresh(); });
            _host = Button("Criar sala", "Criar sala", _connection.transform, new Vector2(-270, -110), new Vector2(500, 80), () => Connect(true));
            _join = Button("Entrar", "Entrar na sala", _connection.transform, new Vector2(270, -110), new Vector2(500, 80), () => Connect(false));
            Text("Limite", _connection.transform, "Até 2 jogadores · Mesmo protocolo nos dois computadores", new Vector2(0, -205), new Vector2(1100, 50), 23, new Color(0.65f, 0.75f, 0.8f));
            BuildSpellRow();

            _pause = new GameObject("Menu local", typeof(RectTransform));
            _pause.transform.SetParent(_panel.transform, false);
            Place((RectTransform)_pause.transform, Vector2.zero, new Vector2(1100, 400));
            Text("Aviso", _pause.transform, "A partida continua enquanto este menu está aberto.", new Vector2(0, 120), new Vector2(1100, 60), 28, Color.white);
            Button("Continuar", "Continuar", _pause.transform, new Vector2(0, 10), new Vector2(600, 80), () => _session.SetLocalMenu(false));
            Button("Sair", "Sair da sala", _pause.transform, new Vector2(0, -100), new Vector2(600, 80), () => _session.Leave());
            _status = Text("Status", _panel.transform, "", new Vector2(0, -340), new Vector2(1400, 80), 26, _accent);
            var cancel = Button("Cancelar conexão", "Cancelar conexão", _connection.transform, new Vector2(0, -280), new Vector2(450, 50), () => _session.Leave());
            cancel.GetComponent<Image>().color = _muted;
            _hud = Box("HUD", canvas.transform, new Vector2(-590, 450), new Vector2(690, 140), new Color(0.025f, 0.06f, 0.09f, 0.82f));
            _hudText = Text("Jogadores", _hud.transform, "", new Vector2(0, 12), new Vector2(650, 100), 20, Color.white);
            _hudText.alignment = TextAlignmentOptions.MidlineLeft;
            _cooldownText = Text("Magia ativa", _hud.transform, "", new Vector2(0, -48), new Vector2(650, 34), 20, _accent);
            _cooldownText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        /// <summary>
        /// Fileira de magias do menu. Cada opção mostra o ícone do pacote de arte; sem ícone, cai no nome,
        /// para o menu continuar utilizável antes de a arte ser importada.
        /// </summary>
        private void BuildSpellRow()
        {
            var catalog = _session.Catalog;
            Text("Magia rótulo", _connection.transform, "Escolha sua magia", new Vector2(0, -258), new Vector2(1100, 34), 21, new Color(0.65f, 0.75f, 0.8f));
            if (catalog == null || catalog.Count == 0)
            {
                _spellInfo = Text("Magia vazia", _connection.transform, "Nenhuma magia configurada no catálogo.", new Vector2(0, -300), new Vector2(1100, 40), 20, new Color(0.9f, 0.6f, 0.35f));
                _spellButtons = new Button[0];
                return;
            }
            _spellButtons = new Button[catalog.Count];
            const float size = 86f, gap = 14f;
            float total = catalog.Count * size + (catalog.Count - 1) * gap;
            float start = -total * 0.5f + size * 0.5f;
            for (int i = 0; i < catalog.Count; i++)
            {
                int index = i;
                var spell = catalog[i];
                var go = Box($"Magia {i}", _connection.transform, new Vector2(start + i * (size + gap), -308), new Vector2(size, size), _muted);
                var button = go.AddComponent<Button>();
                button.targetGraphic = go.GetComponent<Image>();
                button.onClick.AddListener(() => { _spell = index; Refresh(); });
                if (spell != null && spell.Icon != null)
                {
                    var iconGo = new GameObject("Ícone", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(go.transform, false);
                    Place((RectTransform)iconGo.transform, Vector2.zero, new Vector2(size - 14, size - 14));
                    var image = iconGo.GetComponent<Image>();
                    image.sprite = spell.Icon;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
                else
                {
                    Text("Nome", go.transform, spell != null ? spell.DisplayName : "?", Vector2.zero, new Vector2(size - 8, size - 8), 17, Color.white);
                }
                _spellButtons[i] = button;
            }
            _spellInfo = Text("Magia info", _connection.transform, "", new Vector2(0, -372), new Vector2(1100, 60), 20, new Color(0.7f, 0.8f, 0.84f));
        }

        /// <summary>Resume a magia em uma linha, para a escolha ser informada e não só visual.</summary>
        private void RefreshSpellInfo()
        {
            var catalog = _session.Catalog;
            if (_spellInfo == null || catalog == null || catalog.Count == 0) return;
            var spell = catalog[_spell];
            if (spell == null) { _spellInfo.text = ""; return; }
            string area = spell.Shape == SpellShape.Sphere
                ? $"esfera de {spell.Radius:0.#} m a {spell.Range:0.#} m"
                : $"cone de {spell.ConeAngle:0}° e {spell.Range:0.#} m";
            _spellInfo.text = $"<b>{spell.DisplayName}</b> · {area} · recarga {spell.Cooldown:0.#}s" +
                (string.IsNullOrWhiteSpace(spell.Description) ? "" : $"\n{spell.Description}");
        }

        /// <summary>Magia do jogador local e estado da recarga, para o clique ter resposta visível.</summary>
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

        private void Connect(bool host) => _session.StartSession(host, _address.text, _port.text, _name.text, _color, _protocol, _spell);
        private void Refresh()
        {
            if (_udp == null) return;
            _udp.GetComponent<Image>().color = _protocol == ConnectionProtocol.Udp ? _accent : _muted;
            _webSocket.GetComponent<Image>().color = _protocol == ConnectionProtocol.WebSocket ? _accent : _muted;
            _blue.GetComponent<Image>().color = _color == 0 ? _accent : _muted;
            _orange.GetComponent<Image>().color = _color == 1 ? _accent : _muted;
            if (_spellButtons != null)
                for (int i = 0; i < _spellButtons.Length; i++)
                    if (_spellButtons[i] != null)
                        _spellButtons[i].GetComponent<Image>().color = i == _spell ? _accent : _muted;
            RefreshSpellInfo();
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static GameObject Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static TMP_Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private Button Button(string name, string label, Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = Box(name, parent, position, size, _muted);
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(action);
            Text("Texto", go.transform, label, Vector2.zero, size - new Vector2(15, 5), 26, Color.white);
            return button;
        }

        private TMP_InputField Field(string name, string placeholder, string value, Transform parent, Vector2 position, Vector2 size)
        {
            Text(name + " rótulo", parent, placeholder, position + Vector2.up * 48, new Vector2(size.x, 30), 19, new Color(0.65f, 0.75f, 0.8f));
            var go = Box(name, parent, position, size, _muted);
            var field = go.AddComponent<TMP_InputField>();
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(go.transform, false);
            Place((RectTransform)viewport.transform, Vector2.zero, size - new Vector2(25, 8));
            field.textViewport = (RectTransform)viewport.transform;
            field.textComponent = Text("Valor", viewport.transform, "", Vector2.zero, size - new Vector2(30, 8), 26, Color.white);
            field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
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
