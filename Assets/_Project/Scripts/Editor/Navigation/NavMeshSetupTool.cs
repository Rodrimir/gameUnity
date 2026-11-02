using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace GameYunit.Editor
{
    /// <summary>
    /// Configura e assa o NavMesh de um mapa: um NavMeshSurface na raiz coleta apenas os colliders
    /// dos filhos estáticos. Objetos não estáticos recebem NavMeshModifier "Ignore From Build".
    /// </summary>
    /// <remarks>
    /// <para>"Navigation Static" é uma flag legada; o NavMeshSurface não a lê. Aqui, qualquer flag Static
    /// (inclusive a legada, ou as aplicadas por GameYunit → Mapa → Preparar mapa selecionado) marca o objeto
    /// como obstáculo/piso do NavMesh.</para>
    /// <para>Coletar só os filhos do mapa evita que jogadores e inimigos (CharacterControllers) entrem no bake.</para>
    /// </remarks>
    public static class NavMeshSetupTool
    {
        private const string MenuPath = "GameYunit/Mapa/Configurar e assar NavMesh do mapa selecionado";

        [MenuItem(MenuPath, validate = true)]
        private static bool CanSetup() => Selection.activeGameObject != null &&
            !EditorUtility.IsPersistent(Selection.activeGameObject);

        [MenuItem(MenuPath)]
        private static void SetupAndBake()
        {
            GameObject root = Selection.activeGameObject;
            Undo.SetCurrentGroupName("Configurar NavMesh");
            int undoGroup = Undo.GetCurrentGroup();

            if (!root.TryGetComponent(out NavMeshSurface surface))
            {
                surface = Undo.AddComponent<NavMeshSurface>(root);
            }

            Undo.RecordObject(surface, "Configurar NavMeshSurface");
            surface.collectObjects = CollectObjects.Children;
            // Colliders: o NavMesh coincide com o que os CharacterControllers realmente colidem.
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            int ignored = 0;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                GameObject go = collider.gameObject;
                if (go == root || go.isStatic || collider.isTrigger)
                {
                    continue;
                }

                if (!go.TryGetComponent(out NavMeshModifier modifier))
                {
                    modifier = Undo.AddComponent<NavMeshModifier>(go);
                }

                Undo.RecordObject(modifier, "Ignorar no NavMesh");
                modifier.ignoreFromBuild = true;
                // Afeta só este objeto; filhos estáticos continuam no bake.
                var serialized = new SerializedObject(modifier);
                SerializedProperty applyToChildren = serialized.FindProperty("m_ApplyToChildren");
                if (applyToChildren != null)
                {
                    applyToChildren.boolValue = false;
                    serialized.ApplyModifiedProperties();
                }

                ignored++;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Bake(surface);
            Debug.Log($"[NavMeshSetupTool] NavMesh de '{root.name}' assado. {ignored} objeto(s) não estático(s) ignorado(s).", root);
        }

        private static void Bake(NavMeshSurface surface)
        {
            NavMeshData previous = surface.navMeshData;
            surface.BuildNavMesh();
            NavMeshData data = surface.navMeshData;
            if (data == null)
            {
                Debug.LogError("[NavMeshSetupTool] O bake não gerou dados. O mapa tem colliders estáticos?", surface);
                return;
            }

            // Persiste como asset ao lado da cena, como o botão Bake do inspector faz.
            string path = GetAssetPath(surface);
            if (previous != null && AssetDatabase.Contains(previous))
            {
                path = AssetDatabase.GetAssetPath(previous);
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(data, path);
            surface.navMeshData = data;
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        private static string GetAssetPath(NavMeshSurface surface)
        {
            string scenePath = surface.gameObject.scene.path;
            string folder = string.IsNullOrEmpty(scenePath)
                ? "Assets/_Project/Scenes"
                : Path.Combine(Path.GetDirectoryName(scenePath) ?? "Assets", Path.GetFileNameWithoutExtension(scenePath))
                    .Replace('\\', '/');

            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/') ?? "Assets";
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }

            return AssetDatabase.GenerateUniqueAssetPath($"{folder}/NavMesh-{surface.gameObject.name}.asset");
        }
    }
}
