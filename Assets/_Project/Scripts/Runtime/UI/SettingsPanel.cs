using System.Collections.Generic;
using System.Linq;
using GameYunit.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GameYunit.UI.MenuWidgets;

namespace GameYunit.UI
{
    /// <summary>
    /// Painel de configurações: vídeo, áudio, controles e interface.
    /// </summary>
    /// <remarks>
    /// As opções seguem o conjunto que é padrão em jogos de PC — presets de qualidade, resolução, modo de
    /// janela, VSync, limite de quadros, anti-aliasing e escala de renderização —, cada uma aplicada na hora
    /// para a escolha ser conferida sem reiniciar. O painel é construído por código, como o resto da
    /// interface do projeto, para não depender de prefabs fora do Git.
    /// </remarks>
    public sealed class SettingsPanel : MonoBehaviour
    {
        private GameObject _root;
        private readonly List<Cycler> _cyclers = new();
        private readonly List<Switch> _switches = new();
        private readonly List<Choice> _choices = new();
        private readonly List<Knob> _knobs = new();
        private IReadOnlyList<Vector2Int> _resolutions;
        private static readonly int[] FrameCaps = { 0, 30, 60, 75, 90, 120, 144, 165, 240 };
        private static readonly int[] MsaaOptions = { 1, 2, 4, 8 };

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform parent)
        {
            _resolutions = GameSettings.AvailableResolutions;
            _root = Box("Configurações", parent, Vector2.zero, new Vector2(1920, 1080), Panel);
            Text("Título", _root.transform, "Configurações", new Vector2(0, 452), new Vector2(900, 70), 48, Color.white);

            var content = ScrollArea("Rolagem", _root.transform, new Vector2(0, -10), new Vector2(860, 800),
                1500f, out _);
            float y = -26f;

            Section("Vídeo", content, y); y -= 70f;
            _choices.Add(new Choice("Modo de janela", content, y,
                new[] { "Tela cheia", "Sem borda", "Janela" },
                () => (int)GameSettings.Window, v => GameSettings.SetWindow((WindowMode)v))); y -= 56f;
            _cyclers.Add(new Cycler("Resolução", content, y,
                CurrentResolutionIndex,
                v => GameSettings.SetResolution(_resolutions[v].x, _resolutions[v].y),
                () => _resolutions.Count,
                v => $"{_resolutions[v].x} × {_resolutions[v].y}")); y -= 56f;
            _cyclers.Add(new Cycler("Qualidade", content, y,
                () => GameSettings.QualityLevel, GameSettings.SetQuality,
                () => GameSettings.QualityNames.Length,
                v => GameSettings.QualityNames[v])); y -= 56f;
            _choices.Add(new Choice("Sincronismo vertical", content, y,
                new[] { "Desligado", "A cada quadro", "A cada 2" },
                () => GameSettings.VSync, GameSettings.SetVSync)); y -= 56f;
            _cyclers.Add(new Cycler("Limite de quadros", content, y,
                () => System.Array.IndexOf(FrameCaps, GameSettings.FrameCap) is var i && i >= 0 ? i : 0,
                v => GameSettings.SetFrameCap(FrameCaps[v]),
                () => FrameCaps.Length,
                v => FrameCaps[v] == 0 ? "Sem limite" : $"{FrameCaps[v]} fps")); y -= 56f;
            _cyclers.Add(new Cycler("Suavização de serrilhado", content, y,
                () => System.Array.IndexOf(MsaaOptions, GameSettings.Msaa) is var i && i >= 0 ? i : 0,
                v => GameSettings.SetMsaa(MsaaOptions[v]),
                () => MsaaOptions.Length,
                v => MsaaOptions[v] == 1 ? "Desligada" : $"MSAA {MsaaOptions[v]}×")); y -= 56f;
            _knobs.Add(new Knob("Escala de renderização", content, y, 0.5f, 1f, GameSettings.RenderScale,
                GameSettings.SetRenderScale, v => $"{v * 100f:0}%")); y -= 56f;
            _knobs.Add(new Knob("Brilho", content, y, 0.5f, 1.6f, GameSettings.Brightness,
                GameSettings.SetBrightness, v => $"{v * 100f:0}%")); y -= 56f;
            _knobs.Add(new Knob("Campo de visão", content, y, 30f, 90f, GameSettings.FieldOfView,
                GameSettings.SetFieldOfView, v => $"{v:0}°", true)); y -= 76f;

            Section("Áudio", content, y); y -= 70f;
            _knobs.Add(new Knob("Volume geral", content, y, 0f, 1f, GameSettings.MasterVolume,
                GameSettings.SetMasterVolume, Percent)); y -= 56f;
            _knobs.Add(new Knob("Música", content, y, 0f, 1f, GameSettings.MusicVolume,
                GameSettings.SetMusicVolume, Percent)); y -= 56f;
            _knobs.Add(new Knob("Efeitos", content, y, 0f, 1f, GameSettings.EffectsVolume,
                GameSettings.SetEffectsVolume, Percent)); y -= 76f;

            Section("Controles", content, y); y -= 70f;
            _knobs.Add(new Knob("Sensibilidade do mouse", content, y, 0.2f, 3f, GameSettings.MouseSensitivity,
                GameSettings.SetMouseSensitivity, v => $"{v:0.00}×")); y -= 56f;
            _switches.Add(new Switch("Inverter eixo vertical", content, y,
                () => GameSettings.InvertMouseY, GameSettings.SetInvertMouseY)); y -= 76f;

            Section("Interface", content, y); y -= 70f;
            _switches.Add(new Switch("Mostrar quadros por segundo", content, y,
                () => GameSettings.ShowFps, GameSettings.SetShowFps)); y -= 56f;

            Button("Restaurar padrões", _root.transform, new Vector2(-230, -452), new Vector2(420, 64),
                () => { GameSettings.ResetToDefaults(); Refresh(); }, 23);
            Button("Voltar", _root.transform, new Vector2(230, -452), new Vector2(420, 64), Close, 23);
            Text("Aviso", _root.transform, "As mudanças valem na hora e ficam salvas nesta máquina.",
                new Vector2(0, -510), new Vector2(1100, 34), 18, Label);
            _root.SetActive(false);
        }

