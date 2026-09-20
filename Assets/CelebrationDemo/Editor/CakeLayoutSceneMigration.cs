#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CelebrationDemo
{
    /// <summary>
    /// Applies the cake collision and authored interaction layout to the
    /// current prototype scene without rebuilding user-placed objects.
    /// </summary>
    public static class CakeLayoutSceneMigration
    {
        const string ScenePath = "Assets/Scenes/CelebrationPrototype.unity";
        const string GeneratedRootName = "CelebrationPrototypeGenerated";
        const string MarkerName = "Cake Collision And Interaction Layout V1";
        const float CakeCenterZ = 2.4f;
        const float CakeFootprintScale = 2.43f;
        const float CakeInteractionRadius = 3.15f * CakeFootprintScale;

        /// <summary>Batch-mode entry point used to update the authored scene.</summary>
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

            AddCakeColliders(generated.transform);
            MoveTargets(generated.transform);

            if (generated.transform.Find(MarkerName) == null)
            {
                var marker = new GameObject(MarkerName);
                marker.transform.SetParent(generated.transform, false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Applied cake collision and interaction layout migration.");
        }

        static void AddCakeColliders(Transform generated)
        {
            for (int i = 1; i <= 6; i++)
            {
                var sector = generated.Find("Cake Sector " + i + " Base");
                if (sector == null) continue;

                var mesh = sector.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) continue;

                var collider = sector.GetComponent<MeshCollider>();
                if (collider == null) collider = sector.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
                collider.convex = false;
                collider.isTrigger = false;
            }
        }

        static void MoveTargets(Transform generated)
        {
            var targets = generated.GetComponentsInChildren<TargetView>(true);
            foreach (var target in targets)
            {
                if (target.Spec == null) continue;

                Vector3 position;
                if (TryGetQueuePosition(target.Spec.Id, out position))
                {
                    target.transform.position = position;
                    continue;
                }

                if (TryGetCakePosition(target.Spec.Id, out position))
                    target.transform.position = position;
            }
        }

        static bool TryGetQueuePosition(string id, out Vector3 position)
        {
            switch (id)
            {
                case "apple_pile": position = new Vector3(-16f, .55f, 2.5f); return true;
                case "banana_pile": position = new Vector3(-12f, .55f, 2.5f); return true;
                case "orange_pile": position = new Vector3(-16f, .55f, -1.5f); return true;
                case "egg_pile": position = new Vector3(-12f, .55f, -1.5f); return true;
                case "cut": position = new Vector3(-16f, .65f, -6f); return true;
                case "whip": position = new Vector3(-12f, .65f, -6f); return true;
                case "sliced_fruit": position = new Vector3(-16f, .48f, -11.5f); return true;
                case "cream_pile": position = new Vector3(-12f, .48f, -11.5f); return true;
                case "apple_pile_right": position = new Vector3(16f, .55f, 2.5f); return true;
                case "banana_pile_right": position = new Vector3(12f, .55f, 2.5f); return true;
                case "orange_pile_right": position = new Vector3(16f, .55f, -1.5f); return true;
                case "egg_pile_right": position = new Vector3(12f, .55f, -1.5f); return true;
                case "cut_right": position = new Vector3(16f, .65f, -6f); return true;
                case "whip_right": position = new Vector3(12f, .65f, -6f); return true;
                case "sliced_fruit_right": position = new Vector3(16f, .48f, -11.5f); return true;
                case "cream_pile_right": position = new Vector3(12f, .48f, -11.5f); return true;
                case "chopsticks": position = new Vector3(-16f, .65f, 6.5f); return true;
                case "celebration": position = new Vector3(16f, .8f, 6.5f); return true;
                default: position = Vector3.zero; return false;
            }
        }

        static bool TryGetCakePosition(string id, out Vector3 position)
        {
            int sector;
            switch (id)
            {
                case "cake_fruit_0": sector = 0; break;
                case "cake_cream_0": sector = 1; break;
                case "cake_fruit_1": sector = 2; break;
                case "cake_cream_1": sector = 3; break;
                case "cake_fruit_2": sector = 4; break;
                case "cake_cream_2": sector = 5; break;
                default: position = Vector3.zero; return false;
            }

            float angle = sector * Mathf.PI / 3f;
            position = new Vector3(Mathf.Cos(angle) * CakeInteractionRadius, 1.2f,
                CakeCenterZ + Mathf.Sin(angle) * CakeInteractionRadius);
            return true;
        }
    }
}
#endif
