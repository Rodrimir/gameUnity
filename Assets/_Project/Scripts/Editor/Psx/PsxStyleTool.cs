using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameYunit.Editor
{
    /// <summary>
    /// Menus para forçar o estilo retrô em assets fora das pastas automáticas (ex.: um mapa importado):
    /// reimportação de texturas/áudios com as regras PSX e conversão de materiais para "GameYunit/PSX Lit".
    /// </summary>
    /// <remarks>Aceita como seleção: pastas, assets (texturas, áudios, materiais, modelos) e objetos da cena.</remarks>
    public static class PsxStyleTool
    {
        private const string ShaderName = "GameYunit/PSX Lit";
        private const string ImportMenu = "GameYunit/PSX/Aplicar importação retrô (texturas e áudio) na seleção";
        private const string MaterialMenu = "GameYunit/PSX/Converter materiais da seleção para PSX Lit";

        [MenuItem(ImportMenu)]
        private static void ApplyRetroImport()
        {
            int textures = 0;
            int audios = 0;
            foreach (string path in CollectAssetPaths("t:Texture2D t:AudioClip"))
            {
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (PsxImportRules.IsIgnored(importer))
                {
                    continue;
                }

                if (importer is TextureImporter texture)
                {
                    PsxImportRules.ApplyTexture(texture);
                    texture.SaveAndReimport();
                    textures++;
                }
                else if (importer is AudioImporter audio)
                {
                    PsxImportRules.ApplyAudio(audio, path);
                    audio.SaveAndReimport();
                    audios++;
                }
            }

            Debug.Log($"[PsxStyleTool] Reimportados no estilo PSX: {textures} textura(s), {audios} áudio(s).");
        }

        [MenuItem(MaterialMenu)]
        private static void ConvertMaterials()
        {
            Shader psx = Shader.Find(ShaderName);
            if (psx == null)
            {
                Debug.LogError($"[PsxStyleTool] Shader '{ShaderName}' não encontrado (Assets/_Project/Art/Shaders/PSXLit.shader).");
                return;
            }

            var materials = new HashSet<Material>();
            foreach (string path in CollectAssetPaths("t:Material"))
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Material material)
                    {
                        materials.Add(material);
                    }
                }
            }

            foreach (GameObject go in Selection.gameObjects)
            {
                foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(includeInactive: true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null)
                        {
                            materials.Add(material);
                        }
                    }
                }
            }

            int converted = 0;
            int readOnly = 0;
            var textures = new HashSet<Texture>();
            foreach (Material material in materials)
            {
                // Materiais embutidos no FBX são somente leitura: extraia-os no Model Importer (Materials → Extract).
                if (AssetDatabase.IsSubAsset(material) || !AssetDatabase.IsOpenForEdit(material))
                {
                    readOnly++;
                    continue;
                }

                if (material.shader == psx)
                {
                    continue;
                }

                string textureProperty = material.HasProperty("_BaseMap") ? "_BaseMap" :
                    material.HasProperty("_MainTex") ? "_MainTex" : null;
                Texture texture = textureProperty != null ? material.GetTexture(textureProperty) : null;
                Vector2 scale = textureProperty != null ? material.GetTextureScale(textureProperty) : Vector2.one;
                Vector2 offset = textureProperty != null ? material.GetTextureOffset(textureProperty) : Vector2.zero;
                Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                    material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

                Undo.RecordObject(material, "Converter para PSX Lit");
                material.shader = psx;
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
                material.SetColor("_BaseColor", color);
                EditorUtility.SetDirty(material);
                converted++;

                if (texture != null)
                {
                    textures.Add(texture);
                }
            }

            // As texturas usadas pelos materiais convertidos também recebem filtro Point e limite de 256 px.
            foreach (Texture texture in textures)
            {
                if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) is TextureImporter importer &&
                    !PsxImportRules.IsIgnored(importer))
                {
                    PsxImportRules.ApplyTexture(importer);
                    importer.SaveAndReimport();
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PsxStyleTool] {converted} material(is) convertido(s), {textures.Count} textura(s) ajustada(s).");
            if (readOnly > 0)
            {
                Debug.LogWarning($"[PsxStyleTool] {readOnly} material(is) embutido(s) em modelos foram ignorados. " +
                    "No Model Importer, use Materials → Extract Materials e rode o menu de novo.");
            }
        }

        /// <summary>Assets selecionados diretamente, os contidos em pastas selecionadas e as dependências de objetos da cena.</summary>
        private static IEnumerable<string> CollectAssetPaths(string filter)
        {
            var paths = new HashSet<string>();
            var folders = new List<string>();
            foreach (Object selected in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    folders.Add(path);
                }
                else
                {
                    paths.Add(path);
                }
            }

            if (folders.Count > 0)
            {
                foreach (string guid in AssetDatabase.FindAssets(filter, folders.ToArray()))
                {
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
            }

            // Objetos de cena e modelos: inclui as texturas/áudios/materiais de que dependem.
            foreach (Object dependency in EditorUtility.CollectDependencies(Selection.gameObjects))
            {
                string path = AssetDatabase.GetAssetPath(dependency);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }

            // Mantém apenas os tipos pedidos, inclusive entre os assets selecionados diretamente.
            bool wantsTextures = filter.Contains("Texture2D");
            bool wantsAudio = filter.Contains("AudioClip");
            bool wantsMaterials = filter.Contains("Material");
            foreach (string path in paths)
            {
                System.Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type == null)
                {
                    continue;
                }

                bool isModel = AssetImporter.GetAtPath(path) is ModelImporter;
                if ((wantsTextures && typeof(Texture2D).IsAssignableFrom(type)) ||
                    (wantsAudio && typeof(AudioClip).IsAssignableFrom(type)) ||
                    (wantsMaterials && (typeof(Material).IsAssignableFrom(type) || isModel)))
                {
                    yield return path;
                }
            }
        }
    }
}
