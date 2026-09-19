#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CelebrationDemo
{
    /// <summary>
    /// Applies the working Chinese sign font and adds missing sign text in
    /// place, without rebuilding the authored scene or moving user-adjusted
    /// objects.
    /// </summary>
    [InitializeOnLoad]
    static class SignTextSceneMigration
    {
        const string ScenePath = "Assets/Scenes/CelebrationPrototype.unity";
        const string GeneratedRootName = "CelebrationPrototypeGenerated";
        const string MarkerName = "Sign Text Layout V2";
        const string FontPath = "Assets/ThirdParty/kenney_ui-pack/Font/Kenney Future.ttf";

        static SignTextSceneMigration()
        {
            EditorApplication.update += TryMigrate;
        }

        // Allows a one-shot editor invocation to apply the same migration when
        // the interactive editor is unavailable or has not reloaded scripts.
        public static void RunForBatch()
        {
            if (!File.Exists(ScenePath)) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply(scene);
            EditorApplication.Exit(0);
        }

        static void TryMigrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(ScenePath))
                return;

            var sceneText = File.ReadAllText(ScenePath);
            if (sceneText.Contains(MarkerName))
            {
                EditorApplication.update -= TryMigrate;
                return;
            }

            var active = EditorSceneManager.GetActiveScene();
            if (active.IsValid() && active.isDirty)
                return;

            EditorApplication.update -= TryMigrate;
            try
            {
                var scene = active.path == ScenePath
                    ? active
                    : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Apply(scene);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        static void Apply(UnityEngine.SceneManagement.Scene scene)
        {
            var generated = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == GeneratedRootName);
            if (generated == null) return;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            ApplyExistingLabel(generated.transform, "Donation Zone Sign Label", "捐赠区", font);
            ApplyExistingLabel(generated.transform, "Processing Zone Sign Label", "加工区", font);
            ApplyExistingLabel(generated.transform, "Finished Zone Sign Label", "成品区", font);
            ApplyExistingLabel(generated.transform, "Trading Zone Sign Label", "交易区", font);

            AddBoardLabel(generated.transform, "Home 1 Sign [1号家园]", "1号家园", font);
            AddBoardLabel(generated.transform, "Home 2 Sign [2号家园]", "2号家园", font);
            AddBoardLabel(generated.transform, "Home 3 Sign [3号家园]", "3号家园", font);
            AddBoardLabel(generated.transform, "Shop Sign [鸡蛋商店]", "鸡蛋商店", font);

            if (generated.transform.Find(MarkerName) == null)
            {
                var marker = new GameObject(MarkerName);
                marker.transform.SetParent(generated.transform, false);
            }

            var celebration = FindCelebrationTarget(generated.transform);
            if (celebration != null && generated.transform.Find("Celebration Sign Board") == null)
                AddCelebrationBoard(generated.transform, celebration.transform.position, font);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Applied sign font and added home, shop, and celebration sign text.");
        }

        static void ApplyExistingLabel(Transform root, string objectName, string label, Font font)
        {
            var labelObject = FindChild(root, objectName);
            if (labelObject == null) return;
            var text = labelObject.GetComponent<TextMesh>() ?? labelObject.gameObject.AddComponent<TextMesh>();
            ConfigureText(text, label, font);
        }

        static void AddBoardLabel(Transform root, string boardName, string label, Font font)
        {
            var board = FindChild(root, boardName);
            if (board == null) return;
            var labelObject = board.Find(boardName + " Label");
            if (labelObject == null)
            {
                var child = new GameObject(boardName + " Label");
                child.transform.SetParent(board, false);
                child.transform.localPosition = new Vector3(0f, 0f, .15f);
                child.transform.localRotation = Quaternion.identity;
                labelObject = child.transform;
            }

            var text = labelObject.GetComponent<TextMesh>() ?? labelObject.gameObject.AddComponent<TextMesh>();
            ConfigureText(text, label, font);
        }

        static void AddCelebrationBoard(Transform root, Vector3 targetPosition, Font font)
        {
            // The authored scene already has a celebration banner attached to
            // the trigger. Reuse it so the label follows the user's current
            // placement instead of adding a second sign in front of the camera.
            if (FindChild(root, "Banner") != null)
            {
                AddBoardLabel(root, "Banner", "庆典开始", font);
                return;
            }

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Celebration Sign Board";
            board.transform.SetParent(root, false);
            board.transform.position = targetPosition + Vector3.up * 3.25f + Vector3.back * .2f;
            board.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            board.transform.localScale = new Vector3(3.2f, .86f, .16f);
            var collider = board.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);

            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CelebrationDemo/World/GeneratedMaterials/DarkWood.mat");
            var renderer = board.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;

            var labelObject = new GameObject("Celebration Sign Label");
            labelObject.transform.SetParent(board.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, .15f);
            labelObject.transform.localRotation = Quaternion.identity;
            ConfigureText(labelObject.AddComponent<TextMesh>(), "庆典开始", font);
        }

        static void ConfigureText(TextMesh text, string label, Font font)
        {
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = .1f;
            text.color = Color.white;
            if (font != null) text.font = font;
        }

        static TargetView FindCelebrationTarget(Transform root)
        {
            return root.GetComponentsInChildren<TargetView>(true)
                .FirstOrDefault(target => target.Spec != null &&
                    target.Spec.Kind == TargetKind.Celebration);
        }

        static Transform FindChild(Transform root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName);
        }
    }
}
#endif
