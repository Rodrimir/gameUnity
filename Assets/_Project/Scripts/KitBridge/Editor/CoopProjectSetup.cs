using System;
using System.IO;
using System.Linq;
using GameYunit.Network;
using GameYunit.Characters;
using GameYunit.Combat;
using GameYunit.Spells;
using GameYunit.UI;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace GameYunit.KitBridge.Editor
{
    public static class CoopProjectSetup
    {
        public const string MenuPath = "Assets/_Project/Scenes/CoopMenu.unity";
        public const string LevelPath = "Assets/_Project/Scenes/Level1_Coop.unity";
        public const string PlayerPath = "Assets/_Project/Prefabs/EllenCoop.prefab";
        private const string ControllerPath = "Assets/_Project/Prefabs/EllenCoop.controller";
        private const string CatalogPath = "Assets/_Project/Spells/CatalogoDeMagias.asset";
        private const string CharactersPath = "Assets/_Project/Characters/CatalogoDePersonagens.asset";

        [MenuItem("GameYunit/Preparação/Preparar cooperativo")]
        public static void Prepare()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Saia de Play antes de preparar o cooperativo.");
            Directory.CreateDirectory("Assets/_Project/Prefabs");
            CopyAnimator();
            SpellSetup.PrepareSpells();
            CharacterSetup.PrepareCharacters();
            if (!File.Exists(PlayerPath)) CreatePlayerPrefab();
            if (!File.Exists(LevelPath)) CreateLevel();
            if (!File.Exists(MenuPath)) CreateMenu();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(LevelPath, true),
                new EditorBuildSettingsScene("Assets/_Project/Scenes/Start.unity", true),
                new EditorBuildSettingsScene("Assets/_Project/Scenes/Level1.unity", true)
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MenuPath);
            Debug.Log("[CoopProjectSetup] Cooperativo preparado. Abra GameYunit → Jogar cooperativo.");
        }

        private static void CopyAnimator()
        {
            if (File.Exists(ControllerPath)) return;
            string source = "Assets/3DGamekit/Art/Animations/Animators/Characters/Ellen.controller";
            string text = File.ReadAllText(source);
            foreach (string script in new[] { "EllenStaffEffect", "EllenRespawnEffect", "EllenSetTargetableSMB" })
            {
                string oldGuid = AssetDatabase.AssetPathToGUID($"Assets/3DGamekit/Scripts/Game/Player/StateMachineBehaviour/{script}.cs");
                string newGuid = AssetDatabase.AssetPathToGUID($"Assets/_Project/Scripts/KitBridge/Player/{script}.cs");
                if (string.IsNullOrEmpty(oldGuid) || string.IsNullOrEmpty(newGuid)) throw new InvalidOperationException($"Script indisponível: {script}");
                text = text.Replace(oldGuid, newGuid);
            }
            File.WriteAllText(ControllerPath, text);
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceSynchronousImport);
        }

        public static void RebuildPlayerPrefab()
        {
            CopyAnimator();
            CreatePlayerPrefab();
            AssetDatabase.SaveAssets();
        }

        private static void CreatePlayerPrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3DGamekit/Prefabs/Characters/Ellen/Ellen.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            player.name = "EllenCoop";
            var original = player.GetComponent<Gamekit3D.PlayerController>();
            var controller = player.AddComponent<PlayerController>();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(original), controller);
            controller.meleeWeapon = original.meleeWeapon;
            controller.footstepPlayer = original.footstepPlayer;
            controller.hurtAudioPlayer = original.hurtAudioPlayer;
            controller.landingPlayer = original.landingPlayer;
            controller.emoteLandingPlayer = original.emoteLandingPlayer;
            controller.emoteDeathPlayer = original.emoteDeathPlayer;
            controller.emoteAttackPlayer = original.emoteAttackPlayer;
            controller.emoteJumpPlayer = original.emoteJumpPlayer;
            controller.cameraSettings = null;
            controller.canAttack = true;
            UnityEngine.Object.DestroyImmediate(original);
            UnityEngine.Object.DestroyImmediate(player.GetComponent<global::PlayerInput>());
            player.AddComponent<PlayerInput>();
            var damageable = player.GetComponent<Gamekit3D.Damageable>();
            damageable.onDamageMessageReceivers.Clear();
            damageable.enabled = true;
            damageable.maxHitPoints = 100;
            var distributor = player.GetComponent<Gamekit3D.TargetDistributor>();
            if (distributor != null) UnityEngine.Object.DestroyImmediate(distributor);
            foreach (var weapon in player.GetComponentsInChildren<Gamekit3D.MeleeWeapon>(true))
            {
                weapon.enabled = true;
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                weapon.targetLayers = enemyLayer >= 0 ? 1 << enemyLayer : ~0;
                weapon.hitParticlePrefab = null;
            }
            player.AddComponent<NetworkObject>();
            var networkTransform = player.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;
            networkTransform.PositionThreshold = 0.01f;
            networkTransform.RotAngleThreshold = 0.5f;
            var animator = player.GetComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var networkAnimator = player.AddComponent<NetworkAnimator>();
            networkAnimator.Animator = animator;
            networkAnimator.AuthorityMode = NetworkAnimator.AuthorityModes.Owner;
            player.AddComponent<NetworkPlayerIdentity>();
            player.AddComponent<CoopPlayer>();
            player.AddComponent<NetworkPlayerCombat>();
            // Vida, piscar e barra antes do conjurador, que depende de NetworkHealth nos alvos.
            player.AddComponent<NetworkHealth>();
            player.AddComponent<DamageFlash>();
            player.AddComponent<HealthBar>();
            var caster = player.AddComponent<SpellCaster>();
            var casterSerialized = new SerializedObject(caster);
            casterSerialized.FindProperty("_catalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SpellCatalog>(CatalogPath);
            casterSerialized.ApplyModifiedPropertiesWithoutUndo();
            var coopSerialized = new SerializedObject(player.GetComponent<CoopPlayer>());
            coopSerialized.FindProperty("_characters").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CharactersPath);
            coopSerialized.ApplyModifiedPropertiesWithoutUndo();
            var label = new GameObject("Nome do jogador", typeof(TextMeshPro));
            label.transform.SetParent(player.transform, false);
            label.transform.localPosition = new Vector3(0, 2.4f, 0);
            var text = label.GetComponent<TextMeshPro>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 2.5f;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(5, 1);
            text.text = "Explorador";
            text.textWrappingMode = TextWrappingModes.NoWrap;
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            UnityEngine.Object.DestroyImmediate(player);
        }

        private static void CreateLevel()
        {
            // Reproduzível em cada clone: o cenário grande fica local, com GUID estável.
            File.Copy("Assets/3DGamekit/Scenes/GamePlay/Level1.unity", LevelPath);
            File.WriteAllText(LevelPath + ".meta", "fileFormatVersion: 2\nguid: b67ecc304a284152a7b22fb2b47abf20\nDefaultImporter:\n  externalObjects: {}\n");
            AssetDatabase.ImportAsset(LevelPath, ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.OpenScene(LevelPath);
            var singlePlayer = UnityEngine.Object.FindFirstObjectByType<Gamekit3D.PlayerController>();
            Vector3 start = singlePlayer.transform.position;
            Quaternion rotation = singlePlayer.transform.rotation;
            string[] removeRoots = { "SceneController", "TransitionStart", "TransitionDestination", "ScreenFader", "MenuCanvases", "HealthCanvas", "Level01InformationZones", "FPSCounter", "Ellen", "Enemies", "Level01Gameplay", "Destructibles", "DeathVolume", "Level01Sequences" };
            foreach (var root in scene.GetRootGameObjects())
                if (removeRoots.Contains(root.name)) UnityEngine.Object.DestroyImmediate(root);
            var camera = UnityEngine.Object.FindFirstObjectByType<Gamekit3D.CameraSettings>();
            camera.follow = camera.lookAt = null;
            camera.allowRuntimeCameraSettingsChanges = true;
            foreach (var axis in camera.GetComponentsInChildren<CinemachineInputAxisController>(true)) axis.enabled = false;
            var spawns = new GameObject("Coop Spawn Points");
            for (int slot = 0; slot < ConnectionRules.MaxPlayers; slot++)
            {
                var spawn = new GameObject($"Spawn {slot + 1}");
                spawn.transform.SetParent(spawns.transform);
                Vector3 position = start + Vector3.right * (slot == 0 ? -1.2f : 1.2f);
                if (Physics.Raycast(position + Vector3.up * 5, Vector3.down, out var hit, 10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) position.y = hit.point.y + 0.1f;
                spawn.transform.SetPositionAndRotation(position, rotation);
                spawn.AddComponent<PlayerSpawnPoint>().Configure(slot);
            }
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = new Color(0.025f, 0.06f, 0.09f);
            var root = new GameObject("Cooperative Session", typeof(UnityTransport), typeof(NetworkManager));
            var session = root.AddComponent<CoopSession>();
            var serialized = new SerializedObject(session);
            serialized.FindProperty("_playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            serialized.FindProperty("_catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SpellCatalog>(CatalogPath);
            serialized.FindProperty("_characters").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CharactersPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<CoopMenu>();
            root.AddComponent<CoopSmokeProbe>();
            EditorSceneManager.SaveScene(scene, MenuPath);
        }

        /// <summary>
        /// Religa prefab e cena do menu às magias. Necessário porque <see cref="Prepare"/> só cria o que
        /// falta, e nas máquinas onde EllenCoop e CoopMenu já existiam eles não conhecem o catálogo.
        /// </summary>
        [MenuItem("GameYunit/Preparação/Atualizar cooperativo")]
        public static void UpgradeForSpells()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Saia de Play antes de atualizar o cooperativo.");
            SpellSetup.PrepareSpells();
            CharacterSetup.PrepareCharacters();
            RebuildPlayerPrefab();
            var catalog = AssetDatabase.LoadAssetAtPath<SpellCatalog>(CatalogPath);
            var scene = EditorSceneManager.OpenScene(MenuPath);
            var session = UnityEngine.Object.FindFirstObjectByType<CoopSession>();
            if (session == null) throw new InvalidOperationException("CoopMenu não tem CoopSession.");
            var serialized = new SerializedObject(session);
            serialized.FindProperty("_catalog").objectReferenceValue = catalog;
            serialized.FindProperty("_characters").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CharactersPath);
            serialized.FindProperty("_playerPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CoopProjectSetup] Cooperativo atualizado para magias: prefab, catálogo e CoopMenu.");
        }

        [MenuItem("GameYunit/Jogar cooperativo")]
        public static void PlayCoop()
        {
            EditorSceneManager.OpenScene(MenuPath);
            GameYunit.Editor.EditorEntryPoints.ConfigureGameView();
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("GameYunit/Build/Cliente Linux Development")]
        public static void BuildLinux()
        {
            Directory.CreateDirectory("Builds/Linux");
            const BuildTarget target = BuildTarget.StandaloneLinux64;
            bool previousDefault = PlayerSettings.GetUseDefaultGraphicsAPIs(target);
            var previousApis = PlayerSettings.GetGraphicsAPIs(target);
            try
            {
                // Backend já usado pelo Editor Linux; evita compilar também milhares de variantes Vulkan.
                PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.OpenGLCore });
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MenuPath, LevelPath },
                    locationPathName = "Builds/Linux/GameYunit.x86_64",
                    target = target,
                    options = BuildOptions.Development
                });
                if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException($"Build falhou: {result.summary.result}");
                Debug.Log($"[CoopProjectSetup] Build Linux OpenGL concluído: {result.summary.totalSize} bytes.");
            }
            finally
            {
                PlayerSettings.SetGraphicsAPIs(target, previousApis);
                PlayerSettings.SetUseDefaultGraphicsAPIs(target, previousDefault);
            }
        }
    }
}
