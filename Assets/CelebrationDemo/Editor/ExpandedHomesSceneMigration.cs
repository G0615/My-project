#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace CelebrationDemo
{
    /// <summary>
    /// Migrates the serialized prototype scene once after the expanded-home
    /// layout is introduced. The source scene remains the single source of
    /// truth; this hook only runs while the old scene signature is present.
    /// </summary>
    [InitializeOnLoad]
    static class ExpandedHomesSceneMigration
    {
        const string ScenePath = "Assets/Scenes/CelebrationPrototype.unity";

        static ExpandedHomesSceneMigration()
        {
            EditorApplication.update += TryMigrate;
        }

        static void TryMigrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorSceneManager.GetActiveScene().isDirty)
                return;

            if (!File.Exists(ScenePath) || SceneAlreadyExpanded())
            {
                EditorApplication.update -= TryMigrate;
                return;
            }

            EditorApplication.update -= TryMigrate;
            CelebrationSceneBuilder.Create();
        }

        static bool SceneAlreadyExpanded()
        {
            var text = File.ReadAllText(ScenePath);
            return text.Contains("Banana Pile") && text.Contains("InteractionCollider") &&
                text.Contains("Home 3 Orange Tree") && text.Contains("Cake Sector 1 Base") &&
                text.Contains("Cream Palette 1") && text.Contains("CakeSectorVisual") &&
                text.Contains("Cake Sector 1 Base Lit Mesh") &&
                text.Contains("Donation Zone Sign Board") &&
                text.Contains("Finished Zone Sign Board") &&
                text.Contains("Shop Egg Rack Left");
        }
    }
}
#endif
