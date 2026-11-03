using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameYunit.Editor
{
    /// <summary>
    /// Pontos de entrada usados pelo Tools/unity.sh (-executeMethod) e pelo menu GameYunit.
    /// Abrem a cena principal do jogo, que é a primeira cena habilitada em Build Settings.
    /// </summary>
    public static class EditorEntryPoints
    {
        private const string LogPrefix = "[EditorEntryPoints]";

        /// <summary>Abre a cena principal (Tools/unity.sh --reopen-game).</summary>
        [MenuItem("GameYunit/Abrir cena principal")]
        public static void OpenGame()
        {
            string scene = FirstBuildScene();
            if (scene == null)
            {
                Debug.LogWarning($"{LogPrefix} Nenhuma cena habilitada em Build Settings.");
                return;
            }

            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            Debug.Log($"{LogPrefix} Cena aberta: {scene}");
        }

        /// <summary>Abre a cena principal e entra em Play Mode (Tools/unity.sh --reopen-play).</summary>
        [MenuItem("GameYunit/Jogar a partir da cena principal")]
        public static void PlayGame()
        {
            OpenGame();
            if (FirstBuildScene() != null)
            {
                EditorApplication.EnterPlaymode();
            }
        }

        private static string FirstBuildScene() =>
            EditorBuildSettings.scenes.FirstOrDefault(scene => scene.enabled)?.path;
    }
}
