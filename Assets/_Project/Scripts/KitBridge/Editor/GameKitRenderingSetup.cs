using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameYunit.KitBridge.Editor
{
    /// <summary>Configura os assets URP do projeto usando os efeitos originais do kit.</summary>
    public static class GameKitRenderingSetup
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string RenderingFolder = SettingsFolder + "/Rendering";

        [MenuItem("GameYunit/Preparação/Configurar URP do kit")]
        public static void ConfigureRendering()
        {
            EnsureFolder("Assets/_Project", "Settings");
            EnsureFolder(SettingsFolder, "Rendering");
            string rendererPath = RenderingFolder + "/PC_Renderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "PC_Renderer";
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }

            EnsureFeature<ComplexSkyboxRenderFeature>(renderer);
            var water = EnsureFeature<WaterReflectionRenderFeature>(renderer);
            water.settings.filterSettings.LayerMask = ~LayerMask.GetMask("Water", "WaterGeometry", "Skybox");
            water.settings.overrideMode = WaterReflectionRenderFeature.Settings.OverrideMaterialMode.None;
            EditorUtility.SetDirty(water);
            EnsureFeature<GaussianBlurRenderFeature>(renderer);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(renderer);

            string pipelinePath = RenderingFolder + "/PC_RPAsset.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "PC_RPAsset";
                pipeline.supportsCameraDepthTexture = true;
                pipeline.supportsCameraOpaqueTexture = true;
                pipeline.supportsHDR = true;
                pipeline.shadowDistance = 80f;
                pipeline.shadowCascadeCount = 4;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            var pipelineSettings = new SerializedObject(pipeline);
            var volumeProfile = pipelineSettings.FindProperty("m_VolumeProfile");
            if (volumeProfile.objectReferenceValue == null)
            {
                volumeProfile.objectReferenceValue = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                    "Assets/DefaultVolumeProfile.asset");
                pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssetIfDirty(pipeline);
            Debug.Log("[GameKitRenderingSetup] URP configurado com céu, reflexos de água e blur do kit em assets próprios.");
        }

        private static T EnsureFeature<T>(UniversalRendererData renderer) where T : ScriptableRendererFeature
        {
            var feature = renderer.rendererFeatures.OfType<T>().FirstOrDefault();
            if (feature != null) return feature;
            feature = ScriptableObject.CreateInstance<T>();
            feature.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            return feature;
        }

        private static void EnsureFolder(string parent, string folder)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + folder))
                AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
