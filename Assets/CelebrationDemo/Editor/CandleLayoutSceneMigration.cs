#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CelebrationDemo
{
    /// <summary>Expands the authored cake candle cluster without rebuilding the scene.</summary>
    public static class CandleLayoutSceneMigration
    {
        const string ScenePath = "Assets/Scenes/CelebrationPrototype.unity";
        const string GeneratedRootName = "CelebrationPrototypeGenerated";
        const string MarkerName = "Cake Candle Cluster V1";

        static readonly Vector3[] CandleOffsets =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(-.65f, 0f, -.45f),
            new Vector3(.65f, 0f, -.45f),
            new Vector3(-.7f, 0f, .5f),
            new Vector3(.7f, 0f, .5f)
        };

        public static void RunForBatch()
        {
            if (!File.Exists(ScenePath))
            {
                EditorApplication.Exit(1);
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply(scene);
            EditorApplication.Exit(0);
        }

        static void Apply(Scene scene)
        {
            var generated = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == GeneratedRootName);
            if (generated == null)
            {
                Debug.LogError("Could not find " + GeneratedRootName + " in " + ScenePath);
                return;
            }
            if (generated.transform.Find(MarkerName) != null)
            {
                Debug.Log("Cake candle cluster is already applied.");
                return;
            }

            var candle = generated.transform.Find("Cake Candle");
            var flame = generated.transform.Find("Cake Flame");
            if (candle == null || flame == null)
            {
                Debug.LogError("Cake Candle or Cake Flame is missing.");
                return;
            }

            for (int i = 0; i < CandleOffsets.Length; i++)
            {
                string suffix = i == 0 ? string.Empty : " " + (i + 1);
                var currentCandle = i == 0 ? candle : CloneVisual(candle, "Cake Candle" + suffix);
                var currentFlame = i == 0 ? flame : CloneVisual(flame, "Cake Flame" + suffix);
                currentCandle.localPosition = new Vector3(CandleOffsets[i].x, 2.27f, 2.4f + CandleOffsets[i].z);
                currentCandle.localScale = new Vector3(.18f, .7f, .18f);
                currentFlame.localPosition = new Vector3(CandleOffsets[i].x, 3.13f, 2.4f + CandleOffsets[i].z);
                currentFlame.localScale = new Vector3(.3f, .45f, .3f);
            }

            var marker = new GameObject(MarkerName);
            marker.transform.SetParent(generated.transform, false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Applied enlarged cake candle cluster.");
        }

        static Transform CloneVisual(Transform source, string name)
        {
            var clone = Object.Instantiate(source.gameObject, source.parent);
            clone.name = name;
            return clone.transform;
        }
    }
}
#endif
