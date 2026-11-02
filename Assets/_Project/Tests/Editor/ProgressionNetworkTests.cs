using System;
using System.Collections;
using System.Collections.Generic;
using GameYunit.Core.Pooling;
using GameYunit.Enemies;
using GameYunit.Network;
using GameYunit.Progression;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameYunit.Tests
{
    public sealed class ProgressionNetworkTests
    {
        private string _assetFolder;
        private GameObject _managerObject;
        private GameObject _ground;
        private NetworkManager _manager;
        private NetworkObjectPool _networkPool;
        private XpDropService _dropService;
        private GameObject _enemyPrefab;
        private PlayerLevel _player;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_manager != null && _manager.IsListening)
            {
                _manager.Shutdown();
                float timeout = Time.realtimeSinceStartup + 10f;
                while (_manager.IsListening && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }
            }

            if (_managerObject != null)
            {
                Object.Destroy(_managerObject);
            }
            if (_ground != null)
            {
                Object.Destroy(_ground);
            }

            yield return null;
            if (!string.IsNullOrEmpty(_assetFolder))
            {
                AssetDatabase.DeleteAsset(_assetFolder);
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator XpCurveRaisesOneLevelUpEventPerLevel()
        {
            yield return StartHost(maxActiveGems: 300);

            var levelUps = new List<int>();
            var globalLevelUps = new List<int>();
            _player.OnLevelUp += levelUps.Add;
            Action<PlayerLevel, int> global = (player, level) => globalLevelUps.Add(level);
            PlayerLevel.OnAnyPlayerLevelUp += global;

            try
            {
                // Curva padrão: 5, 10, 15... para sair dos níveis 1, 2, 3...
                Assert.That(_player.XpToNextLevel, Is.EqualTo(5));
                _player.AddXpServer(4);
                Assert.That(_player.Level, Is.EqualTo(1));
                Assert.That(_player.Xp, Is.EqualTo(4));

                // 4 + 27 = 31 -> nível 2 (sobra 26) -> nível 3 (sobra 16) -> nível 4 (sobra 1).
                _player.AddXpServer(27);
                yield return null;
                Assert.That(_player.Level, Is.EqualTo(4));
                Assert.That(_player.Xp, Is.EqualTo(1));
                Assert.That(levelUps, Is.EqualTo(new[] { 2, 3, 4 }));
                Assert.That(globalLevelUps, Is.EqualTo(new[] { 2, 3, 4 }));
            }
            finally
            {
                PlayerLevel.OnAnyPlayerLevelUp -= global;
            }
        }

        [UnityTest]
        public IEnumerator KilledEnemyDropsGemThatPlayerCollectsOnServer()
        {
            yield return StartHost(maxActiveGems: 300);

            XpGem spawnedGem = null;
            _dropService.OnGemSpawned += gem => spawnedGem = gem;

            EnemyAI enemy = SpawnEnemy(new Vector3(5f, 0f, 0f));
            yield return null;
            Vector3 deathPosition = enemy.transform.position;
            enemy.ReturnToPoolServer();

            yield return WaitFor(() => spawnedGem != null);
            Assert.That(_dropService.ActiveGemCount, Is.EqualTo(1));
            Assert.That(spawnedGem.Value, Is.EqualTo(3));
            Assert.That(Vector3.Distance(spawnedGem.transform.position, deathPosition + Vector3.up * 0.5f),
                Is.LessThan(0.01f));

            // Leva o CharacterController do jogador até a gema: o trigger só é tratado no servidor.
            CharacterController body = _player.GetComponent<CharacterController>();
            Vector3 toGem = spawnedGem.transform.position - _player.transform.position;
            toGem.y = 0f;
            body.Move(toGem);

            yield return WaitFor(() => _player.Xp == 3);
            yield return WaitFor(() => _dropService.ActiveGemCount == 0);
            Assert.That(spawnedGem.IsSpawned, Is.False);
            Assert.That(spawnedGem.GetComponent<PooledObject>().IsInPool, Is.True);
        }

        [UnityTest]
        public IEnumerator DropsAboveCapMergeIntoOldestGem()
        {
            yield return StartHost(maxActiveGems: 1);

            EnemyAI first = SpawnEnemy(new Vector3(6f, 0f, 0f));
            EnemyAI second = SpawnEnemy(new Vector3(-6f, 0f, 0f));
            yield return null;
            first.ReturnToPoolServer();
            second.ReturnToPoolServer();

            yield return WaitFor(() => _dropService.ActiveGemCount == 1);
            yield return null;
            Assert.That(_dropService.ActiveGemCount, Is.EqualTo(1));

            XpGem gem = Object.FindFirstObjectByType<XpGem>();
            Assert.That(gem.Value, Is.EqualTo(6));
        }

        private IEnumerator StartHost(int maxActiveGems)
        {
            string folderName = "__GameYunitProgressionTest_" + Guid.NewGuid().ToString("N");
            _assetFolder = "Assets/" + folderName;
            AssetDatabase.CreateFolder("Assets", folderName);

            var gemTemplate = new GameObject("Test gem");
            gemTemplate.AddComponent<XpGem>();
            GameObject gemPrefab = SavePrefab(gemTemplate, "Gem");

            var playerTemplate = new GameObject("Test player", typeof(NetworkObject));
            CharacterController playerBody = playerTemplate.AddComponent<CharacterController>();
            playerBody.height = 2f;
            playerBody.center = Vector3.up;
            playerTemplate.AddComponent<PlayerLevel>();
            GameObject playerPrefab = SavePrefab(playerTemplate, "Player");

            var enemyTemplate = new GameObject("Test enemy");
            enemyTemplate.AddComponent<EnemyAI>();
            CharacterController capsule = enemyTemplate.GetComponent<CharacterController>();
            capsule.height = 2f;
            capsule.center = Vector3.up;
            XpDropper dropper = enemyTemplate.AddComponent<XpDropper>();
            var dropperSettings = new SerializedObject(dropper);
            dropperSettings.FindProperty("_gemPrefab").objectReferenceValue = gemPrefab.GetComponent<XpGem>();
            dropperSettings.FindProperty("_xpValue").intValue = 3;
            dropperSettings.ApplyModifiedPropertiesWithoutUndo();
            _enemyPrefab = SavePrefab(enemyTemplate, "Enemy");

            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);
            _ground.transform.localScale = new Vector3(100f, 1f, 100f);

            _managerObject = new GameObject("Test network manager");
            _managerObject.SetActive(false);
            _managerObject.AddComponent<ObjectPoolManager>();
            UnityTransport transport = _managerObject.AddComponent<UnityTransport>();
            _manager = _managerObject.AddComponent<NetworkManager>();
            _networkPool = _managerObject.AddComponent<NetworkObjectPool>();
            _dropService = _managerObject.AddComponent<XpDropService>();
            var serviceSettings = new SerializedObject(_dropService);
            serviceSettings.FindProperty("_maxActiveGems").intValue = maxActiveGems;
            serviceSettings.ApplyModifiedPropertiesWithoutUndo();
            _manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                PlayerPrefab = playerPrefab
            };
            transport.SetConnectionData("127.0.0.1", 0, "127.0.0.1");
            _managerObject.SetActive(true);
            _manager.AddNetworkPrefab(_enemyPrefab);
            _manager.AddNetworkPrefab(gemPrefab);
            Assert.That(_networkPool.RegisterPrefab(_enemyPrefab.GetComponent<NetworkObject>(), 2), Is.True);
            Assert.That(_networkPool.RegisterPrefab(gemPrefab.GetComponent<NetworkObject>(), 2), Is.True);
            Assert.That(_manager.StartHost(), Is.True);
            yield return null;

            _player = _manager.LocalClient.PlayerObject.GetComponent<PlayerLevel>();
            Assert.That(_player, Is.Not.Null);
        }

        private EnemyAI SpawnEnemy(Vector3 position)
        {
            NetworkObject enemy = _networkPool.Spawn(_enemyPrefab.GetComponent<NetworkObject>(), position, Quaternion.identity);
            Assert.That(enemy, Is.Not.Null);
            return enemy.GetComponent<EnemyAI>();
        }

        private GameObject SavePrefab(GameObject instance, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, _assetFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, "O estado esperado não chegou em cinco segundos.");
        }
    }
}
