using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameYunit.Spells;
using UnityEditor;
using UnityEngine;

namespace GameYunit.KitBridge.Editor
{
    /// <summary>
    /// Cria as magias e o catálogo, e vincula a arte dos pacotes da Asset Store.
    /// </summary>
    /// <remarks>
    /// A arte vem de pacotes ignorados pelo Git (EULA e tamanho), então as referências precisam ser
    /// religadas em cada máquina. Por isso a vinculação é um passo separado e repetível: as definições
    /// são criadas mesmo sem arte, e <see cref="LinkArt"/> casa ícones e VFX por palavra-chave assim que
    /// os pacotes forem importados, sem sobrescrever o que já estiver preenchido à mão.
    /// </remarks>
    public static class SpellSetup
    {
        private const string SpellsFolder = "Assets/_Project/Spells";
        private const string CatalogPath = SpellsFolder + "/CatalogoDeMagias.asset";

        /// <summary>
        /// Perfil de cada magia. Os números seguem um orçamento parecido de dano por segundo no caso
        /// ideal (~27 a 35), com trocas diferentes: quem bate mais forte acerta menos alvos ou recarrega
        /// mais devagar; quem recarrega rápido tem alcance curto.
        /// </summary>
        private sealed class Profile
        {
            public string Id, Name, Description;
            public SpellShape Shape;
            public float Range, Radius, ConeAngle, Height, Cooldown, CastDelay, Lifetime, EffectOffset;
            public int Damage, MaxTargets;
            public Color Tint;
            public string[] Keywords;
        }

        private static readonly Profile[] Profiles =
        {
            new Profile
            {
                Id = "bola-de-fogo", Name = "Bola de Fogo",
                Description = "Explosão concentrada a distância. Dano alto em poucos alvos.",
                Shape = SpellShape.Sphere, Range = 9f, Radius = 2.2f, Height = 3f,
                Damage = 26, MaxTargets = 3, Cooldown = 2.2f, CastDelay = 0.35f, Lifetime = 3f,
                EffectOffset = 0.6f, Tint = new Color(1f, 0.45f, 0.15f),
                Keywords = new[] { "fire", "flame", "fogo", "burn", "explosion" }
            },
            new Profile
            {
                Id = "nova-de-gelo", Name = "Nova de Gelo",
                Description = "Estoura em volta de você. Dano menor, mas sem limite de alvos.",
                Shape = SpellShape.Sphere, Range = 0f, Radius = 4.5f, Height = 3.5f,
                Damage = 14, MaxTargets = 0, Cooldown = 3f, CastDelay = 0.3f, Lifetime = 2.5f,
                EffectOffset = 0.1f, Tint = new Color(0.45f, 0.8f, 1f),
                Keywords = new[] { "ice", "frost", "gelo", "snow", "water" }
            },
            new Profile
            {
                Id = "relampago", Name = "Relâmpago",
                Description = "Cone elétrico à frente. Acerta vários inimigos em linha.",
                Shape = SpellShape.Cone, Range = 8f, Radius = 1f, ConeAngle = 70f, Height = 3f,
                Damage = 18, MaxTargets = 5, Cooldown = 2.6f, CastDelay = 0.3f, Lifetime = 2f,
                EffectOffset = 0.8f, Tint = new Color(0.75f, 0.7f, 1f),
                Keywords = new[] { "lightning", "thunder", "electric", "shock", "raio" }
            },
            new Profile
            {
                Id = "impacto-arcano", Name = "Impacto Arcano",
                Description = "Golpe curto e rápido. Recarga baixa, alcance pequeno.",
                Shape = SpellShape.Sphere, Range = 4f, Radius = 2.4f, Height = 3f,
                Damage = 15, MaxTargets = 2, Cooldown = 1.1f, CastDelay = 0.25f, Lifetime = 1.8f,
                EffectOffset = 0.5f, Tint = new Color(0.8f, 0.45f, 1f),
                Keywords = new[] { "arcane", "magic", "void", "dark", "purple", "energy" }
            }
        };

