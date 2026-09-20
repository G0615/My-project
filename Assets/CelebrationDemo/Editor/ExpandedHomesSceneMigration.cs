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
            // Target roots are all serialized as InteractionCollider, so the
            // authored pile/tree display names are not reliable migration
            // markers. The mirrored right-side target IDs are stable and are
            // present in the expanded scene saved by the current layout.
            bool hasMirroredInteractionTargets =
                text.Contains("apple_pile_right") &&
                text.Contains("banana_pile_right") &&
                text.Contains("orange_pile_right") &&
                text.Contains("egg_pile_right") &&
                text.Contains("cut_right") &&
                text.Contains("whip_right") &&
                text.Contains("sliced_fruit_right") &&
                text.Contains("cream_pile_right");

            bool hasLegacyExpandedSignature = text.Contains("Banana Pile") && text.Contains("InteractionCollider") &&
                text.Contains("Home 3 Orange Tree") && text.Contains("Cake Sector 1 Base") &&
                text.Contains("Cream Palette 1") && text.Contains("CakeSectorVisual") &&
                text.Contains("Cake Sector 1 Base Lit Mesh") &&
                text.Contains("Donation Zone Sign Board") &&
                text.Contains("Finished Zone Sign Board") &&
                text.Contains("Shop Egg Rack Left") &&
                (text.Contains("Interactive Outer Trees Layout V2") ||
                 text.Contains("home_apple_outer_a"));

            return hasMirroredInteractionTargets || hasLegacyExpandedSignature;
        }
    }
}
#endif
