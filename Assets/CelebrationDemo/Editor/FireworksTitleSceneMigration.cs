#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CelebrationDemo
{
    /// <summary>
    /// Adds an authored, disabled fireworks title to the persistent prototype
    /// scene without rebuilding or moving any user-adjusted scene objects.
    /// </summary>
    [InitializeOnLoad]
    public static class FireworksTitleSceneMigration
    {
        const string ScenePath = "Assets/Scenes/CelebrationPrototype.unity";
        const string GeneratedRootName = "CelebrationPrototypeGenerated";
        const string MarkerName = "HappyBirthday Scene Template V1";
        const string FontPath = "Assets/ThirdParty/kenney_ui-pack/Font/Kenney Future.ttf";

        static FireworksTitleSceneMigration()
        {
            EditorApplication.update += TryMigrate;
        }

        /// <summary>One-shot entry point for applying the migration in batch mode.</summary>
        public static void RunForBatch()
        {
            if (!File.Exists(ScenePath))
            {
                EditorApplication.Exit(0);
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply(scene);
            EditorApplication.Exit(0);
        }

        static void TryMigrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ScenePath))
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

        static void Apply(Scene scene)
        {
            var generated = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == GeneratedRootName);
            if (generated == null) return;

            var root = FindChild(generated.transform, "庆典烟花");
            if (root == null)
            {
                var rootObject = new GameObject("庆典烟花");
                rootObject.transform.SetParent(generated.transform, false);
                root = rootObject.transform;
            }

            var fireworks = root.GetComponent<CelebrationFireworks>();
            if (fireworks == null) fireworks = root.gameObject.AddComponent<CelebrationFireworks>();

            var title = root.Find("HappyBirthday");
            bool createdTitle = false;
            if (title == null)
            {
                var titleObject = new GameObject("HappyBirthday");
                titleObject.transform.SetParent(root, false);
                title = titleObject.transform;
                title.localPosition = new Vector3(0f, 6.4f, 2.2f);
                title.localRotation = Quaternion.Euler(0f, 0f, 0f);
                createdTitle = true;
            }

            var text = title.GetComponent<TextMesh>();
            if (text == null) text = title.gameObject.AddComponent<TextMesh>();
            if (string.IsNullOrEmpty(text.text)) text.text = "Happy\nBirthday";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            if (createdTitle || text.fontSize <= 0) text.fontSize = 64;
            if (createdTitle || text.characterSize <= 0f) text.characterSize = .12f;
            text.fontStyle = FontStyle.Bold;
            if (createdTitle || text.color.a <= 0f) text.color = new Color(1f, .86f, .25f, 1f);
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (text.font == null && font != null) text.font = font;
            fireworks.BirthdayText = text;

            // The complete authored template stays hidden at rest. The runtime
            // clones it and enables only the clone during the fireworks phase.
            title.gameObject.SetActive(false);
            root.gameObject.SetActive(false);

            if (generated.transform.Find(MarkerName) == null)
            {
                var marker = new GameObject(MarkerName);
                marker.transform.SetParent(generated.transform, false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Added disabled HappyBirthday scene template for Inspector editing.");
        }

        static Transform FindChild(Transform root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName);
        }
    }
}
#endif
