using UnityEditor;
using UnityEngine;

namespace GameYunit.Editor
{
    /// <summary>
    /// Regras de importação no estilo PSX, compartilhadas pelo postprocessor automático e pelos menus.
    /// </summary>
    /// <remarks>
    /// Para isentar um asset, escreva <c>psx-ignore</c> no campo User Data do importer (modo Debug do Inspector).
    /// </remarks>
    internal static class PsxImportRules
    {
        public const string IgnoreTag = "psx-ignore";

        /// <summary>A VRAM do PSX limitava texturas a 256x256.</summary>
        public const int MaxTextureSize = 256;

        public const uint AudioSampleRate = 11025;

        // Pastas tratadas automaticamente a cada importação. Mapas de outras pastas usam o menu.
        private const string ArtFolder = "Assets/_Project/Art/";
        private const string AudioFolder = "Assets/_Project/Audio/";

        public static bool IsAutoTexturePath(string path) =>
            path.StartsWith(ArtFolder) && !path.Contains("/UI/");

        public static bool IsAutoAudioPath(string path) => path.StartsWith(AudioFolder);

        public static bool IsIgnored(AssetImporter importer) =>
            importer != null && !string.IsNullOrEmpty(importer.userData) && importer.userData.Contains(IgnoreTag);

        /// <summary>Filtro Point, sem mipmaps e sem compressão com perdas, no máximo 256 px.</summary>
        public static void ApplyTexture(TextureImporter importer)
        {
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.anisoLevel = 0;
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        /// <summary>
        /// Mono a 11025 Hz. Efeitos ficam descomprimidos na memória (ADPCM, decodificação barata);
        /// músicas (pasta /Music/) são transmitidas em Vorbis.
        /// </summary>
        public static void ApplyAudio(AudioImporter importer, string path)
        {
            bool music = path.Contains("/Music/");
            importer.forceToMono = true;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = AudioSampleRate;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.quality = music ? 0.5f : 1f;
            importer.defaultSampleSettings = settings;
        }
    }

    /// <summary>Aplica as regras PSX automaticamente a texturas de <c>_Project/Art</c> e áudios de <c>_Project/Audio</c>.</summary>
    internal sealed class PsxAssetPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (PsxImportRules.IsAutoTexturePath(assetPath) && !PsxImportRules.IsIgnored(assetImporter))
            {
                PsxImportRules.ApplyTexture((TextureImporter)assetImporter);
            }
        }

        private void OnPreprocessAudio()
        {
            if (PsxImportRules.IsAutoAudioPath(assetPath) && !PsxImportRules.IsIgnored(assetImporter))
            {
                PsxImportRules.ApplyAudio((AudioImporter)assetImporter, assetPath);
            }
        }
    }
}
