using System.Collections;
using GameYunit.Characters;
using GameYunit.Enemies;
using GameYunit.Network;
using GameYunit.Player;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GameYunit.Tests
{
    public sealed class CharacterSceneTests
    {
        private NetworkManager _manager;

        [UnityTest]
        public IEnumerator GameSceneStartsHostAndSwitchesEverySelectableCharacter()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity");
            yield return new EnterPlayMode();
            var setup = Object.FindFirstObjectByType<NetworkManagerSetup>();
            Assert.That(setup, Is.Not.Null);
            _manager = setup.GetComponent<NetworkManager>();
            PlayerAppearance.PreferredCharacterIndex = 1;
            setup.StartHost();

            float timeout = Time.realtimeSinceStartup + 10f;
            while (PlayerNetwork.LocalPlayer == null && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(PlayerNetwork.LocalPlayer, Is.Not.Null, "Host não criou o jogador local.");
            PlayerAppearance appearance = PlayerNetwork.LocalPlayer.GetComponent<PlayerAppearance>();
            Assert.That(appearance, Is.Not.Null);
            Assert.That(appearance.CharacterCount, Is.EqualTo(3));
            Assert.That(appearance.SelectedIndex, Is.EqualTo(1));

            for (int i = 0; i < appearance.CharacterCount; i++)
            {
                appearance.RequestCharacter(i);
                yield return null;
                Assert.That(appearance.SelectedIndex, Is.EqualTo(i));
                int activeVisuals = 0;
                foreach (Transform child in appearance.transform)
                {
                    if (child.gameObject.activeSelf) activeVisuals++;
                }
                Assert.That(activeVisuals, Is.EqualTo(1), "Só um visual deve estar ativo por jogador.");
                Assert.That(appearance.GetComponent<CharacterController>().enabled, Is.True);
            }

            appearance.RequestCharacter(-1);
            appearance.RequestCharacter(appearance.CharacterCount);
            Assert.That(appearance.SelectedIndex, Is.EqualTo(2), "Índices inválidos devem ser ignorados.");
            timeout = Time.realtimeSinceStartup + 5f;
            while (EnemyAI.ActiveOnServer.Count == 0 && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(EnemyAI.ActiveOnServer.Count, Is.GreaterThan(0), "O monstro deve nascer no Host.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_manager != null && _manager.IsListening)
            {
                _manager.Shutdown();
                float timeout = Time.realtimeSinceStartup + 10f;
                while (_manager.IsListening && Time.realtimeSinceStartup < timeout) yield return null;
            }

            yield return new ExitPlayMode();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Characters.unity");
        }
    }
}
