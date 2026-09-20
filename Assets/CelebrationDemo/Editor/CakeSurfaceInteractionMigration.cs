#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CelebrationDemo
{
    /// <summary>Moves cake F targets into the cake and enlarges fruit patches.</summary>
    public static class CakeSurfaceInteractionMigration
    {
        const string ScenePath = "Assets/Scenes/CelebrationPrototype.unity";
        const string GeneratedRootName = "CelebrationPrototypeGenerated";
        const string MarkerName = "Cake Surface Interaction V1";
        const float CakeCenterZ = 2.4f;
        const float CakeFootprintScale = 2.43f;
        const float CakeInteractionRadius = 2.45f * CakeFootprintScale;

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
                Debug.Log("Cake surface interaction is already applied.");
                return;
            }

            foreach (var target in generated.GetComponentsInChildren<TargetView>(true))
            {
                if (target.Spec == null ||
                    (target.Spec.Kind != TargetKind.CakeFruit && target.Spec.Kind != TargetKind.CakeCream))
                    continue;

                int sector = SectorFor(target.Spec.Id);
                if (sector < 0) continue;
                float angle = sector * Mathf.PI / 3f;
                target.transform.position = new Vector3(
                    Mathf.Cos(angle) * CakeInteractionRadius,
                    1.2f,
                    CakeCenterZ + Mathf.Sin(angle) * CakeInteractionRadius);

                var marker = target.transform.Find("TargetMarker");
                if (marker != null) marker.localPosition = Vector3.up * .42f;
                if (target.Spec.Kind != TargetKind.CakeFruit) continue;

                for (int i = 0; i < 3; i++)
                {
                    Vector3 offset = FruitOffset(sector, i);
                    string stateName = i == 0 ? "StateVisual" : "StateVisual " + (i + 1);
                    var state = target.transform.Find(stateName);
                    if (state != null)
                    {
                        state.localPosition = Vector3.up * .42f + offset;
                        state.localScale = new Vector3(.92f, .04f, .92f);
                    }

                    string fruitName = i == 0 ? "FruitDecoration" : "FruitDecoration " + (i + 1);
                    var fruit = target.transform.Find(fruitName);
                    if (fruit != null)
                    {
                        fruit.localPosition = Vector3.up * .52f + offset;
                        fruit.localScale = new Vector3(.90f, .21f, .90f);
                    }
                }
            }

            var markerObject = new GameObject(MarkerName);
            markerObject.transform.SetParent(generated.transform, false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Applied cake surface interaction layout.");
        }

        static int SectorFor(string id)
        {
            switch (id)
            {
                case "cake_fruit_0": return 0;
                case "cake_cream_0": return 1;
                case "cake_fruit_1": return 2;
                case "cake_cream_1": return 3;
                case "cake_fruit_2": return 4;
                case "cake_cream_2": return 5;
                default: return -1;
            }
        }

        static Vector3 FruitOffset(int sector, int index)
        {
            Vector3 radial = new Vector3(
                Mathf.Cos(sector * Mathf.PI / 3f), 0f,
                Mathf.Sin(sector * Mathf.PI / 3f));
            Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
            switch (index)
            {
                case 0: return -radial * 1.169f - tangent * .975f;
                case 1: return -radial * 3.441f + tangent * .080f;
                default: return -radial * 1.970f + tangent * 1.172f;
            }
        }
    }
}
#endif
