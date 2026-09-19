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
            var runtimeObjects = UnityEngine.Object.FindObjectsByType<DemoRuntime>();
            Require(runtimeObjects.Length == 1, "Exactly one DemoRuntime must own scene input and session state");
            var runtime = runtimeObjects[0];
            Require(EditorBuildSettings.scenes.Length > 0 &&
                EditorBuildSettings.scenes[0].enabled &&
                EditorBuildSettings.scenes[0].path == ScenePath,
                "CelebrationPrototype must be the enabled first build scene");
            Require(GameObject.Find("CelebrationPrototypeGenerated") != null,
                "Scene root must be owned by CelebrationSceneBuilder");
            Require(runtime != null, "Runtime missing");
            Require(runtime.Actors != null && runtime.Actors.Length == 3, "Expected three wired actors");
            Require(runtime.Actors.All(a => a != null), "Actor reference missing");
            Require(runtime.Actors.Select(a => a.ActorId).OrderBy(id => id).SequenceEqual(new[] { 1, 2, 3 }), "Actor identities");
            Require(runtime.ActiveActorId == 1 && runtime.GetActorView(1) == runtime.Actors[0],
                "Actor 1 is the default active input target");
            ValidateActors(runtime.Actors);
            Require(runtime.CameraRig != null && runtime.CameraRig.GetComponent<Camera>() != null, "Camera missing");
            ValidateCamera(runtime.CameraRig);
            Require(runtime.Hud != null && runtime.Hud.isActiveAndEnabled, "HUD missing or disabled");
            Require(UnityEngine.Object.FindObjectsByType<Camera>().Length == 1, "Exactly one camera after scene generation");
            Require(UnityEngine.Object.FindObjectsByType<Collider>()
                .Any(c => c.name == "Ground" && c.enabled && !c.isTrigger), "Solid ground collider missing");
            var plaza = GameObject.Find("Plaza");
            Require(plaza != null && plaza.GetComponent<Collider>() == null,
                "Plaza visual must remain walkable without a second solid collider");
            ValidateMovementBoundaries();
            Require(runtime.Targets != null && runtime.Targets.Length == 23, "Expected 23 targets: 13 supplies/work + 6 cake + celebration + 3 trophy");
            Require(runtime.Targets.All(t => t != null), "Target reference missing");
            Require(runtime.Targets.All(t => t.Spec != null && t.InteractionRadius > 0f),
                "Every target needs a positive interaction radius and spec");
            Require(runtime.Targets.Select(t => t.Spec.Id).All(id => !string.IsNullOrWhiteSpace(id)),
                "Every target needs a non-empty stable ID");
            Require(runtime.Targets.Select(t => t.Spec.Id).Distinct().Count() == runtime.Targets.Length, "Duplicate target IDs");
            Require(runtime.Targets.All(t => t.FeedbackAnchor != null), "Every target needs a feedback anchor");
            Require(runtime.Targets.All(t => !string.IsNullOrWhiteSpace(t.DisplayName) &&
                t.DisplayName.Any(character => character >= '\u4E00' && character <= '\u9FFF')),
                "Every target needs a readable Chinese interaction name");
            Require(runtime.Targets.All(t => t.transform.Find("Highlight") != null),
                "Every target needs a highlight visual");
            Require(runtime.Targets.All(t =>
                t.GetComponent<Collider>() != null && t.GetComponent<Collider>().enabled && !t.GetComponent<Collider>().isTrigger),
                "Every interaction target needs a solid collision barrier");
            Require(runtime.Targets.All(t => !t.transform.Find("Highlight").gameObject.activeSelf),
                "Target highlights start hidden");
            foreach (TargetKind kind in Enum.GetValues(typeof(TargetKind)))
                Require(runtime.Targets.Any(t => t.Spec.Kind == kind), "Missing target " + kind);
            Require(runtime.Targets.Count(t => t.Spec.Kind == TargetKind.CakeFruit) == 3, "Fruit slot count");
            Require(runtime.Targets.Count(t => t.Spec.Kind == TargetKind.CakeCream) == 3, "Cream region count");
            var eggPile = runtime.Targets.FirstOrDefault(t => t.Spec.Kind == TargetKind.EggPile);
            Require(eggPile != null && eggPile.DisplayName == "鸡蛋堆" &&
                eggPile.GetComponent<Collider>() != null && !eggPile.GetComponent<Collider>().isTrigger,
                "Egg pile must expose a solid F interaction target");
            Require(runtime.Targets.Where(t => t.Spec.Kind == TargetKind.Trophy).Select(t => t.Spec.OwnerActorId)
                .OrderBy(id => id).SequenceEqual(new[] { 1, 2, 3 }), "Trophy ownership");
            Require(UnityEngine.Object.FindObjectsByType<PlayerController>().Length == 0,
                "Old input-reading PlayerController must not run in the new scene");
            Require(UnityEngine.Object.FindObjectsByType<PlayerInteractor>().Length == 0,
                "Old input-reading PlayerInteractor must not run in the new scene");
            Debug.Log("CELEBRATION_SCENE_PASS: actor identities, 23 targets, ownership, collisions, references, input isolation");
            Debug.Log("A08_STATIC_SCENE_PASS: build entry, expanded boundaries, camera, target prompts, runtime/HUD wiring");
        }

        static void ValidateActors(ActorView[] actors)
        {
            for (var index = 0; index < actors.Length; index++)
            {
                var actor = actors[index];
                var actorId = index + 1;
                Require(actor.ActorId == actorId, "Actor array order must match ActorId " + actorId);
                Require(actor.name == "Actor " + actorId, "Actor name/identity mismatch for " + actorId);
                Require(actor.gameObject.activeSelf && actor.enabled,
                    "Actor object remains enabled for " + actorId);
                var controller = actor.GetComponent<CharacterController>();
                Require(controller != null && controller.enabled,
                    "CharacterController missing or disabled for actor " + actorId);
                Require(actor.transform.Find("HeadAnchor") != null, "Head anchor missing for actor " + actorId);
                Require(actor.transform.Find("SelectedMarker") != null, "Selection marker missing for actor " + actorId);

                var body = actor.transform.Find("Body");
                var renderer = body == null ? null : body.GetComponent<Renderer>();
                Require(renderer != null && renderer.sharedMaterial != null,
                    "Body material missing for actor " + actorId);
                var material = renderer.sharedMaterial;
                var color = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.GetColor("_Color");
                Require(ColorMatches(color, ActorView.ColorForActor(actorId)),
                    "Body color mismatch for actor " + actorId);

                for (var other = index + 1; other < actors.Length; other++)
                    Require((actor.transform.position - actors[other].transform.position).sqrMagnitude > 1f,
                        "Actor spawn positions overlap");
            }

            Require(actors.Count(actor => actor.transform.Find("SelectedMarker").gameObject.activeSelf) == 1 &&
                actors[0].transform.Find("SelectedMarker").gameObject.activeSelf,
                "Actor 1 must be the only initially selected actor");
        }

        static void ValidateMovementBoundaries()
        {
            ValidateBoundary("Boundary West", new Vector3(-43.6f, 0.5f, 0f), new Vector3(0.4f, 1f, 68f));
            ValidateBoundary("Boundary East", new Vector3(43.6f, 0.5f, 0f), new Vector3(0.4f, 1f, 68f));
            ValidateBoundary("Boundary North", new Vector3(0f, 0.5f, 33.6f), new Vector3(88f, 1f, 0.4f));
            ValidateBoundary("Boundary South", new Vector3(0f, 0.5f, -33.6f), new Vector3(88f, 1f, 0.4f));
        }

        static void ValidateBoundary(string name, Vector3 expectedPosition, Vector3 expectedScale)
        {
            var boundary = GameObject.Find(name);
            Require(boundary != null, "Missing movement boundary " + name);
            var collider = boundary.GetComponent<BoxCollider>();
            Require(collider != null && collider.enabled && !collider.isTrigger,
                "Movement boundary must be a solid BoxCollider: " + name);
            Require(Vector3.Distance(boundary.transform.position, expectedPosition) < 0.001f,
                "Movement boundary position mismatch: " + name);
            Require(Vector3.Distance(boundary.transform.lossyScale, expectedScale) < 0.001f,
                "Movement boundary size mismatch: " + name);
        }

        static void ValidateCamera(FixedAngleCamera cameraRig)
        {
            var camera = cameraRig.GetComponent<Camera>();
            Require(camera.orthographic, "Camera must use orthographic projection");
            Require(Mathf.Abs(camera.orthographicSize - 21f) < 0.001f,
                "Camera orthographic size must stay at 21 for the expanded scene");
            Require(Quaternion.Angle(cameraRig.transform.rotation, Quaternion.Euler(48f, 0f, 0f)) < 0.01f,
                "Camera rotation must stay at the fixed 48 degree pitch");
            Require(cameraRig.FollowSmoothTime > 0f, "Camera follow smoothing must be positive");
        }

        static bool ColorMatches(Color actual, Color expected)
        {
            return Mathf.Abs(actual.r - expected.r) < 0.01f &&
                Mathf.Abs(actual.g - expected.g) < 0.01f &&
                Mathf.Abs(actual.b - expected.b) < 0.01f;
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
