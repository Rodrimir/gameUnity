using System.Collections.Generic;
using System.IO;
using GameYunit.Characters;
using UnityEditor;
using UnityEngine;

namespace GameYunit.KitBridge.Editor
{
    /// <summary>
    /// Cria os personagens jogáveis e o catálogo.
    /// </summary>
    /// <remarks>
    /// Os personagens são variações de aparência sobre o corpo da Ellen porque todos os modelos do kit usam
    /// rig <c>Generic</c> com esqueletos próprios: não há retargeting, e trocar o modelo quebraria as
    /// animações. Os materiais vêm do próprio kit (apenas referenciados, nunca editados), incluindo as
    /// variantes "Dirty" que ele já traz.
    /// </remarks>
    public static class CharacterSetup
    {
        private const string Folder = "Assets/_Project/Characters";
        private const string CatalogPath = Folder + "/CatalogoDePersonagens.asset";
        private const string KitMaterials = "Assets/3DGamekit/Art/Materials/Characters/Ellen/";

        private sealed class Profile
        {
            public string Id, Name, Description, Body, Head, Hair;
            public Color Tint = Color.white;
            public float Scale = 1f;
            public bool PlayerColor;
        }

        private static readonly Profile[] Profiles =
        {
            new Profile
            {
                Id = "ellen", Name = "Ellen", Description = "A exploradora original, na cor do jogador.",
                PlayerColor = true, Scale = 1f
            },
            new Profile
            {
                Id = "ellen-veterana", Name = "Veterana", Description = "Traje desgastado por muitas descidas.",
                Body = "Ellen_Body_Dirty_Mat", Head = "Ellen_Head_Dirty_Mat", Hair = "Ellen_Hair_Mat",
                Tint = new Color(0.78f, 0.74f, 0.70f), Scale = 1f
            },
            new Profile
            {
                Id = "ellen-arcana", Name = "Arcana", Description = "Mais leve e um pouco mais baixa.",
                Body = "Ellen_Body_Mat", Head = "Ellen_Head_Mat", Hair = "Ellen_Hair_Mat",
                Tint = new Color(0.62f, 0.45f, 0.95f), Scale = 0.94f
            },
            new Profile
            {
                Id = "ellen-sentinela", Name = "Sentinela", Description = "Porte maior, presença mais pesada.",
                Body = "Ellen_Body_Mat", Head = "Ellen_Head_Mat", Hair = "Ellen_Hair_Mat",
                Tint = new Color(0.35f, 0.85f, 0.65f), Scale = 1.07f
            }
        };

        [MenuItem("GameYunit/Preparação/Preparar personagens")]
        public static void PrepareCharacters()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var created = new List<CharacterDefinition>();
            foreach (var profile in Profiles)
            {
                string path = $"{Folder}/{profile.Id}.asset";
                var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
                if (character == null)
                {
                    character = ScriptableObject.CreateInstance<CharacterDefinition>();
                    AssetDatabase.CreateAsset(character, path);
                }
                Apply(character, profile);
                created.Add(character);
            }
            var serialized = new SerializedObject(catalog);
            var array = serialized.FindProperty("_characters");
            array.arraySize = created.Count;
            for (int i = 0; i < created.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = created[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterSetup] {created.Count} personagens preparados em {Folder}.");
        }

        private static void Apply(CharacterDefinition character, Profile profile)
        {
            var serialized = new SerializedObject(character);
            serialized.FindProperty("_id").stringValue = profile.Id;
            serialized.FindProperty("_displayName").stringValue = profile.Name;
            serialized.FindProperty("_description").stringValue = profile.Description;
            serialized.FindProperty("_tint").colorValue = profile.Tint;
            serialized.FindProperty("_scale").floatValue = profile.Scale;
            // O perfil da cor do jogador deixa os materiais vazios de propósito: é o que mantém a pintura por cor.
            serialized.FindProperty("_bodyMaterial").objectReferenceValue = profile.PlayerColor ? null : Material(profile.Body);
            serialized.FindProperty("_headMaterial").objectReferenceValue = profile.PlayerColor ? null : Material(profile.Head);
            serialized.FindProperty("_hairMaterial").objectReferenceValue = profile.PlayerColor ? null : Material(profile.Hair);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(character);
        }

        private static Material Material(string name) => string.IsNullOrEmpty(name)
            ? null
            : AssetDatabase.LoadAssetAtPath<Material>(KitMaterials + name + ".mat");
    }
}
