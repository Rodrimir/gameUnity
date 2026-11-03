using System.Collections;
using System.Linq;
using System.Text;
using GameYunit.Network;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GameYunit.Tests
{
    public sealed class CoopTests
    {
        [TestCase(0, 1, 0, null)]
        [TestCase(1, 1, 1, null)]
        [TestCase(2, 1, 0, "Sala cheia")]
        [TestCase(3, 1, 1, "Sala cheia")]
        [TestCase(0, 2, 0, "Versão incompatível")]
        [TestCase(0, 1, -1, "Cor de jogador inválida")]
        public void ApprovalIncludesHostAndReservesOnlyTwoSlots(int slots, int version, int color, string rejection)
        {
            var result = ConnectionRules.Rejection(slots, version, color);
            if (rejection == null) Assert.That(result, Is.Null);
            else Assert.That(result, Does.StartWith(rejection));
        }

        [TestCase("127.0.0.1", "7777", true)]
        [TestCase("192.168.1.50", "65535", true)]
        [TestCase("0.0.0.0", "7777", false)]
        [TestCase("localhost", "7777", false)]
        [TestCase("127.0.0.1", "0", false)]
        [TestCase("127.0.0.1", "65536", false)]
        [TestCase("127.0.0.1", "abc", false)]
        public void EndpointValidatesAddressAndPort(string ip, string port, bool valid)
        {
            Assert.That(ConnectionRules.TryEndpoint(ip, port, out _, out _), Is.EqualTo(valid));
        }

        [Test]
        public void NamesCannotInjectRichTextOrOverflowNetworkString()
        {
            Assert.That(ConnectionRules.CleanName("<color=red>João</color>"), Does.Not.Contain("<"));
            Assert.That(ConnectionRules.CleanName(""), Is.EqualTo("Explorador"));
            string name = ConnectionRules.CleanName(new string('界', 40));
            Assert.That(name.Length, Is.LessThanOrEqualTo(24));
            Assert.That(Encoding.UTF8.GetByteCount(name), Is.LessThanOrEqualTo(60));
        }

        [Test]
        public void CoopPrefabUsesOwnerAuthorityWithoutOriginalControllers()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/EllenCoop.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkTransform>().AuthorityMode, Is.EqualTo(NetworkTransform.AuthorityModes.Owner));
            Assert.That(prefab.GetComponent<NetworkAnimator>().AuthorityMode, Is.EqualTo(NetworkAnimator.AuthorityModes.Owner));
            Assert.That(prefab.GetComponent<NetworkPlayerIdentity>(), Is.Not.Null);
            var bridge = prefab.GetComponents<MonoBehaviour>().First(s => s.GetType().FullName == "GameYunit.KitBridge.PlayerController");
            foreach (string field in new[] { "meleeWeapon", "footstepPlayer", "hurtAudioPlayer", "landingPlayer", "emoteLandingPlayer", "emoteDeathPlayer", "emoteAttackPlayer", "emoteJumpPlayer" })
                Assert.That(new SerializedObject(bridge).FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            var scripts = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            Assert.That(scripts.All(s => s != null), Is.True);
            Assert.That(scripts.Any(s => s.GetType().FullName == "PlayerInput" || s.GetType().FullName == "Gamekit3D.PlayerController"), Is.False);
        }

        [Test]
        public void CoopSceneHasTwoSpawnsAndNoSinglePlayerFlow()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level1_Coop.unity");
            var spawns = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            Assert.That(spawns.Length, Is.EqualTo(2));
            Assert.That(Vector3.Distance(spawns[0].transform.position, spawns[1].transform.position), Is.GreaterThan(1));
            var scripts = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            string[] prohibited = { "PlayerInput", "Gamekit3D.PlayerController", "Gamekit3D.SceneController", "Gamekit3D.StartUI", "Gamekit3D.EnemyController" };
            Assert.That(scripts.Where(s => s != null).Any(s => prohibited.Contains(s.GetType().FullName)), Is.False);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator HostLoadsCoopAndReturnsToMenuOnShutdown()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/CoopMenu.unity");
            yield return new EnterPlayMode();
            yield return null;
            var session = CoopSession.Instance;
            Assert.That(session.StartSession(true, "127.0.0.1", "17777", "Teste Host", 0, ConnectionProtocol.Udp), Is.True);
            float deadline = Time.realtimeSinceStartup + 90;
            while (session.Manager.LocalClient?.PlayerObject == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(CoopSession.LevelScene));
            Assert.That(session.Manager.LocalClient.PlayerObject, Is.Not.Null);
            Assert.That(session.Manager.LocalClient.PlayerObject.GetComponent<NetworkPlayerIdentity>().PlayerName.Value.ToString(), Is.EqualTo("Teste Host"));
            Assert.That(session.LocalMenuOpen, Is.False);
            session.Leave();
            deadline = Time.realtimeSinceStartup + 30;
            while ((session.Busy || SceneManager.GetActiveScene().name != CoopSession.MenuScene) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(session.Busy, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(CoopSession.MenuScene));
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