        [MenuItem("GameYunit/Preparação/Preparar magias")]
        public static void PrepareSpells()
        {
            Directory.CreateDirectory(SpellsFolder);
            AssetDatabase.Refresh();
            var catalog = AssetDatabase.LoadAssetAtPath<SpellCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SpellCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var spells = new List<SpellDefinition>();
            foreach (var profile in Profiles)
            {
                string path = $"{SpellsFolder}/{profile.Id}.asset";
                var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>(path);
                if (spell == null)
                {
                    spell = ScriptableObject.CreateInstance<SpellDefinition>();
                    AssetDatabase.CreateAsset(spell, path);
                }
                Apply(spell, profile);
                spells.Add(spell);
            }
            var serialized = new SerializedObject(catalog);
            var array = serialized.FindProperty("_spells");
            array.arraySize = spells.Count;
            for (int i = 0; i < spells.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = spells[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpellSetup] {spells.Count} magias preparadas em {SpellsFolder}.");
            LinkArt();
        }

        /// <summary>Escreve só os números; ícone e VFX ficam para <see cref="LinkArt"/>.</summary>
        private static void Apply(SpellDefinition spell, Profile profile)
        {
            var serialized = new SerializedObject(spell);
            serialized.FindProperty("_id").stringValue = profile.Id;
            serialized.FindProperty("_displayName").stringValue = profile.Name;
            serialized.FindProperty("_description").stringValue = profile.Description;
            serialized.FindProperty("_tint").colorValue = profile.Tint;
            serialized.FindProperty("_shape").enumValueIndex = (int)profile.Shape;
            serialized.FindProperty("_range").floatValue = profile.Range;
            serialized.FindProperty("_radius").floatValue = profile.Radius;
            if (profile.ConeAngle > 0f) serialized.FindProperty("_coneAngle").floatValue = profile.ConeAngle;
            serialized.FindProperty("_height").floatValue = profile.Height;
            serialized.FindProperty("_damage").intValue = profile.Damage;
            serialized.FindProperty("_maxTargets").intValue = profile.MaxTargets;
            serialized.FindProperty("_cooldown").floatValue = profile.Cooldown;
            serialized.FindProperty("_castDelay").floatValue = profile.CastDelay;
            serialized.FindProperty("_effectLifetime").floatValue = profile.Lifetime;
            serialized.FindProperty("_effectHeightOffset").floatValue = profile.EffectOffset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spell);
        }

        [MenuItem("GameYunit/Preparação/Vincular arte das magias")]
        public static void LinkArt()
        {
            var icons = LoadByType<Sprite>("t:Sprite");
            var effects = LoadEffectPrefabs();
            if (icons.Count == 0 && effects.Count == 0)
            {
                Debug.LogWarning("[SpellSetup] Nenhum ícone ou VFX encontrado. Importe os pacotes por " +
                                 "GameYunit → Preparação → Importar efeitos/ícones de magia e repita.");
                return;
            }
            int linkedIcons = 0, linkedEffects = 0;
            var usedIcons = new HashSet<string>();
            var usedEffects = new HashSet<string>();
            foreach (var profile in Profiles)
            {
                var spell = AssetDatabase.LoadAssetAtPath<SpellDefinition>($"{SpellsFolder}/{profile.Id}.asset");
                if (spell == null) continue;
                var serialized = new SerializedObject(spell);
                var iconProperty = serialized.FindProperty("_icon");
                var effectProperty = serialized.FindProperty("_effectPrefab");
                // Não sobrescreve escolha manual: só preenche o que ainda está vazio.
                if (iconProperty.objectReferenceValue == null)
                {
                    var icon = BestMatch(icons, profile.Keywords, usedIcons);
                    if (icon.Value != null)
                    {
                        iconProperty.objectReferenceValue = icon.Value;
                        usedIcons.Add(icon.Key);
                        linkedIcons++;
                    }
                }
                if (effectProperty.objectReferenceValue == null)
                {
                    var effect = BestMatch(effects, profile.Keywords, usedEffects);
                    if (effect.Value != null)
                    {
                        effectProperty.objectReferenceValue = effect.Value;
                        usedEffects.Add(effect.Key);
                        linkedEffects++;
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(spell);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpellSetup] Arte vinculada: {linkedIcons} ícone(s) e {linkedEffects} efeito(s) " +
                      $"entre {icons.Count} sprite(s) e {effects.Count} prefab(s) disponíveis.");
        }

        /// <summary>Candidatos fora de `_Project` e do kit: só o que veio dos pacotes de magia.</summary>
        private static Dictionary<string, T> LoadByType<T>(string filter) where T : UnityEngine.Object
        {
            var result = new Dictionary<string, T>();
            foreach (string guid in AssetDatabase.FindAssets(filter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/3DGamekit") || path.StartsWith("Assets/_Project") ||
                    path.StartsWith("Assets/TextMesh Pro") || !path.StartsWith("Assets/")) continue;
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null && !result.ContainsKey(path)) result[path] = asset;
            }
            return result;
        }

        private static Dictionary<string, GameObject> LoadEffectPrefabs()
        {
            var result = new Dictionary<string, GameObject>();
            foreach (var entry in LoadByType<GameObject>("t:Prefab"))
            {
                // Só prefabs que realmente emitem partículas servem como efeito de magia.
                if (entry.Value.GetComponentInChildren<ParticleSystem>(true) == null) continue;
                result[entry.Key] = entry.Value;
            }
            return result;
        }

        /// <summary>Melhor candidato por palavra-chave no caminho, evitando repetir o mesmo asset.</summary>
        private static KeyValuePair<string, T> BestMatch<T>(Dictionary<string, T> candidates,
            string[] keywords, HashSet<string> used) where T : UnityEngine.Object
        {
            KeyValuePair<string, T> best = default;
            int bestScore = 0;
            foreach (var entry in candidates)
            {
                if (used.Contains(entry.Key)) continue;
                string haystack = entry.Key.ToLowerInvariant();
                int score = 0;
                for (int i = 0; i < keywords.Length; i++)
                    if (haystack.Contains(keywords[i]))
                        score += keywords.Length - i; // a primeira palavra-chave pesa mais
                if (score > bestScore)
                {
                    bestScore = score;
                    best = entry;
                }
            }
            return bestScore > 0 ? best : default;
        }
    }
}
