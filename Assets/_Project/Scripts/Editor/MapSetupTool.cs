using GameYunit.Map;
using UnityEditor;
using UnityEngine;

namespace GameYunit.Editor
{
    /// <summary>
    /// Prepara um mapa importado (FBX/prefab) para o gameplay atual: colisão estática para jogadores,
    /// inimigos, projéteis e amostragem de spawn, além de pontos de nascimento dos jogadores.
    /// </summary>
    /// <remarks>Uso: arraste o mapa para a cena, selecione a raiz e rode GameYunit → Mapa → Preparar mapa selecionado.</remarks>
    public static class MapSetupTool
    {
        private const string MenuPath = "GameYunit/Mapa/Preparar mapa selecionado";

        private const StaticEditorFlags MapStaticFlags =
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
            StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.ReflectionProbeStatic;

        [MenuItem(MenuPath, validate = true)]
        private static bool CanPrepareSelectedMap() => Selection.activeGameObject != null &&
            !EditorUtility.IsPersistent(Selection.activeGameObject);

        [MenuItem(MenuPath)]
        private static void PrepareSelectedMap()
        {
            GameObject root = Selection.activeGameObject;
            Undo.SetCurrentGroupName("Preparar mapa");
            int undoGroup = Undo.GetCurrentGroup();

            int colliders = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            {
                GameObject go = filter.gameObject;
                if (filter.sharedMesh == null || !go.TryGetComponent(out MeshRenderer _))
                {
                    continue;
                }

                Undo.RecordObject(go, "Marcar estático");
                GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | MapStaticFlags);

                // Respeita colliders feitos à mão (caixas simples são mais baratos que MeshCollider).
                if (go.TryGetComponent(out Collider _))
                {
                    continue;
                }

                MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(go);
                meshCollider.sharedMesh = filter.sharedMesh;
                colliders++;
            }

            // SkinnedMeshRenderer não gera MeshCollider útil; avisamos em vez de adivinhar.
            int skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true).Length;

            int spawnPoints = Object.FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None).Length;
            if (spawnPoints == 0)
            {
                CreateDefaultSpawnPoints(root);
                spawnPoints = 2;
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[MapSetupTool] '{root.name}': {colliders} MeshCollider(s) adicionados, " +
                $"{spawnPoints} PlayerSpawnPoint(s) na cena.", root);
            if (skinned > 0)
            {
                Debug.LogWarning($"[MapSetupTool] {skinned} SkinnedMeshRenderer(s) ignorados; adicione colliders manualmente se forem cenário.", root);
            }
        }

        /// <summary>Cria dois pontos lado a lado no centro do mapa, apoiados no chão encontrado por raycast.</summary>
        private static void CreateDefaultSpawnPoints(GameObject root)
        {
            Bounds bounds = CalculateBounds(root);
            Physics.SyncTransforms();

            for (int slot = 0; slot < 2; slot++)
            {
                Vector3 position = bounds.center + Vector3.right * (slot == 0 ? -1.5f : 1.5f);
                Vector3 rayOrigin = new Vector3(position.x, bounds.max.y + 1f, position.z);
                position.y = Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, bounds.size.y + 2f,
                    ~0, QueryTriggerInteraction.Ignore)
                    ? hit.point.y
                    : bounds.min.y;

                var go = new GameObject($"PlayerSpawnPoint_{slot}");
                Undo.RegisterCreatedObjectUndo(go, "Criar spawn point");
                go.transform.position = position;
                PlayerSpawnPoint point = go.AddComponent<PlayerSpawnPoint>();
                var serialized = new SerializedObject(point);
                serialized.FindProperty("_slot").intValue = slot;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Debug.Log("[MapSetupTool] Spawn points criados no centro do mapa; confira se caíram numa área aberta.");
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.one);
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
