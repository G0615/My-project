using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CelebrationDemo
{
    public static class DemoBuildValidation
    {
        public const string ScenePath = "Assets/Scenes/CelebrationPrototype.unity";

        // Batch entry point: generates the authored scene, verifies wiring, then builds it.
        [MenuItem("Tools/Celebration Demo/Validate and Build Prototype")]
        public static void Build()
        {
            CelebrationSceneBuilder.Create();
            ValidateScene();
            RunCoreChecks();
            Directory.CreateDirectory("Builds/CelebrationDemo");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/CelebrationDemo/CelebrationDemo.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Prototype build failed: " + report.summary.result);
            Debug.Log("CELEBRATION_BUILD_PASS: " + report.summary.totalSize + " bytes");
        }

        [MenuItem("Tools/Celebration Demo/Validate Prototype Scene")]
        public static void ValidateScene()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var runtime = UnityEngine.Object.FindAnyObjectByType<DemoRuntime>();
            Require(runtime != null, "Runtime missing");
            Require(runtime.Actors != null && runtime.Actors.Length == 3, "Expected three wired actors");
            Require(runtime.Actors.Select(a => a.ActorId).OrderBy(id => id).SequenceEqual(new[] { 1, 2, 3 }), "Actor identities");
            Require(runtime.CameraRig != null && runtime.CameraRig.GetComponent<Camera>() != null, "Camera missing");
            Require(runtime.Hud != null, "HUD missing");
            Require(UnityEngine.Object.FindObjectsByType<Camera>().Length == 1, "Exactly one camera after scene generation");
            Require(UnityEngine.Object.FindObjectsByType<Collider>()
                .Any(c => c.name == "Ground" && c.enabled && !c.isTrigger), "Solid ground collider missing");
            Require(runtime.Targets != null && runtime.Targets.Length == 19, "Expected 19 targets: 9 supplies/work + 6 cake + celebration + 3 trophy");
            Require(runtime.Targets.Select(t => t.Spec.Id).Distinct().Count() == runtime.Targets.Length, "Duplicate target IDs");
            foreach (TargetKind kind in Enum.GetValues(typeof(TargetKind)))
                Require(runtime.Targets.Any(t => t.Spec.Kind == kind), "Missing target " + kind);
            Require(runtime.Targets.Count(t => t.Spec.Kind == TargetKind.CakeFruit) == 3, "Fruit slot count");
            Require(runtime.Targets.Count(t => t.Spec.Kind == TargetKind.CakeCream) == 3, "Cream region count");
            Require(runtime.Targets.Where(t => t.Spec.Kind == TargetKind.Trophy).Select(t => t.Spec.OwnerActorId)
                .OrderBy(id => id).SequenceEqual(new[] { 1, 2, 3 }), "Trophy ownership");
            Require(UnityEngine.Object.FindObjectsByType<PlayerController>().Length == 0,
                "Old input-reading PlayerController must not run in the new scene");
            Require(UnityEngine.Object.FindObjectsByType<PlayerInteractor>().Length == 0,
                "Old input-reading PlayerInteractor must not run in the new scene");
            Debug.Log("CELEBRATION_SCENE_PASS: actor identities, 19 targets, ownership, references, input isolation");
        }

        static void RunCoreChecks()
        {
            // The core agent supplies a framework-free suite; reflection keeps it optional in player builds.
            var checkType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.FullName == "CelebrationDemo.CoreSmokeTests");
            if (checkType == null) throw new Exception("CoreSmokeTests missing; do not build without rule verification.");
            var run = checkType.GetMethod("RunAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (run == null) throw new Exception("CoreSmokeTests.RunAll missing");
            var result = run.Invoke(null, null);
            Debug.Log("CELEBRATION_CORE_PASS: " + result);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
