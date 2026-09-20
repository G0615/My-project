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

            if (generated.transform.Find(MarkerName) != null)
            {
                Debug.Log("Cake collision and interaction layout is already applied.");
                return;
            }

            AddCakeColliders(generated.transform);
            MoveTargets(generated.transform);
            ArrangeCakeVisuals(generated.transform);
            MoveZoneSigns(generated.transform);

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

        static void ArrangeCakeVisuals(Transform generated)
        {
            var targets = generated.GetComponentsInChildren<TargetView>(true);
            foreach (var target in targets)
            {
                if (target.Spec == null ||
                    (target.Spec.Kind != TargetKind.CakeFruit && target.Spec.Kind != TargetKind.CakeCream))
                    continue;

                Vector3 direction = new Vector3(target.transform.position.x, 0f,
                    target.transform.position.z - CakeCenterZ);
                if (direction.sqrMagnitude < .001f) continue;

                Vector3 attached = -direction.normalized * 1.45f + Vector3.up * .42f;
                var marker = target.transform.Find("TargetMarker");
                if (marker != null) marker.localPosition = attached;

                if (target.Spec.Kind != TargetKind.CakeFruit) continue;

                Vector3 radial = direction.normalized;
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                Vector3[] offsets =
                {
                    -radial * 1.169f - tangent * .975f,
                    -radial * 3.441f + tangent * .080f,
                    -radial * 1.970f + tangent * 1.172f
                };
                var state = target.transform.Find("StateVisual");
                if (state == null) continue;

                for (int i = 0; i < offsets.Length; i++)
                {
                    string stateName = i == 0 ? "StateVisual" : "StateVisual " + (i + 1);
                    var slot = target.transform.Find(stateName);
                    if (slot == null)
                    {
                        var clone = Object.Instantiate(state.gameObject, target.transform);
                        clone.name = stateName;
                        slot = clone.transform;
                    }
                    slot.localPosition = attached + offsets[i];
                    slot.localRotation = Quaternion.identity;
                    slot.localScale = new Vector3(.92f, .05f, .92f);
                    slot.gameObject.SetActive(false);
                }

                for (int i = 0; i < offsets.Length; i++)
                {
                    var fruitName = i == 0 ? "FruitDecoration" : "FruitDecoration " + (i + 1);
                    var fruit = target.transform.Find(fruitName);
                    if (fruit == null) continue;
                    fruit.localPosition = attached + Vector3.up * .10f + offsets[i];
                    fruit.localScale = new Vector3(.90f, .21f, .90f);
                }
            }
        }

        static void MoveZoneSigns(Transform generated)
        {
            // The queue targets were authored five units farther back. Move
            // each matching sign group together so the labels stay with the
            // interaction rows, including the relocated trading/celebration
            // targets at the two outer queue ends.
            MoveSignGroup(generated, "Donation Zone Sign", 0f, -5f);
            MoveSignGroup(generated, "Processing Zone Sign", 0f, -5f);
            MoveSignGroup(generated, "Finished Zone Sign", 0f, -5f);
            MoveSignGroup(generated, "Donation Zone Sign Right", 0f, -5f);
            MoveSignGroup(generated, "Processing Zone Sign Right", 0f, -5f);
            MoveSignGroup(generated, "Finished Zone Sign Right", 0f, -5f);
            MoveSignGroup(generated, "Trading Zone Sign", -13f, -5f);
            MoveSignGroup(generated, "Celebration Sign", 13f, -5f);
        }

        static void MoveSignGroup(Transform generated, string prefix, float deltaX, float deltaZ)
        {
            var children = generated.GetComponentsInChildren<Transform>(true);
            foreach (var child in children)
            {
                if (!child.name.StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                child.position += new Vector3(deltaX, 0f, deltaZ);
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
