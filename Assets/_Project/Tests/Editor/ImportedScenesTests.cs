using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GameYunit.Tests
{
    public sealed class ImportedScenesTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator StartButtonLoadsPlayableLevel1()
        {
            Assert.That(Resources.Load<TMP_Settings>("TMP Settings"), Is.Not.Null,
                "Importe os recursos essenciais do TextMesh Pro antes de iniciar o jogo.");
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null,
                "A fonte padrão do TextMesh Pro precisa estar disponível.");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Start.unity");
            yield return new EnterPlayMode();
            yield return null;
            var startButton = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(button => button.name == "StartButton");
            Assert.That(startButton, Is.Not.Null, "O botão de início precisa existir no menu do kit.");
            startButton.onClick.Invoke();

            float deadline = Time.realtimeSinceStartup + 60f;
            while (SceneManager.GetActiveScene().name != "Level1" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level1"));
            yield return new WaitForSeconds(3f);
            var player = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .FirstOrDefault(component => component.GetType().FullName == "Gamekit3D.PlayerController");
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(player.GetComponent<Animator>().runtimeAnimatorController, Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [TestCase("Start")]
        [TestCase("Level1")]
        public void ImportedSceneHasNoMissingScripts(string sceneName)
        {
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene($"Assets/_Project/Scenes/{sceneName}.unity");
                var objects = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(transform => transform.gameObject).ToArray();
                Assert.That(objects, Is.Not.Empty);
                var missing = objects.Where(gameObject =>
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) != 0);
                Assert.That(missing.Select(gameObject => gameObject.name), Is.Empty,
                    "A cena contém componentes cujo script ou dependência não foi importado.");
                Assert.That(GraphicsSettings.defaultRenderPipeline, Is.Not.Null,
                    "O pipeline URP referenciado pelo projeto precisa estar disponível.");

                if (sceneName == "Level1")
                {
                    var player = objects.SelectMany(gameObject => gameObject.GetComponents<MonoBehaviour>())
                        .FirstOrDefault(component => component != null &&
                            component.GetType().FullName == "Gamekit3D.PlayerController");
                    Assert.That(player, Is.Not.Null, "A Ellen do kit precisa estar presente no Level1.");
                    Assert.That(player.GetComponent<CharacterController>(), Is.Not.Null);
                    Assert.That(player.GetComponent<Animator>().runtimeAnimatorController, Is.Not.Null);
                }
            }
            finally
            {
                if (previousSetup.Any(setup => setup.isLoaded) && previousSetup.Any(setup => setup.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
