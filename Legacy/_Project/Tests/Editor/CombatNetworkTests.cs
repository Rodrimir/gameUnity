using System;
using System.Collections;
using System.Collections.Generic;
using GameYunit.Combat;
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
    public sealed class CombatNetworkTests
    {
        private string _assetFolder;
        private GameObject _managerObject;
        private GameObject _ground;
        private NetworkManager _manager;
        private NetworkObjectPool _networkPool;
        private GameObject _enemyPrefab;
        private AutoAttackSystem _attack;

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
        public IEnumerator StraightShotHitsNearestEnemyAndRecyclesProjectiles()
        {
            yield return StartHost(CreateWeapon("Straight", w =>
            {
                w.FindProperty("_fireInterval").floatValue = 0.05f;
                w.FindProperty("_damage").floatValue = 7f;
            }));

            var hits = RecordHits();

            // Nenhum alvo: a arma não dispara.
            yield return new WaitForSeconds(0.2f);
            Assert.That(_attack.ActiveServerProjectileCount, Is.Zero);

            EnemyAI enemy = SpawnEnemy(new Vector3(6f, 0f, 0f));
            Assert.That(EnemyAI.ActiveOnServer, Has.Member(enemy));

            yield return WaitFor(() => hits.Count > 0);
            Assert.That(hits[0].Target, Is.SameAs(enemy.gameObject));
            Assert.That(hits[0].Damage, Is.EqualTo(7f));

            // Sem alvos, os projéteis restantes colidem ou expiram e voltam ao pool.
            enemy.ReturnToPoolServer();
            Assert.That(EnemyAI.ActiveOnServer, Is.Empty);
            yield return WaitFor(() => _attack.ActiveServerProjectileCount == 0);

            foreach (Projectile projectile in Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                Assert.That(projectile.GetComponent<PooledObject>().IsInPool, Is.True);
                Assert.That(projectile.gameObject.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator FanBurstFiresConfiguredProjectileCountPerVolley()
        {
            yield return StartHost(CreateWeapon("Fan", w =>
            {
                w.FindProperty("_fireInterval").floatValue = 10f;
                w.FindProperty("_projectileCount").intValue = 5;
                w.FindProperty("_spreadAngle").floatValue = 60f;
            }));

            var volleys = new List<ProjectileVolley>();
            _attack.OnVolleyFired += volleys.Add;
            SpawnEnemy(new Vector3(0f, 0f, 8f));

            yield return WaitFor(() => volleys.Count == 1);
            Assert.That(volleys[0].Count, Is.EqualTo(5));
            Assert.That(_attack.ActiveServerProjectileCount, Is.EqualTo(5));

            // As pontas do leque abrem 30° para cada lado da direção central.
            Assert.That(Vector3.Angle(volleys[0].GetDirection(0), volleys[0].Direction), Is.EqualTo(30f).Within(0.01f));
            Assert.That(Vector3.Angle(volleys[0].GetDirection(4), volleys[0].Direction), Is.EqualTo(30f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator PiercingShotHitsAlignedEnemiesWithFalloff()
        {
            yield return StartHost(CreateWeapon("Pierce", w =>
            {
                w.FindProperty("_fireInterval").floatValue = 10f;
                w.FindProperty("_damage").floatValue = 20f;
                w.FindProperty("_projectileSpeed").floatValue = 30f;
                w.FindProperty("_pierceCount").intValue = 1;
                w.FindProperty("_pierceDamageFalloff").floatValue = 0.5f;
            }));

            var hits = RecordHits();
            EnemyAI near = SpawnEnemy(new Vector3(4f, 0f, 0f));
            EnemyAI far = SpawnEnemy(new Vector3(7f, 0f, 0f));

            yield return WaitFor(() => hits.Count >= 2);
            Assert.That(hits[0].Target, Is.SameAs(near.gameObject));
            Assert.That(hits[0].Damage, Is.EqualTo(20f));
            Assert.That(hits[1].Target, Is.SameAs(far.gameObject));
            Assert.That(hits[1].Damage, Is.EqualTo(10f));
        }

        [UnityTest]
        public IEnumerator HomingShotCurvesTowardTargetOutsideAim()
        {
            // Dispara para a frente (+Z) com o alvo a 90°: só acerta se o teleguiado funcionar.
            yield return StartHost(CreateWeapon("Homing", w =>
            {
                w.FindProperty("_fireInterval").floatValue = 10f;
                w.FindProperty("_aim").enumValueIndex = (int)WeaponDefinition.AimMode.PlayerFacing;
                w.FindProperty("_projectileSpeed").floatValue = 12f;
                w.FindProperty("_projectileLifetime").floatValue = 3f;
                w.FindProperty("_homingTurnRate").floatValue = 720f;
            }));

            var hits = RecordHits();
            EnemyAI enemy = SpawnEnemy(new Vector3(6f, 0f, 0f));

            yield return WaitFor(() => hits.Count > 0);
            Assert.That(hits[0].Target, Is.SameAs(enemy.gameObject));
        }

        [UnityTest]
        public IEnumerator OwnerCanSelectStartingWeaponThroughServer()
        {
            WeaponDefinition first = CreateWeapon("First", _ => { });
            WeaponDefinition second = CreateWeapon("Second", _ => { });
            yield return StartHost(first, second);

            WeaponDefinition changed = null;
            _attack.OnWeaponChanged += weapon => changed = weapon;
            Assert.That(_attack.CurrentWeapon, Is.SameAs(first));

            _attack.RequestWeapon(1);
            yield return WaitFor(() => _attack.CurrentWeaponIndex == 1);
            Assert.That(_attack.CurrentWeapon, Is.SameAs(second));
            Assert.That(changed, Is.SameAs(second));

            // Índice inválido é recusado pelo servidor.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Troca de arma recusada"));
            _attack.RequestWeapon(7);
            yield return null;
            Assert.That(_attack.CurrentWeaponIndex, Is.EqualTo(1));
        }

        private WeaponDefinition CreateWeapon(string name, Action<SerializedObject> configure)
        {
            EnsureAssetFolder();
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(weapon, $"{_assetFolder}/Weapon_{name}.asset");
            var serialized = new SerializedObject(weapon);
            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return weapon;
        }

        private IEnumerator StartHost(params WeaponDefinition[] weapons)
        {
            EnsureAssetFolder();

            // Primitiva com SphereCollider: o Projectile deve desligar o próprio collider.
            GameObject projectileTemplate = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileTemplate.transform.localScale = Vector3.one * 0.3f;
            projectileTemplate.AddComponent<Projectile>();
            GameObject projectilePrefab = SavePrefab(projectileTemplate, "Projectile");

            var playerTemplate = new GameObject("Test player", typeof(NetworkObject));
            AutoAttackSystem attackTemplate = playerTemplate.AddComponent<AutoAttackSystem>();
            var serialized = new SerializedObject(attackTemplate);
            serialized.FindProperty("_projectilePrefab").objectReferenceValue = projectilePrefab.GetComponent<Projectile>();
            serialized.FindProperty("_prewarmCount").intValue = 4;
            SerializedProperty weaponList = serialized.FindProperty("_weapons");
            weaponList.arraySize = weapons.Length;
            for (int i = 0; i < weapons.Length; i++)
            {
                weaponList.GetArrayElementAtIndex(i).objectReferenceValue = weapons[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject playerPrefab = SavePrefab(playerTemplate, "Player");

            var enemyTemplate = new GameObject("Test enemy");
            enemyTemplate.AddComponent<EnemyAI>();
            CharacterController capsule = enemyTemplate.GetComponent<CharacterController>();
            capsule.height = 2f;
            capsule.center = Vector3.up;
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
            _manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                PlayerPrefab = playerPrefab
            };
            transport.SetConnectionData("127.0.0.1", 0, "127.0.0.1");
            _managerObject.SetActive(true);
            _manager.AddNetworkPrefab(_enemyPrefab);
            Assert.That(_networkPool.RegisterPrefab(_enemyPrefab.GetComponent<NetworkObject>(), 2), Is.True);
            Assert.That(_manager.StartHost(), Is.True);
            yield return null;

            _attack = _manager.LocalClient.PlayerObject.GetComponent<AutoAttackSystem>();
            Assert.That(_attack, Is.Not.Null);
        }

        private EnemyAI SpawnEnemy(Vector3 position)
        {
            NetworkObject enemy = _networkPool.Spawn(_enemyPrefab.GetComponent<NetworkObject>(), position, Quaternion.identity);
            Assert.That(enemy, Is.Not.Null);
            return enemy.GetComponent<EnemyAI>();
        }

        private List<(GameObject Target, float Damage)> RecordHits()
        {
            var hits = new List<(GameObject Target, float Damage)>();
            _attack.OnServerProjectileHit += (target, point, damage) => hits.Add((target, damage));
            return hits;
        }

        private void EnsureAssetFolder()
        {
            if (!string.IsNullOrEmpty(_assetFolder))
            {
                return;
            }

            string folderName = "__GameYunitCombatTest_" + Guid.NewGuid().ToString("N");
            _assetFolder = "Assets/" + folderName;
            AssetDatabase.CreateFolder("Assets", folderName);
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
