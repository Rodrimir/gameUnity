using System;
using System.Collections;
using System.Collections.Generic;
using GameYunit.Core.Pooling;
using GameYunit.Enemies;
using GameYunit.Network;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GameYunit.Tests
{
    public sealed class EnemyNetworkTests
    {
        private string _assetFolder;
        private GameObject _managerObject;
        private GameObject _ground;
        private NetworkManager _manager;

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
        public IEnumerator HostSpawnsCappedHordeChasesAndReusesEnemies()
        {
            string folderName = "__GameYunitNetworkTest_" + Guid.NewGuid().ToString("N");
            _assetFolder = "Assets/" + folderName;
            AssetDatabase.CreateFolder("Assets", folderName);

            var playerTemplate = new GameObject("Test player", typeof(NetworkObject));
            GameObject playerPrefab = SavePrefab(playerTemplate, "Player");
            var enemyTemplate = new GameObject("Test enemy");
            enemyTemplate.AddComponent<EnemyAI>();
            CharacterController capsule = enemyTemplate.GetComponent<CharacterController>();
            capsule.height = 2f;
            capsule.center = Vector3.up;
            GameObject enemyPrefab = SavePrefab(enemyTemplate, "Enemy");

            var spawnerTemplate = new GameObject("Test spawner");
            EnemySpawner spawnerComponent = spawnerTemplate.AddComponent<EnemySpawner>();
            var serialized = new SerializedObject(spawnerComponent);
            serialized.FindProperty("_enemyPrefab").objectReferenceValue = enemyPrefab.GetComponent<EnemyAI>();
            serialized.FindProperty("_spawnInterval").floatValue = 0.05f;
            serialized.FindProperty("_enemiesPerSpawn").intValue = 2;
            serialized.FindProperty("_maxActiveEnemies").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject spawnerPrefab = SavePrefab(spawnerTemplate, "Spawner");

            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);
            _ground.transform.localScale = new Vector3(100f, 1f, 100f);

            _managerObject = new GameObject("Test network manager");
            _managerObject.SetActive(false);
            _managerObject.AddComponent<ObjectPoolManager>();
            UnityTransport transport = _managerObject.AddComponent<UnityTransport>();
            _manager = _managerObject.AddComponent<NetworkManager>();
            NetworkObjectPool pool = _managerObject.AddComponent<NetworkObjectPool>();
            _manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                PlayerPrefab = playerPrefab
            };
            transport.SetConnectionData("127.0.0.1", 0, "127.0.0.1");
            _managerObject.SetActive(true);
            _manager.AddNetworkPrefab(enemyPrefab);
            _manager.AddNetworkPrefab(spawnerPrefab);
            Assert.That(pool.RegisterPrefab(enemyPrefab.GetComponent<NetworkObject>(), 3), Is.True);
            Assert.That(_manager.StartHost(), Is.True);
            yield return null;

            EnemySpawner spawner = Object.Instantiate(spawnerPrefab).GetComponent<EnemySpawner>();
            var spawned = new List<EnemyAI>();
            spawner.OnEnemySpawned += enemy =>
            {
                float radius = Vector3.Distance(Vector3.zero, enemy.transform.position);
                Assert.That(radius, Is.InRange(12f - 0.001f, 18f + 0.001f));
                Assert.That(enemy.GetComponent<CharacterController>().enabled, Is.True);
                spawned.Add(enemy);
            };
            spawner.GetComponent<NetworkObject>().Spawn();
            yield return WaitFor(() => spawner.ActiveEnemyCount == 3);

            Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(3));
            Assert.That(spawned.Count, Is.EqualTo(3));
            EnemyAI first = spawned[0];
            float before = Vector3.Distance(first.transform.position, Vector3.zero);
            yield return WaitFor(() => Vector3.Distance(first.transform.position, Vector3.zero) < before);
            Assert.That(Vector3.Distance(first.transform.position, Vector3.zero), Is.LessThan(before));

            first.ReturnToPoolServer();
            Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(2));
            Assert.That(first.IsSpawned, Is.False);
            Assert.That(first.GetComponent<PooledObject>().IsInPool, Is.True);
            Assert.That(first.gameObject.activeSelf, Is.False);

            yield return WaitFor(() => spawner.ActiveEnemyCount == 3);
            Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(3));
            Assert.That(spawned.Count, Is.EqualTo(4));
            Assert.That(spawned[3], Is.SameAs(first));
            Assert.That(first.IsSpawned, Is.True);

            spawner.GetComponent<NetworkObject>().Despawn();
            Assert.That(spawner.ActiveEnemyCount, Is.Zero);
            foreach (EnemyAI enemy in spawned)
            {
                Assert.That(enemy.IsSpawned, Is.False);
                Assert.That(enemy.GetComponent<PooledObject>().IsInPool, Is.True);
            }
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
