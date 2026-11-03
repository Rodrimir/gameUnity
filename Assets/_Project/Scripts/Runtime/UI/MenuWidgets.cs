using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameYunit.UI
{
    /// <summary>
    /// Construtores de controles de menu por código.
    /// </summary>
    /// <remarks>
    /// O projeto monta a interface em runtime em vez de prefabs de UI, seguindo o que o
    /// <see cref="CoopMenu"/> já fazia: assim a tela não depende de assets fora do Git e continua
    /// reproduzível em qualquer clone. Estes widgets evitam repetir o mesmo bloco por opção.
    /// </remarks>
    public static class MenuWidgets
    {
        public static readonly Color Accent = new(0.25f, 0.77f, 0.85f);
        public static readonly Color Muted = new(0.13f, 0.20f, 0.25f);
        public static readonly Color Label = new(0.70f, 0.80f, 0.84f);
        public static readonly Color Panel = new(0.025f, 0.06f, 0.09f, 0.95f);

        public static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static GameObject Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            go.GetComponent<Image>().color = color;
            return go;
        }

        public static TMP_Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size,
            float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        public static Button Button(string label, Transform parent, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction action, float fontSize = 24f)
        {
            var go = Box(label, parent, position, size, Muted);
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(action);
            Text("Texto", go.transform, label, Vector2.zero, size - new Vector2(12, 4), fontSize, Color.white);
            return button;
        }

        /// <summary>
        /// Opção em carrossel: seta esquerda, valor, seta direita. Preferida a um dropdown porque funciona
        /// igual com mouse e teclado e não precisa de um template de lista montado à mão.
        /// </summary>
        public sealed class Cycler
        {
            private readonly TMP_Text _value;
            private readonly Func<int> _get;
            private readonly Action<int> _set;
            private readonly Func<int> _count;
            private readonly Func<int, string> _name;

            public Cycler(string label, Transform parent, float y, Func<int> get, Action<int> set,
                Func<int> count, Func<int, string> name, float width = 760f)
            {
                _get = get; _set = set; _count = count; _name = name;
                Text(label + " rótulo", parent, label, new Vector2(-width * 0.5f + 190f, y),
                    new Vector2(360, 34), 21, Label, TextAlignmentOptions.MidlineLeft);
                Button("<", parent, new Vector2(width * 0.5f - 300f, y), new Vector2(46, 42), () => Step(-1), 22);
                _value = Text(label + " valor", parent, "", new Vector2(width * 0.5f - 160f, y),
                    new Vector2(220, 36), 21, Color.white);
                Button(">", parent, new Vector2(width * 0.5f - 20f, y), new Vector2(46, 42), () => Step(1), 22);
                Refresh();
            }

            private void Step(int direction)
            {
                int total = _count();
                if (total <= 0) return;
                // Dá a volta, para não travar nas pontas.
                _set(((_get() + direction) % total + total) % total);
                Refresh();
            }

            public void Refresh()
            {
                int total = _count();
                _value.text = total <= 0 ? "—" : _name(Mathf.Clamp(_get(), 0, total - 1));
            }
        }

        /// <summary>Slider com o valor escrito ao lado, para a escolha não ficar só na posição do punho.</summary>
        public sealed class Knob
        {
            private readonly Slider _slider;
            private readonly TMP_Text _value;
            private readonly Func<float, string> _format;

            public Knob(string label, Transform parent, float y, float min, float max, float current,
                Action<float> set, Func<float, string> format, bool wholeNumbers = false, float width = 760f)
            {
                _format = format;
                Text(label + " rótulo", parent, label, new Vector2(-width * 0.5f + 190f, y),
                    new Vector2(360, 34), 21, Label, TextAlignmentOptions.MidlineLeft);

                var track = Box(label + " trilho", parent, new Vector2(width * 0.5f - 230f, y), new Vector2(300, 12), Muted);
                _slider = track.AddComponent<Slider>();
                _slider.minValue = min;
                _slider.maxValue = max;
                _slider.wholeNumbers = wholeNumbers;

                var fillArea = new GameObject("Preenchimento", typeof(RectTransform), typeof(Image));
                fillArea.transform.SetParent(track.transform, false);
                var fillRect = (RectTransform)fillArea.transform;
                fillRect.anchorMin = new Vector2(0, 0);
                fillRect.anchorMax = new Vector2(1, 1);
                fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
                fillArea.GetComponent<Image>().color = Accent;
                _slider.fillRect = fillRect;

                var handle = Box(label + " punho", track.transform, Vector2.zero, new Vector2(22, 30), Color.white);
                _slider.handleRect = (RectTransform)handle.transform;
                _slider.targetGraphic = handle.GetComponent<Image>();

                _slider.SetValueWithoutNotify(current);
                _value = Text(label + " valor", parent, format(current), new Vector2(width * 0.5f - 40f, y),
                    new Vector2(130, 34), 20, Color.white);
                _slider.onValueChanged.AddListener(v => { set(v); _value.text = _format(v); });
            }

            public void Refresh(float current)
            {
                _slider.SetValueWithoutNotify(current);
                _value.text = _format(current);
            }
        }

        /// <summary>Liga/desliga como dois botões, no mesmo estilo das outras linhas.</summary>
        public sealed class Switch
        {
            private readonly Image _on, _off;
            private readonly Func<bool> _get;

            public Switch(string label, Transform parent, float y, Func<bool> get, Action<bool> set, float width = 760f)
            {
                _get = get;
                Text(label + " rótulo", parent, label, new Vector2(-width * 0.5f + 190f, y),
                    new Vector2(360, 34), 21, Label, TextAlignmentOptions.MidlineLeft);
                _on = Button("Ligado", parent, new Vector2(width * 0.5f - 230f, y), new Vector2(140, 42),
                    () => { set(true); Refresh(); }, 20).GetComponent<Image>();
                _off = Button("Desligado", parent, new Vector2(width * 0.5f - 80f, y), new Vector2(140, 42),
                    () => { set(false); Refresh(); }, 20).GetComponent<Image>();
                Refresh();
            }

            public void Refresh()
            {
                bool value = _get();
                _on.color = value ? Accent : Muted;
                _off.color = value ? Muted : Accent;
            }
        }

        /// <summary>Fileira de botões exclusivos, para escolhas curtas como o modo de janela.</summary>
        public sealed class Choice
        {
            private readonly Image[] _images;
            private readonly Func<int> _get;

            public Choice(string label, Transform parent, float y, string[] options, Func<int> get, Action<int> set,
                float width = 760f)
            {
                _get = get;
                _images = new Image[options.Length];
                Text(label + " rótulo", parent, label, new Vector2(-width * 0.5f + 190f, y),
                    new Vector2(360, 34), 21, Label, TextAlignmentOptions.MidlineLeft);
                float size = 150f, gap = 8f;
                float total = options.Length * size + (options.Length - 1) * gap;
                float start = width * 0.5f - 20f - total + size * 0.5f;
                for (int i = 0; i < options.Length; i++)
                {
                    int index = i;
                    _images[i] = Button(options[i], parent, new Vector2(start + i * (size + gap), y),
                        new Vector2(size, 42), () => { set(index); Refresh(); }, 19).GetComponent<Image>();
                }
                Refresh();
            }

            public void Refresh()
            {
                int value = _get();
                for (int i = 0; i < _images.Length; i++)
                    _images[i].color = i == value ? Accent : Muted;
            }
        }

        /// <summary>Separador com título, para agrupar as linhas de uma seção.</summary>
        public static void Section(string title, Transform parent, float y, float width = 760f)
        {
            Text(title, parent, title.ToUpperInvariant(), new Vector2(0, y), new Vector2(width, 32), 19, Accent);
            var line = Box(title + " linha", parent, new Vector2(0, y - 22f), new Vector2(width, 2),
                new Color(Accent.r, Accent.g, Accent.b, 0.35f));
            line.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>Área rolável, para a lista de opções caber em telas menores.</summary>
        public static RectTransform ScrollArea(string name, Transform parent, Vector2 position, Vector2 size,
            float contentHeight, out ScrollRect scroll)
        {
            var viewport = Box(name, parent, position, size, new Color(0, 0, 0, 0));
            viewport.AddComponent<RectMask2D>();
            scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var content = new GameObject("Conteúdo", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var rect = (RectTransform)content.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size.x, contentHeight);
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = rect;
            return rect;
        }

        private static readonly List<Button> Reusable = new();

        /// <summary>Liga/desliga todos os botões de um painel, usado enquanto uma conexão está em curso.</summary>
        public static void SetInteractable(GameObject root, bool interactable)
        {
            if (root == null) return;
            Reusable.Clear();
            root.GetComponentsInChildren(true, Reusable);
            foreach (var button in Reusable) button.interactable = interactable;
        }
    }
}
