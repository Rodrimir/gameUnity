using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GameYunit.KitBridge.Editor
{
    public static class CoopSceneInspection
    {
        public static void Inspect()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level1.unity");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToArray();
            var lines = all.GroupBy(b => b.GetType().FullName).OrderBy(g => g.Key).Select(g => $"{g.Key}: {g.Count()} [{string.Join(", ", g.Take(3).Select(b => b.name))}]").ToList();
            foreach (var p in all.OfType<Gamekit3D.PlayerController>()) lines.Add($"ELLEN {p.transform.position} {p.transform.rotation} animator={AssetDatabase.GetAssetPath(p.GetComponent<Animator>().runtimeAnimatorController)}");
            foreach (var root in scene.GetRootGameObjects()) lines.Add($"ROOT {root.name} ({root.GetComponentsInChildren<Transform>(true).Length})");
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/Fase2-scene-inspection.txt", lines);
        }
    }
}
