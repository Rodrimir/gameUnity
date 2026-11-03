#if UNITY_EDITOR
using System.IO;
using System.Linq;
using GameYunit.KitBridge;
using GameYunit.Network;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace GameYunit.KitBridge.Editor
{
    /// <summary>Converte os dois assets importados em inimigos da primeira onda cooperativa.</summary>
    public static class EnemyProjectSetup
    {
        private const string Output = "Assets/_Project/Prefabs/Enemies";
        private const string CyberSource = "Assets/Cyber Monsters 2/Prefab/Cyber Monsters 2.prefab";
        private const string SkeletonSource = "Assets/DungeonCharacters/Skeletons_demo/DungeonSkeleton_demoPrefab.prefab";

        public static void PrepareImportedEnemies()
        {
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            GameObject cyber = CreateEnemyPrefab(CyberSource, Output + "/CyberMonster.prefab", 60, 2.4f, 2.0f, 12);
            GameObject skeleton = CreateEnemyPrefab(SkeletonSource, Output + "/DungeonSkeleton.prefab", 35, 2.0f, 1.6f, 10);
            ConfigureNetworkManager(cyber, skeleton);
            ConfigureLevel(cyber, skeleton);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[EnemyProjectSetup] CyberMonster e DungeonSkeleton preparados e registrados no co-op.");
        }

        public static void PrepareCombat()
        {
            CoopProjectSetup.RebuildPlayerPrefab();
            PrepareImportedEnemies();
            AssetDatabase.SaveAssets();
            Debug.Log("[EnemyProjectSetup] Combate, hit kill e respawn preparados.");
        }

        private static GameObject CreateEnemyPrefab(string sourcePath, string outputPath, int health, float speed, float range, int damage)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new System.InvalidOperationException("Asset não encontrado: " + sourcePath);
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(outputPath));
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
            visual.transform.SetParent(root.transform, false);
            try
            {
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                if (enemyLayer >= 0) root.layer = enemyLayer;
                string controllerPath = outputPath.Replace(".prefab", ".controller");
                var enemyController = CreateController(controllerPath, sourcePath);
                var visualAnimator = root.GetComponentInChildren<Animator>(true);
                if (visualAnimator == null)
                {
                    var animator = root.AddComponent<Animator>();
                    animator.runtimeAnimatorController = enemyController;
                }
                else
                {
                    visualAnimator.runtimeAnimatorController = enemyController;
                    var networkAnimator = AddOrGet<NetworkAnimator>(visualAnimator.gameObject);
                    networkAnimator.Animator = visualAnimator;
                }

                AddOrGet<NetworkObject>(root);
                AddOrGet<NetworkTransform>(root);
                // O prefab visual importado não recebe componentes do kit. A ponte de dano
                // será adicionada quando a fase de combate criar o hitbox por variante.
                var enemy = AddOrGet<NetworkEnemy>(root);
                SetPrivate(enemy, "_maxHealth", health);
                SetPrivate(enemy, "_moveSpeed", speed);
                SetPrivate(enemy, "_attackRange", range);
                SetPrivate(enemy, "_attackDamage", damage);
                PrefabUtility.SaveAsPrefabAsset(root, outputPath);
                return AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RuntimeAnimatorController CreateController(string path, string sourcePath)
        {
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            string clipDirectory = sourcePath.Contains("DungeonCharacters")
                ? "Assets/DungeonCharacters/Skeletons_demo/animation"
                : "Assets/Cyber Monsters 2/Animation";
            var clips = AssetDatabase.FindAssets("t:AnimationClip", new[] { clipDirectory })
                .SelectMany(guid => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Find(string text) => clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(text));
            var idle = Find("idle") ?? clips.FirstOrDefault();
            var walk = Find("walk") ?? Find("run") ?? idle;
            var attack = Find("attack") ?? Find("shoot") ?? idle;
            var death = Find("death") ?? idle;
            var layer = controller.layers[0];
            var idleState = layer.stateMachine.AddState("Idle"); idleState.motion = idle;
            var walkState = layer.stateMachine.AddState("Walk"); walkState.motion = walk;
            var attackState = layer.stateMachine.AddState("Attack"); attackState.motion = attack;
            var deathState = layer.stateMachine.AddState("Death"); deathState.motion = death;
            layer.stateMachine.defaultState = idleState;
            var toWalk = idleState.AddTransition(walkState); toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            var toIdle = walkState.AddTransition(idleState); toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            var toAttack = layer.stateMachine.AddAnyStateTransition(attackState); toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack"); toAttack.duration = 0.05f;
            var toDeath = layer.stateMachine.AddAnyStateTransition(deathState); toDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead"); toDeath.duration = 0.05f;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureNetworkManager(GameObject cyber, GameObject skeleton)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/CoopMenu.unity", OpenSceneMode.Single);
            var session = Object.FindFirstObjectByType<CoopSession>();
            if (session == null) throw new System.InvalidOperationException("CoopSession não encontrado em CoopMenu.");
            var serialized = new SerializedObject(session);
            var prefabs = serialized.FindProperty("_enemyPrefabs");
            prefabs.arraySize = 2;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = cyber;
            prefabs.GetArrayElementAtIndex(1).objectReferenceValue = skeleton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureLevel(GameObject cyber, GameObject skeleton)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level1_Coop.unity", OpenSceneMode.Single);
            var directorObject = GameObject.Find("EnemyWaveDirector") ?? new GameObject("EnemyWaveDirector");
            var networkObject = AddOrGet<NetworkObject>(directorObject);
            var director = AddOrGet<EnemyWaveDirector>(directorObject);
            var pointsRoot = GameObject.Find("EnemySpawnPoints") ?? new GameObject("EnemySpawnPoints");
            var playerPoints = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
            var points = new Transform[4];
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject("EnemySpawnPoint_" + i).transform;
                point.SetParent(pointsRoot.transform);
                Vector3 basePosition = playerPoints.Length == 0 ? Vector3.zero : playerPoints[i % playerPoints.Length].transform.position;
                point.position = basePosition + new Vector3(i % 2 == 0 ? 6 : -6, 0, i < 2 ? 5 : -5);
                points[i] = point;
            }
            var serialized = new SerializedObject(director);
            var prefabs = serialized.FindProperty("_enemyPrefabs"); prefabs.arraySize = 2;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = cyber;
            prefabs.GetArrayElementAtIndex(1).objectReferenceValue = skeleton;
            var spawnPoints = serialized.FindProperty("_spawnPoints"); spawnPoints.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
        }

        private static T AddOrGet<T>(GameObject root) where T : Component => root.GetComponent<T>() ?? root.AddComponent<T>();

        private static void SetPrivate(Object target, string property, object value)
        {
            var serialized = new SerializedObject(target);
            var prop = serialized.FindProperty(property);
            if (value is int intValue) prop.intValue = intValue;
            if (value is float floatValue) prop.floatValue = floatValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
