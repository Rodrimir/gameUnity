using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameYunit.Editor
{
    /// <summary>Pontos de entrada usados pelo launcher e pelo menu do projeto.</summary>
    public static class EditorEntryPoints
    {
        private const string KitScenes = "Assets/3DGamekit/Scenes/";
        private const string ProjectScenes = "Assets/_Project/Scenes/";

        [MenuItem("GameYunit/Preparação/Importar 3D Game Kit")]
        public static void OpenKitImport()
        {
            EditorApplication.delayCall += () =>
            {
                UnityEditor.PackageManager.UI.Window.Open("115747");
                Debug.Log("[EditorEntryPoints] Package Manager aberto no 3D Game Kit da Asset Store.");
            };
        }

        [MenuItem("GameYunit/Preparação/Preparar single-player")]
        public static void PrepareSinglePlayer()
        {
            CopyScene("Start.unity", "Start.unity", "95d704b8dd3f45a8b8a3a9ae4a2fb429");
            CopyScene("GamePlay/Level1.unity", "Level1.unity", "a37d1ca0eb92bffb1a56d6c0b9088a87");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ProjectScenes + "Start.unity", true),
                new EditorBuildSettingsScene(ProjectScenes + "Level1.unity", true)
            };
            OpenLevel1();
            Debug.Log("[EditorEntryPoints] Cópias de Start e Level1 preparadas para single-player.");
        }

        [MenuItem("GameYunit/Abrir cena principal")]
        public static void OpenGame()
        {
            TryOpenScene(FirstBuildScene());
        }

        [MenuItem("GameYunit/Jogar a partir da cena principal")]
        public static void PlayGame()
        {
            if (TryOpenScene(FirstBuildScene())) StartPlayMode();
        }

        [MenuItem("GameYunit/Abrir Level1")]
        public static void OpenLevel1()
        {
            TryOpenScene(ProjectScenes + "Level1.unity");
        }

        [MenuItem("GameYunit/Jogar Level1")]
        public static void PlayLevel1()
        {
            if (TryOpenScene(ProjectScenes + "Level1.unity")) StartPlayMode();
        }

        [MenuItem("GameYunit/Jogar pelo menu do kit")]
        public static void PlayMenu()
        {
            if (TryOpenScene(ProjectScenes + "Start.unity")) StartPlayMode();
        }

        private static void StartPlayMode()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            ConfigureGameView();
            EditorApplication.EnterPlaymode();
        }

        private static string FirstBuildScene() =>
            EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled)?.path;

        private static bool TryOpenScene(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning("[EditorEntryPoints] Cena indisponível. Importe o kit e prepare o single-player.");
                return false;
            }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log($"[EditorEntryPoints] Cena aberta: {path}");
            return true;
        }

        [MenuItem("GameYunit/Prévia Full HD")]
        public static void ConfigureGameView()
        {
            // A Unity não expõe uma API pública para resolução/zoom da aba Game.
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(type);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            type.GetProperty("lowResolutionForAspectRatios", flags).SetValue(view, false);
            // Preset Full HD (1920x1080) do grupo Standalone da Unity 6.3.
            type.GetMethod("SizeSelectionCallback", flags).Invoke(view, new object[] { 3, null });
            float fit = (float)type.GetProperty("minScale", flags).GetValue(view);
            type.GetMethod("SnapZoom", flags).Invoke(view, new object[] { fit });
            view.Repaint();
            Debug.Log($"[EditorEntryPoints] Prévia Full HD configurada; escala ajustada à janela: {fit:F2}x.");
        }

        private static void CopyScene(string source, string destination, string guid)
        {
            string sourcePath = KitScenes + source;
            string destinationPath = ProjectScenes + destination;
            if (File.Exists(destinationPath)) return;
            // GUID estável evita alterações de Build Settings ao gerar a cópia ignorada em outra máquina.
            File.Copy(sourcePath, destinationPath);
            File.WriteAllText(destinationPath + ".meta",
                $"fileFormatVersion: 2\nguid: {guid}\nDefaultImporter:\n  externalObjects: {{}}\n" +
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n");
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
