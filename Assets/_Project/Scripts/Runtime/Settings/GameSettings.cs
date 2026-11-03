using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameYunit.Settings
{
    public enum WindowMode { Fullscreen, Borderless, Windowed }

    /// <summary>
    /// Configurações de vídeo, áudio e controles: aplica no motor e guarda em <see cref="PlayerPrefs"/>.
    /// </summary>
    /// <remarks>
    /// É estática e sem cena própria porque o menu existe antes de qualquer partida e as opções precisam
    /// valer também durante o jogo. A resolução salva é conferida contra a lista do monitor atual a cada
    /// carga: um perfil trazido de outra máquina (ou um monitor trocado) cairia numa resolução inexistente,
    /// e o jogo abriria numa janela inválida.
    /// </remarks>
    public static class GameSettings
    {
        private const string Prefix = "gameyunit.";
        private static bool _loaded;
        private static UniversalRenderPipelineAsset _pipeline;

        /// <summary>Disparado a cada aplicação, para quem precisa reagir (sensibilidade, FOV, HUD de FPS).</summary>
        public static event Action Applied;

        // ---- Vídeo ----
        public static WindowMode Window { get; private set; } = WindowMode.Borderless;
        public static int ResolutionWidth { get; private set; }
        public static int ResolutionHeight { get; private set; }
        public static int RefreshRate { get; private set; }
        /// <summary>0 desligado, 1 a cada V-Blank, 2 a cada segundo V-Blank.</summary>
        public static int VSync { get; private set; } = 1;
        /// <summary>0 significa sem limite. Ignorado quando o VSync está ligado.</summary>
        public static int FrameCap { get; private set; }
        public static int QualityLevel { get; private set; } = 1;
        /// <summary>Escala da resolução interna de renderização, de 0,5 a 1,0.</summary>
        public static float RenderScale { get; private set; } = 1f;
        /// <summary>Amostras de MSAA: 1 (desligado), 2, 4 ou 8.</summary>
        public static int Msaa { get; private set; } = 2;
        public static float Brightness { get; private set; } = 1f;
        public static float FieldOfView { get; private set; } = 40f;
        public static bool ShowFps { get; private set; }

        // ---- Áudio ----
        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 0.8f;
        public static float EffectsVolume { get; private set; } = 1f;

        // ---- Controles ----
        public static float MouseSensitivity { get; private set; } = 1f;
        public static bool InvertMouseY { get; private set; }

        /// <summary>Resoluções do monitor atual, sem repetir pares largura×altura.</summary>
        public static IReadOnlyList<Vector2Int> AvailableResolutions
        {
            get
            {
                var seen = new List<Vector2Int>();
                foreach (var mode in Screen.resolutions)
                {
                    var size = new Vector2Int(mode.width, mode.height);
                    if (!seen.Contains(size)) seen.Add(size);
                }
                if (seen.Count == 0) seen.Add(new Vector2Int(Screen.width, Screen.height));
                return seen;
            }
        }

        public static string[] QualityNames => QualitySettings.names;

        /// <summary>Carrega do disco e aplica. Chamado antes da primeira cena abrir.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            var current = Screen.currentResolution;
            Window = (WindowMode)PlayerPrefs.GetInt(Prefix + "window", (int)WindowMode.Borderless);
            ResolutionWidth = PlayerPrefs.GetInt(Prefix + "width", current.width);
            ResolutionHeight = PlayerPrefs.GetInt(Prefix + "height", current.height);
            RefreshRate = PlayerPrefs.GetInt(Prefix + "refresh", Mathf.RoundToInt((float)current.refreshRateRatio.value));
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1);
            FrameCap = PlayerPrefs.GetInt(Prefix + "framecap", 0);
            QualityLevel = PlayerPrefs.GetInt(Prefix + "quality", QualitySettings.GetQualityLevel());
            RenderScale = PlayerPrefs.GetFloat(Prefix + "renderscale", 1f);
            Msaa = PlayerPrefs.GetInt(Prefix + "msaa", 2);
            Brightness = PlayerPrefs.GetFloat(Prefix + "brightness", 1f);
            FieldOfView = PlayerPrefs.GetFloat(Prefix + "fov", 40f);
            ShowFps = PlayerPrefs.GetInt(Prefix + "showfps", 0) == 1;
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "volmaster", 1f);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "volmusic", 0.8f);
            EffectsVolume = PlayerPrefs.GetFloat(Prefix + "voleffects", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat(Prefix + "sensitivity", 1f);
            InvertMouseY = PlayerPrefs.GetInt(Prefix + "inverty", 0) == 1;
            Sanitize();
            Apply();
        }

        /// <summary>Corrige valores fora de faixa e resoluções que não existem neste monitor.</summary>
        private static void Sanitize()
        {
            var resolutions = AvailableResolutions;
            if (!resolutions.Contains(new Vector2Int(ResolutionWidth, ResolutionHeight)))
            {
                var fallback = resolutions[resolutions.Count - 1];
                Debug.LogWarning($"[GameSettings] Resolução salva {ResolutionWidth}x{ResolutionHeight} não existe " +
                                 $"neste monitor; usando {fallback.x}x{fallback.y}.");
                ResolutionWidth = fallback.x;
                ResolutionHeight = fallback.y;
            }
            VSync = Mathf.Clamp(VSync, 0, 2);
            FrameCap = FrameCap <= 0 ? 0 : Mathf.Clamp(FrameCap, 30, 480);
            QualityLevel = Mathf.Clamp(QualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            RenderScale = Mathf.Clamp(RenderScale, 0.5f, 1f);
            Msaa = Msaa >= 8 ? 8 : Msaa >= 4 ? 4 : Msaa >= 2 ? 2 : 1;
            Brightness = Mathf.Clamp(Brightness, 0.5f, 1.6f);
            FieldOfView = Mathf.Clamp(FieldOfView, 30f, 90f);
            MasterVolume = Mathf.Clamp01(MasterVolume);
            MusicVolume = Mathf.Clamp01(MusicVolume);
            EffectsVolume = Mathf.Clamp01(EffectsVolume);
            MouseSensitivity = Mathf.Clamp(MouseSensitivity, 0.2f, 3f);
        }

        public static void SetWindow(WindowMode mode) { Window = mode; Apply(); }
        public static void SetResolution(int width, int height) { ResolutionWidth = width; ResolutionHeight = height; Apply(); }
        public static void SetVSync(int mode) { VSync = Mathf.Clamp(mode, 0, 2); Apply(); }
        public static void SetFrameCap(int fps) { FrameCap = fps <= 0 ? 0 : Mathf.Clamp(fps, 30, 480); Apply(); }
        public static void SetQuality(int level) { QualityLevel = level; Apply(); }
        public static void SetRenderScale(float scale) { RenderScale = Mathf.Clamp(scale, 0.5f, 1f); Apply(); }
        public static void SetMsaa(int samples) { Msaa = samples; Sanitize(); Apply(); }
        public static void SetBrightness(float value) { Brightness = Mathf.Clamp(value, 0.5f, 1.6f); Apply(); }
        public static void SetFieldOfView(float fov) { FieldOfView = Mathf.Clamp(fov, 30f, 90f); Apply(); }
        public static void SetShowFps(bool show) { ShowFps = show; Apply(); }
        public static void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); Apply(); }
        public static void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); Apply(); }
        public static void SetEffectsVolume(float value) { EffectsVolume = Mathf.Clamp01(value); Apply(); }
        public static void SetMouseSensitivity(float value) { MouseSensitivity = Mathf.Clamp(value, 0.2f, 3f); Apply(); }
        public static void SetInvertMouseY(bool invert) { InvertMouseY = invert; Apply(); }

        /// <summary>Devolve tudo ao padrão de fábrica.</summary>
        public static void ResetToDefaults()
        {
            var current = Screen.currentResolution;
            Window = WindowMode.Borderless;
            ResolutionWidth = current.width;
            ResolutionHeight = current.height;
            VSync = 1;
            FrameCap = 0;
            QualityLevel = Mathf.Min(1, Mathf.Max(0, QualitySettings.names.Length - 1));
            RenderScale = 1f;
            Msaa = 2;
            Brightness = 1f;
            FieldOfView = 40f;
            ShowFps = false;
            MasterVolume = 1f;
            MusicVolume = 0.8f;
            EffectsVolume = 1f;
            MouseSensitivity = 1f;
            InvertMouseY = false;
            Sanitize();
            Apply();
        }

        /// <summary>Escreve no motor e no disco, e avisa quem depende das opções.</summary>
        public static void Apply()
        {
            QualitySettings.SetQualityLevel(QualityLevel, true);
            QualitySettings.vSyncCount = VSync;
            // Com VSync ligado o motor já limita pelo monitor; um teto menor brigaria com ele.
            Application.targetFrameRate = VSync > 0 || FrameCap <= 0 ? -1 : FrameCap;
            ApplyPipeline();
            ApplyScreen();
            AudioListener.volume = MasterVolume;
            Save();
            Applied?.Invoke();
        }

        private static void ApplyScreen()
        {
            var mode = Window switch
            {
                WindowMode.Fullscreen => FullScreenMode.ExclusiveFullScreen,
                WindowMode.Borderless => FullScreenMode.FullScreenWindow,
                _ => FullScreenMode.Windowed
            };
            if (Screen.width == ResolutionWidth && Screen.height == ResolutionHeight && Screen.fullScreenMode == mode)
                return;
            Screen.SetResolution(ResolutionWidth, ResolutionHeight, mode);
        }

        /// <summary>
        /// Escala de renderização e MSAA moram no asset do URP, não em QualitySettings.
        /// </summary>
        /// <remarks>
        /// Mexer no asset em runtime altera o arquivo dentro do Editor (fica sujo no disco), mas numa build
        /// a mudança é só em memória. É o caminho que a própria Unity oferece: não há API de instância.
        /// </remarks>
        private static void ApplyPipeline()
        {
            if (_pipeline == null) _pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_pipeline == null) return;
            _pipeline.renderScale = RenderScale;
            _pipeline.msaaSampleCount = Msaa;
        }

        private static void Save()
        {
            PlayerPrefs.SetInt(Prefix + "window", (int)Window);
            PlayerPrefs.SetInt(Prefix + "width", ResolutionWidth);
            PlayerPrefs.SetInt(Prefix + "height", ResolutionHeight);
            PlayerPrefs.SetInt(Prefix + "refresh", RefreshRate);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync);
            PlayerPrefs.SetInt(Prefix + "framecap", FrameCap);
            PlayerPrefs.SetInt(Prefix + "quality", QualityLevel);
            PlayerPrefs.SetFloat(Prefix + "renderscale", RenderScale);
            PlayerPrefs.SetInt(Prefix + "msaa", Msaa);
            PlayerPrefs.SetFloat(Prefix + "brightness", Brightness);
            PlayerPrefs.SetFloat(Prefix + "fov", FieldOfView);
            PlayerPrefs.SetInt(Prefix + "showfps", ShowFps ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "volmaster", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "volmusic", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "voleffects", EffectsVolume);
            PlayerPrefs.SetFloat(Prefix + "sensitivity", MouseSensitivity);
            PlayerPrefs.SetInt(Prefix + "inverty", InvertMouseY ? 1 : 0);
            // Gravação imediata: um travamento perderia a escolha, e são poucas chaves.
            PlayerPrefs.Save();
        }

        // Com "Enter Play Mode Options" estáticos sobrevivem entre sessões de Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _loaded = false;
            _pipeline = null;
            Applied = null;
        }
    }
}
