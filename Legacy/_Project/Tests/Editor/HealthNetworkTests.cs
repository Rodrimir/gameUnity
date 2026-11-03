using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using GameYunit.Combat;
using GameYunit.Core.Pooling;
using GameYunit.Enemies;
using GameYunit.Network;
using GameYunit.Player;
using GameYunit.UI;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameYunit.Tests
{
    public sealed class HealthNetworkTests
    {
        private string _assetFolder;
        private NetworkManager _host;
        private NetworkManager _client;
        private readonly List<GameObject> _roots = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            string name = "__GameYunitHealthTest_" + Guid.NewGuid().ToString("N");
            _assetFolder = "Assets/" + name;
            AssetDatabase.CreateFolder("Assets", name);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (NetworkManager manager in new[] { _client, _host })
            {
                if (manager != null && manager.IsListening)
                {
                    manager.Shutdown();
                    yield return WaitFor(() => !manager.IsListening && !manager.ShutdownInProgress);
                }
            }

            foreach (GameObject root in _roots)
            {
                if (root != null)
                {
                    Object.Destroy(root);
                }
            }

            _roots.Clear();
            yield return null;
            if (!string.IsNullOrEmpty(_assetFolder))
            {
                AssetDatabase.DeleteAsset(_assetFolder);
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ServerBoundsHealthAndEnemyDeathRecyclesAndResets()
        {
            var playerTemplate = new GameObject("Health test player");
            playerTemplate.AddComponent<HealthComponent>();
            GameObject playerPrefab = SavePrefab(playerTemplate, "Player");
            var enemyTemplate = new GameObject("Health test enemy");
            enemyTemplate.AddComponent<EnemyAI>();
            GameObject enemyPrefab = SavePrefab(enemyTemplate, "Enemy");

            _host = CreateManager("Health test host", playerPrefab, ReservePort(), withPool: true);
            _host.AddNetworkPrefab(enemyPrefab);
            NetworkObjectPool pool = _host.GetComponent<NetworkObjectPool>();
            NetworkObject prefabObject = enemyPrefab.GetComponent<NetworkObject>();
            Assert.That(pool.RegisterPrefab(prefabObject, 1), Is.True);
            Assert.That(_host.StartHost(), Is.True);
            yield return WaitFor(() => _host.LocalClient.PlayerObject != null);

            HealthComponent player = _host.LocalClient.PlayerObject.GetComponent<HealthComponent>();
            int deaths = 0;
            int changes = 0;
            player.OnDeath += health => deaths++;
            player.OnHealthChanged += (previous, current) => changes++;
            Assert.That(player.CurrentHealth, Is.EqualTo(player.MaxHealth));
            Assert.That(player.TakeDamageServer(0), Is.False);
            Assert.That(player.TakeDamageServer(int.MinValue), Is.False);
            Assert.That(player.TakeDamageServer(15), Is.True);
            Assert.That(player.CurrentHealth, Is.EqualTo(85));
            Assert.That(player.HealServer(int.MaxValue), Is.True);
            Assert.That(player.CurrentHealth, Is.EqualTo(100));
            Assert.That(player.TakeDamageServer(int.MaxValue), Is.True);
            Assert.That(player.CurrentHealth, Is.Zero);
            Assert.That(player.TakeDamageServer(1), Is.False);
            Assert.That(player.HealServer(1), Is.False);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(3));

            NetworkObject first = pool.Spawn(prefabObject, new Vector3(5f, 1f, 0f), Quaternion.identity);
            HealthComponent enemy = first.GetComponent<HealthComponent>();
            int enemyDeaths = 0;
            enemy.OnDeath += health => enemyDeaths++;
            Assert.That(enemy.TakeDamageServer(int.MaxValue), Is.True);
            Assert.That(first.IsSpawned, Is.False);
            Assert.That(first.GetComponent<PooledObject>().IsInPool, Is.True);
            Assert.That(first.gameObject.activeSelf, Is.False);

            NetworkObject second = pool.Spawn(prefabObject, new Vector3(-5f, 1f, 0f), Quaternion.identity);
            Assert.That(second, Is.SameAs(first));
            Assert.That(enemy.IsDead, Is.False);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(enemy.MaxHealth));
            Assert.That(enemy.TakeDamageServer(int.MaxValue), Is.True);
            Assert.That(enemyDeaths, Is.EqualTo(2));
            Assert.That(second.GetComponent<PooledObject>().IsInPool, Is.True);
        }

        [UnityTest]
        public IEnumerator ClientReplicatesHealthAndHudOnlyTracksLocalOwner()
        {
            var template = new GameObject("Health test network player");
            template.AddComponent<HealthComponent>();
            template.AddComponent<PlayerNetwork>();
            GameObject prefab = SavePrefab(template, "Player");
            ushort port = ReservePort();
            _host = CreateManager("Health test host", prefab, port);
            Assert.That(_host.StartHost(), Is.True);
            yield return WaitFor(() => _host.LocalClient.PlayerObject != null);
            _client = CreateManager("Health test client", prefab, port);
            Assert.That(_client.StartClient(), Is.True);
            yield return WaitFor(() => _client.IsConnectedClient && _client.LocalClient.PlayerObject != null);

            NetworkObject localPlayer = _client.LocalClient.PlayerObject;
            HealthComponent clientHealth = localPlayer.GetComponent<HealthComponent>();
            HealthComponent serverHealth = _host.ConnectedClients[_client.LocalClientId].PlayerObject.GetComponent<HealthComponent>();
            var hudObject = new GameObject("Health test HUD", typeof(RectTransform), typeof(Slider), typeof(CanvasGroup));
            _roots.Add(hudObject);
            PlayerHealthUI hud = hudObject.AddComponent<PlayerHealthUI>();
            Slider bar = hudObject.GetComponent<Slider>();
            CanvasGroup visibility = hudObject.GetComponent<CanvasGroup>();
            Assert.That(bar.value, Is.EqualTo(100f));
            Assert.That(visibility.alpha, Is.EqualTo(1f));

            HealthComponent hostHealth = _host.LocalClient.PlayerObject.GetComponent<HealthComponent>();
            HealthComponent remoteReplica = _client.SpawnManager.SpawnedObjects[hostHealth.NetworkObjectId].GetComponent<HealthComponent>();
            hostHealth.TakeDamageServer(20);
            yield return WaitFor(() => remoteReplica.CurrentHealth == 80);
            Assert.That(bar.value, Is.EqualTo(100f), "A vida de outro jogador não deve alterar o HUD local.");
            Assert.That(clientHealth.TakeDamageServer(99), Is.False);
            Assert.That(clientHealth.HealServer(99), Is.False);
            Assert.That(serverHealth.CurrentHealth, Is.EqualTo(100));

            serverHealth.TakeDamageServer(30);
            yield return WaitFor(() => clientHealth.CurrentHealth == 70 && bar.value == 70f);
            hud.enabled = false;
            Assert.That(visibility.alpha, Is.Zero);
            serverHealth.TakeDamageServer(5);
            yield return WaitFor(() => clientHealth.CurrentHealth == 65);
            Assert.That(bar.value, Is.Zero);
            hud.enabled = true;
            Assert.That(bar.value, Is.EqualTo(65f));

            int serverDeaths = 0;
            int clientDeaths = 0;
            serverHealth.OnDeath += health => serverDeaths++;
            clientHealth.OnDeath += health => clientDeaths++;
            serverHealth.TakeDamageServer(65);
            yield return WaitFor(() => clientHealth.IsDead && bar.value == 0f);
            Assert.That(serverDeaths, Is.EqualTo(1));
            Assert.That(clientDeaths, Is.Zero, "O client observa HP; a decisão de morte pertence ao servidor.");
            _client.Shutdown();
            yield return WaitFor(() => !_client.IsListening && visibility.alpha == 0f);
        }

        private NetworkManager CreateManager(string name, GameObject playerPrefab, ushort port, bool withPool = false)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            _roots.Add(root);
            if (withPool)
            {
                root.AddComponent<ObjectPoolManager>();
            }

            UnityTransport transport = root.AddComponent<UnityTransport>();
            NetworkManager manager = root.AddComponent<NetworkManager>();
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                PlayerPrefab = playerPrefab
            };
            if (withPool)
            {
                root.AddComponent<NetworkObjectPool>();
            }

            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            root.SetActive(true);
            return manager;
        }

        private GameObject SavePrefab(GameObject instance, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, _assetFolder + "/" + name + ".prefab");
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static ushort ReservePort()
        {
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                return (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            }
        }

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, "O estado esperado não chegou em dez segundos.");
        }
    }
}
