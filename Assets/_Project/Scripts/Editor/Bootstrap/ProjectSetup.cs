using System;
using System.IO;
using System.Linq;
using GameYunit.Characters;
using GameYunit.Combat;
using GameYunit.Core.Pooling;
using GameYunit.Enemies;
using GameYunit.Map;
using GameYunit.Network;
using GameYunit.Player;
using GameYunit.Progression;
using GameYunit.Rendering;
using GameYunit.UI;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace GameYunit.Editor
{
    /// <summary>Monta o projeto e conecta a arte aos sistemas existentes, sem implementar o Bloco 4.</summary>
    public static class ProjectSetup
    {
        public const string GameScene = "Assets/_Project/Scenes/Game.unity";
        public const string PreviewScene = "Assets/_Project/Scenes/Characters.unity";
        private const string Art = "Assets/_Project/Art/Models/Characters";
        private const string Prefabs = "Assets/_Project/Prefabs";
        private const string AnimationFolder = "Assets/_Project/Art/Animations";

        [MenuItem("GameYunit/Projeto/Montar protótipo e personagens")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Encerre o Play Mode antes de montar o protótipo.");
            }

            // A montagem inicial é repetível; cenas já editadas precisam ser preservadas.
            if (File.Exists(GameScene) || File.Exists(PreviewScene))
            {
                Debug.Log("[ProjectSetup] Cenas existentes preservadas. Abra Game ou Characters pelo menu Projeto.");
                OpenCharacters();
                return;
            }

            ConfigureProject();
            ConfigureModelImports();
            ConvertMaterials();
            var survivalist = CreateVisual("Survivalist", "Survivalist", 1.8f);
            var adventure = CreateVisual("AdventureFemale", "Adventure Female", 1.8f);
            var gravemind = CreateVisual("Gravemind", "Gravemind Dreadnought", 1.9f);
            var mutant = CreateVisual("Biomechanical", "Biomechanical Mutant", 2f);
            InputActionReference move = CreateInput();
            Projectile projectile = CreateProjectile();
            XpGem gem = CreateGem();
            GameObject player = CreatePlayer(move, projectile, new[] { survivalist, adventure, gravemind });
            EnemyAI enemy = CreateEnemy(mutant, gem);
            CreateGameScene(player, enemy, gem);
            CreatePreviewScene(new[] { survivalist, adventure, gravemind, mutant });
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Projeto montado: três personagens selecionáveis, um monstro e cenas Game/Characters.");
            OpenCharacters();
        }

        private static void ConfigureProject()
        {
            Directory.CreateDirectory("Assets/_Project/Settings/Rendering");
            Directory.CreateDirectory(AnimationFolder);
            AssetDatabase.Refresh();
            PlayerSettings.companyName = "GameYunit";
            PlayerSettings.productName = "GameYunit";
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 2;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, "Assets/_Project/Settings/Rendering/GameRenderer.asset");
            UniversalRenderPipelineAsset pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.name = "GameURP";
            pipeline.msaaSampleCount = 1;
            pipeline.supportsHDR = false;
            AssetDatabase.CreateAsset(pipeline, "Assets/_Project/Settings/Rendering/GameURP.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorSettings.serializationMode = SerializationMode.ForceText;
        }

        private static void ConfigureModelImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Art }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importCameras = false;
                importer.importLights = false;
                importer.addCollider = false;
                if (path.Contains("/Survivalist/") || path.EndsWith("/Adventures_woman.FBX", StringComparison.OrdinalIgnoreCase))
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.sourceAvatar = null;
                }

                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                if (clips.Length == 0)
                {
                    clips = importer.defaultClipAnimations;
                }

                foreach (ModelImporterClipAnimation clip in clips)
                {
                    clip.loopTime = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.lockRootRotation = true;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static void ConvertMaterials()
        {
            Shader shader = Shader.Find("GameYunit/PSX Lit");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                throw new InvalidOperationException("O shader PSX não compilou.");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Art }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || AssetDatabase.IsSubAsset(mat))
                {
                    continue;
                }

                Texture texture = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                if (texture == null && mat.HasProperty("_BaseMap"))
                {
                    texture = mat.GetTexture("_BaseMap");
                }

                // Materiais HDRP/shaders ausentes preservam a textura nos dados serializados,
                // mesmo quando HasProperty não expõe _MainTex ou _BaseColorMap.
                if (texture == null) texture = SavedTexture(mat, "_MainTex") ?? SavedTexture(mat, "_BaseColorMap");

                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                bool hair = mat.name.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0;
                mat.shader = shader;
                mat.shaderKeywords = Array.Empty<string>();
                mat.SetTexture("_BaseMap", texture);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Cutoff", hair ? 0.4f : 0f);
                mat.SetFloat("_Cull", hair ? 0f : 2f);
                mat.renderQueue = -1;
                EditorUtility.SetDirty(mat);
            }
        }

        private static Texture SavedTexture(Material material, string propertyName)
        {
            var serialized = new SerializedObject(material);
            SerializedProperty textures = serialized.FindProperty("m_SavedProperties.m_TexEnvs");
            for (int i = 0; textures != null && i < textures.arraySize; i++)
            {
                SerializedProperty entry = textures.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == propertyName)
                {
                    return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                }
            }
            return null;
        }

        private static GameObject CreateVisual(string model, string displayName, float height)
        {
            string[] candidates = AssetDatabase.FindAssets("t:Prefab", new[] { $"{Art}/{model}/Source" });
            string sourcePath = candidates.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p =>
                model != "Gravemind" || p.EndsWith("/Gravemind.prefab", StringComparison.OrdinalIgnoreCase));
            if (sourcePath == null)
            {
                throw new InvalidOperationException($"Prefab de origem ausente: {model}");
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var root = new GameObject(displayName);
            GameObject instance = Object.Instantiate(source, root.transform);
            instance.name = "Model";
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(instance);
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            foreach (Animation legacy in instance.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(legacy);
            foreach (MonoBehaviour script in instance.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException($"Modelo sem renderer: {model}");
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = height / Mathf.Max(bounds.size.y, 0.01f);
            instance.transform.localScale *= scale;
            instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;

            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            if (animator.avatar == null)
            {
                string modelPath = AssetDatabase.FindAssets("t:Model", new[] { $"{Art}/{model}/Source" })
                    .Select(AssetDatabase.GUIDToAssetPath).First(p => !p.Contains("/Animations/") &&
                        (model != "Gravemind" || p.EndsWith("/Gravemind.fbx", StringComparison.OrdinalIgnoreCase)));
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = CreateController(model);
            if (animator.runtimeAnimatorController != null) root.AddComponent<CharacterAnimationDriver>();

            string folder = $"{Prefabs}/Characters";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{model}Visual.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static AnimatorController CreateController(string model)
        {
            AnimationClip idle;
            AnimationClip walk;
            if (model == "Gravemind")
            {
                idle = FindClip($"{Art}/Gravemind", "Stand_idle2");
                walk = FindClip($"{Art}/Gravemind", "Walk_F");
            }
            else if (model == "Survivalist" || model == "AdventureFemale")
            {
                idle = FindClip($"{Art}/Survivalist", "Stand--Idle");
                walk = FindClip($"{Art}/Survivalist", "Locomotion--Run_N.anim");
            }
            else
            {
                // O pacote do monstro não fornece animações de locomoção.
                return null;
            }

            if (idle == null || walk == null)
            {
                Debug.LogWarning($"[ProjectSetup] Animações compatíveis não encontradas para {model}.");
                return null;
            }

            idle = PrepareClip(idle, model + "Idle");
            walk = PrepareClip(walk, model + "Run");
            var controller = AnimatorController.CreateAnimatorControllerAtPath($"{AnimationFolder}/{model}.controller");
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var blend = new BlendTree { name = "Locomotion", blendParameter = "Speed", useAutomaticThresholds = false };
            blend.AddChild(idle, 0f);
            blend.AddChild(walk, 1f);
            AssetDatabase.AddObjectToAsset(blend, controller);
            AnimatorState state = controller.layers[0].stateMachine.AddState("Locomotion");
            state.motion = blend;
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimationClip FindClip(string folder, string filename)
        {
            string path = AssetDatabase.FindAssets("", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileName(p).StartsWith(filename, StringComparison.OrdinalIgnoreCase));
            return path == null ? null : AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        private static AnimationClip PrepareClip(AnimationClip source, string name)
        {
            AnimationClip clip = Object.Instantiate(source);
            clip.name = name;
            clip.legacy = false;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, $"{AnimationFolder}/{name}.anim");
            return clip;
        }

        private static InputActionReference CreateInput()
        {
            const string path = "Assets/_Project/Settings/Input/Player.inputactions";
            var input = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = input.AddActionMap("Player");
            InputAction move = map.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            File.WriteAllText(path, input.ToJson());
            Object.DestroyImmediate(input);
            AssetDatabase.ImportAsset(path);
            InputActionAsset imported = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            InputActionReference reference = InputActionReference.Create(imported.FindAction("Player/Move", true));
            AssetDatabase.CreateAsset(reference, "Assets/_Project/Settings/Input/Move.asset");
            return reference;
        }

        private static Projectile CreateProjectile()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.localScale = Vector3.one * 0.3f;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Projectile", new Color(1f, 0.65f, 0.15f));
            go.AddComponent<Projectile>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Prefabs}/Projectiles/Projectile.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Projectile>();
        }

        private static XpGem CreateGem()
        {
            var go = new GameObject("XpGem");
            go.AddComponent<XpGem>();
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * 0.3f;
            visual.transform.localRotation = Quaternion.Euler(0, 0, 45);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = CreateMaterial("XpGem", new Color(0.2f, 1f, 0.55f));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Prefabs}/Pickups/XpGem.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<XpGem>();
        }

        private static GameObject CreatePlayer(InputActionReference move, Projectile projectile, GameObject[] visuals)
        {
            var go = new GameObject("Player");
            go.AddComponent<PlayerController>();
            ConfigureCapsule(go.GetComponent<CharacterController>());
            ConfigureTransform(go.GetComponent<NetworkTransform>());
            Assign(go.GetComponent<PlayerNetwork>(), "_moveAction", move);
            go.AddComponent<PlayerLevel>();
            AutoAttackSystem attack = go.AddComponent<AutoAttackSystem>();
            Assign(attack, "_projectilePrefab", projectile);
            SetObjectArray(attack, "_weapons", new Object[] {
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/ScriptableObjects/Weapons/Weapon_HomingWand.asset"),
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/ScriptableObjects/Weapons/Weapon_FanBurst.asset"),
                AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/ScriptableObjects/Weapons/Weapon_PiercingBolt.asset") });
            PlayerAppearance appearance = go.AddComponent<PlayerAppearance>();
            var serialized = new SerializedObject(appearance);
            SerializedProperty entries = serialized.FindProperty("_characters");
            entries.arraySize = visuals.Length;
            for (int i = 0; i < visuals.Length; i++)
            {
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(visuals[i], go.transform);
                visual.SetActive(i == 0);
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Name").stringValue = DisplayName(visuals[i]);
                entry.FindPropertyRelative("Visual").objectReferenceValue = visual;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Prefabs}/Player/Player.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static EnemyAI CreateEnemy(GameObject visualPrefab, XpGem gem)
        {
            var go = new GameObject("BiomechanicalEnemy");
            EnemyAI enemy = go.AddComponent<EnemyAI>();
            ConfigureCapsule(go.GetComponent<CharacterController>());
            ConfigureTransform(go.GetComponent<NetworkTransform>());
            go.AddComponent<NavMeshSteering>();
            Assign(go.AddComponent<XpDropper>(), "_gemPrefab", gem);
            PrefabUtility.InstantiatePrefab(visualPrefab, go.transform);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{Prefabs}/Enemies/BiomechanicalEnemy.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<EnemyAI>();
        }

        private static void CreateGameScene(GameObject player, EnemyAI enemy, XpGem gem)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject map = CreateArena(60f);
            NavMeshSurface surface = map.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData, "Assets/_Project/Scenes/GameNavMesh.asset");
            for (int i = 0; i < 2; i++)
            {
                var point = new GameObject($"PlayerSpawnPoint_{i}");
                point.transform.position = new Vector3(i == 0 ? -2f : 2f, 0.05f, 0f);
                var so = new SerializedObject(point.AddComponent<PlayerSpawnPoint>());
                so.FindProperty("_slot").intValue = i;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var network = new GameObject("NetworkManager");
            NetworkManager manager = network.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = network.AddComponent<UnityTransport>();
            manager.NetworkConfig.PlayerPrefab = player;
            manager.NetworkConfig.TickRate = 30;
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = player });
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = enemy.gameObject });
            manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = gem.gameObject });
            network.AddComponent<ObjectPoolManager>();
            NetworkObjectPool pool = network.AddComponent<NetworkObjectPool>();
            var ps = new SerializedObject(pool);
            SerializedProperty entries = ps.FindProperty("_prefabs");
            entries.arraySize = 2;
            SetPoolEntry(entries.GetArrayElementAtIndex(0), enemy.GetComponent<NetworkObject>(), 32);
            SetPoolEntry(entries.GetArrayElementAtIndex(1), gem.GetComponent<NetworkObject>(), 64);
            ps.ApplyModifiedPropertiesWithoutUndo();
            network.AddComponent<NetworkManagerSetup>();
            network.AddComponent<XpDropService>();
            network.AddComponent<NetworkDebugHUD>();
            network.AddComponent<WeaponSelectDebugHUD>();
            network.AddComponent<LevelDebugHUD>();
            Assign(network.AddComponent<CharacterSelectDebugHUD>(), "_playerPrefab", player.GetComponent<PlayerAppearance>());

            var spawner = new GameObject("EnemySpawner");
            Assign(spawner.AddComponent<EnemySpawner>(), "_enemyPrefab", enemy);
            Assign(spawner.GetComponent<EnemySpawner>(), "_networkPool", pool);
            Camera camera = CreateCamera(new Vector3(0f, 12f, -8f), new Vector3(0f, 1f, 0f));
            camera.gameObject.AddComponent<CameraController>();
            camera.gameObject.AddComponent<PsxRenderTextureSetup>();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), GameScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScene, true) };
        }

        private static void CreatePreviewScene(GameObject[] visuals)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateArena(18f);
            for (int i = 0; i < visuals.Length; i++)
            {
                Vector3 position = new Vector3((i - 1.5f) * 3.5f, 0f, 0f);
                GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(visuals[i]);
                go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 180, 0));
                var label = new GameObject("Nome");
                label.transform.position = position + new Vector3(0f, 2.65f, 0f);
                TextMesh text = label.AddComponent<TextMesh>();
                text.text = DisplayName(visuals[i]) + (i == 3 ? "\nMonstro" : "\nSelecionável");
                text.fontSize = 48;
                text.characterSize = 0.06f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = i == 3 ? new Color(1f, 0.4f, 0.3f) : new Color(0.6f, 1f, 0.8f);
            }

            CreateCamera(new Vector3(0f, 6f, -15f), new Vector3(0f, 1.4f, 0f));
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), PreviewScene);
        }

        private static GameObject CreateArena(float size)
        {
            var map = new GameObject("Mapa de teste");
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Chão";
            ground.transform.SetParent(map.transform);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(size, 1f, size);
            ground.isStatic = true;
            ground.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Ground", new Color(0.13f, 0.17f, 0.19f));
            var light = new GameObject("Directional Light");
            Light sun = light.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.52f, 0.6f);
            return map;
        }

        private static Camera CreateCamera(Vector3 position, Vector3 target)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Camera camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.05f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 150f;
            camera.fieldOfView = 45f;
            go.transform.position = position;
            go.transform.LookAt(target);
            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>();
            return camera;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"Assets/_Project/Art/Materials/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("GameYunit/PSX Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void ConfigureCapsule(CharacterController controller)
        {
            controller.height = 2f;
            controller.radius = 0.5f;
            controller.center = Vector3.up;
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.08f;
        }

        private static void ConfigureTransform(NetworkTransform transform)
        {
            transform.SyncRotAngleX = false;
            transform.SyncRotAngleZ = false;
            transform.SyncScaleX = false;
            transform.SyncScaleY = false;
            transform.SyncScaleZ = false;
            transform.Interpolate = true;
        }

        private static void Assign(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetPoolEntry(SerializedProperty entry, NetworkObject prefab, int count)
        {
            entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("PrewarmCount").intValue = count;
        }

        private static string DisplayName(GameObject visual)
        {
            switch (visual.name)
            {
                case "SurvivalistVisual": return "Survivalist";
                case "AdventureFemaleVisual": return "Adventure Female";
                case "GravemindVisual": return "Gravemind Dreadnought";
                case "BiomechanicalVisual": return "Biomechanical Mutant";
                default: return visual.name;
            }
        }

        [MenuItem("GameYunit/Projeto/Instalar menu Coop e vídeo")]
        public static void InstallMainMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Encerre o Play Mode antes de instalar o menu.");

            var scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            NetworkManagerSetup network = Object.FindFirstObjectByType<NetworkManagerSetup>();
            if (network == null) throw new InvalidOperationException("NetworkManagerSetup não existe na cena Game.");

            foreach (NetworkDebugHUD component in Object.FindObjectsByType<NetworkDebugHUD>(FindObjectsSortMode.None))
                Object.DestroyImmediate(component);
            foreach (WeaponSelectDebugHUD component in Object.FindObjectsByType<WeaponSelectDebugHUD>(FindObjectsSortMode.None))
                Object.DestroyImmediate(component);
            foreach (CharacterSelectDebugHUD component in Object.FindObjectsByType<CharacterSelectDebugHUD>(FindObjectsSortMode.None))
                Object.DestroyImmediate(component);

            MainMenuHUD menu = network.GetComponent<MainMenuHUD>();
            if (menu == null) menu = network.gameObject.AddComponent<MainMenuHUD>();
            var serialized = new SerializedObject(menu);
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/Player/Player.prefab");
            Camera camera = Camera.main;
            serialized.FindProperty("_network").objectReferenceValue = network;
            serialized.FindProperty("_playerCatalog").objectReferenceValue = player.GetComponent<PlayerAppearance>();
            serialized.FindProperty("_weaponCatalog").objectReferenceValue = player.GetComponent<AutoAttackSystem>();
            serialized.FindProperty("_psx").objectReferenceValue = camera.GetComponent<PsxRenderTextureSetup>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameScene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Menu Coop, seleção inicial e vídeo instalado na cena Game.");
        }

        [MenuItem("GameYunit/Projeto/Abrir cena de jogo")]
        public static void OpenGame() => EditorSceneManager.OpenScene(GameScene);

        [MenuItem("GameYunit/Projeto/Abrir menu em Play Mode")]
        public static void PlayGameForMenuReview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Encerre o Play Mode antes de abrir a revisão do menu.");

            OpenGame();
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                EditorApplication.isPlaying = true;
            };
        }

        [MenuItem("GameYunit/Projeto/Ver personagens")]
        public static void OpenCharacters()
        {
            EditorSceneManager.OpenScene(PreviewScene);
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.LookAt(new Vector3(0f, 1.2f, 0f), Quaternion.Euler(15f, 0f, 0f), 10f);
            }
        }

        public static void OpenAndCaptureCharacters()
        {
            OpenCharacters();
            EditorApplication.delayCall += CaptureCharacters;
        }

        [MenuItem("GameYunit/Projeto/Salvar prévia dos personagens")]
        public static void CaptureCharacters()
        {
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Abra Characters antes de salvar a prévia.");
            var texture = new RenderTexture(1600, 900, 24);
            var screenshot = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                screenshot.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                screenshot.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/Characters.png", screenshot.EncodeToPNG());
                Debug.Log("[ProjectSetup] Prévia salva em Logs/Characters.png.");
            }
            finally
            {
                RenderTexture.active = previous;
                texture.Release();
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(screenshot);
            }
        }
    }
}