        private static string Percent(float value) => $"{value * 100f:0}%";

        private int CurrentResolutionIndex()
        {
            for (int i = 0; i < _resolutions.Count; i++)
                if (_resolutions[i].x == GameSettings.ResolutionWidth &&
                    _resolutions[i].y == GameSettings.ResolutionHeight) return i;
            return Mathf.Max(0, _resolutions.Count - 1);
        }

        public void Open()
        {
            if (_root == null) return;
            // A lista de resoluções pode mudar se o monitor for trocado com o jogo aberto.
            _resolutions = GameSettings.AvailableResolutions;
            Refresh();
            _root.SetActive(true);
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>Relê os valores atuais, depois de restaurar padrões ou reabrir o painel.</summary>
        private void Refresh()
        {
            foreach (var cycler in _cyclers) cycler.Refresh();
            foreach (var choice in _choices) choice.Refresh();
            foreach (var toggle in _switches) toggle.Refresh();
            // Os sliders leem o valor na construção; relê um a um na mesma ordem em que foram criados.
            if (_knobs.Count >= 7)
            {
                _knobs[0].Refresh(GameSettings.RenderScale);
                _knobs[1].Refresh(GameSettings.Brightness);
                _knobs[2].Refresh(GameSettings.FieldOfView);
                _knobs[3].Refresh(GameSettings.MasterVolume);
                _knobs[4].Refresh(GameSettings.MusicVolume);
                _knobs[5].Refresh(GameSettings.EffectsVolume);
                _knobs[6].Refresh(GameSettings.MouseSensitivity);
            }
        }
    }
}
