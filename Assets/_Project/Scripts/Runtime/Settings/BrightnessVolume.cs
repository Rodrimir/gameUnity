using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameYunit.Settings
{
    /// <summary>
    /// Aplica o brilho escolhido por um Volume global próprio.
    /// </summary>
    /// <remarks>
    /// O URP não tem um controle de gama global por script, então o ajuste vira um override de
    /// <see cref="ColorAdjustments"/>. O Volume é criado em runtime, com prioridade alta e perfil próprio,
    /// para não alterar o perfil de volume do projeto nem os volumes do cenário do kit.
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class BrightnessVolume : MonoBehaviour
    {
        private ColorAdjustments _adjustments;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindFirstObjectByType<BrightnessVolume>() != null) return;
            var go = new GameObject("Brilho (GameYunit)");
            DontDestroyOnLoad(go);
            go.AddComponent<BrightnessVolume>();
        }

        private void Awake()
        {
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Brilho (runtime)";
            _adjustments = profile.Add<ColorAdjustments>(true);
            _adjustments.postExposure.overrideState = true;
            volume.sharedProfile = profile;
            GameSettings.Applied += Apply;
            Apply();
        }

        private void Apply()
        {
            if (_adjustments == null) return;
            // Brilho de 0,5 a 1,6 vira exposição em paradas de luz: 1,0 fica neutro em 0 EV.
            _adjustments.postExposure.value = Mathf.Log(Mathf.Max(0.01f, GameSettings.Brightness), 2f) * 2f;
        }

        private void OnDestroy() => GameSettings.Applied -= Apply;
    }
}
