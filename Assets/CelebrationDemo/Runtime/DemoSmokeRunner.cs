using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CelebrationDemo
{
    /// <summary>Runs only with -demoSmoke. Exercises the built scene through the public runtime.</summary>
    public sealed class DemoSmokeRunner : MonoBehaviour
    {
        DemoRuntime runtime;
        int checks;
        int errors;
        string outputDirectory;
        Keyboard testKeyboard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallForAutomatedRun()
        {
            if (!Environment.GetCommandLineArgs().Contains("-demoSmoke")) return;
            new GameObject("Automated prototype verification").AddComponent<DemoSmokeRunner>();
        }

        IEnumerator Start()
        {
            outputDirectory = Path.Combine(Application.dataPath, "..", "Verification");
            Directory.CreateDirectory(outputDirectory);
            Application.logMessageReceived += CountErrors;
            yield return null;
            yield return null;
            runtime = FindAnyObjectByType<DemoRuntime>();
            testKeyboard = InputSystem.AddDevice<Keyboard>("DemoVerificationKeyboard");
            var sequences = new Stack<IEnumerator>();
            sequences.Push(RunSequence());
            while (sequences.Count > 0)
            {
                bool next;
                var sequence = sequences.Peek();
                try { next = sequence.MoveNext(); }
                catch (Exception exception)
                {
                    Finish(false, exception.ToString());
                    yield break;
                }
                if (!next) { sequences.Pop(); continue; }
                if (sequence.Current is IEnumerator nested && !(sequence.Current is CustomYieldInstruction))
                    sequences.Push(nested);
                else
                    yield return sequence.Current;
            }
            Finish(errors == 0, errors == 0 ? "Complete scene and UI flow passed." : errors + " Unity errors during run.");
        }

        IEnumerator RunSequence()
        {
            Require(runtime != null && runtime.Session != null, "Runtime initialized");
            Require(runtime.Actors.Length == 3 && runtime.Targets.Length == 30, "Scene contents");
            yield return VerifyMovementAndBoundary();
            yield return VerifyActorSwitching();
            yield return VerifyCameraFollow();
            yield return VerifyTargetSelectionAndSinglePress();
            runtime.ResetDemo();
            yield return null;
            runtime.ResetDemo();
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "01-plaza.png"));

            // Drive target selection, the F command path, state, and UI together.
            yield return Perform(TargetKind.HomeFruit, 1);
            yield return Perform(TargetKind.FruitPile, 1);
            Require(runtime.Session.GetActor(1).HasDonated, "Donation participation");
            yield return Perform(TargetKind.ShopEgg, 2);
            yield return Perform(TargetKind.EggPile, 2);

            yield return Perform(TargetKind.CutStation, 1);
            yield return Perform(TargetKind.CutStation, 2);
            yield return Perform(TargetKind.CutStation, 3);
            Require(runtime.Session.CutStation.ParticipantIds.Count() == 3, "Three-person shared batch");
            yield return Perform(TargetKind.WhipStation, 1);
            yield return Perform(TargetKind.WhipStation, 2);
            yield return new WaitForSeconds(5.3f);
            Require(!runtime.Session.CutStation.IsRunning && !runtime.Session.WhipStation.IsRunning, "Both batches finish while switching");

            yield return Perform(TargetKind.Chopsticks, 2);
            yield return Perform(TargetKind.SlicedFruit, 2);
            Require(runtime.Session.GetActor(2).HasEaten && runtime.Session.GetSpeed(2) == 9f, "Steal feedback and speed");
            Require(runtime.Session.Resolve(2, Target(TargetKind.FruitPile).Spec).Label.Contains("偷吃"), "Chopsticks choose stealing automatically");
            for (int i = 0; i < 3; i++)
            {
                yield return Perform(TargetKind.CakeFruit, 1, i);
                yield return Perform(TargetKind.CakeCream, 3, i);
            }
            Require(runtime.Session.Cake.FruitStyles.All(value => value > 0), "Three fruit slots visible");
            Require(runtime.Session.Cake.CreamColors.All(value => value > 0), "Three cream regions changed");

            // Generate enough valid actions to prove the HUD window cannot truncate trophy history.
            var home = Target(TargetKind.HomeFruit).Spec;
            for (int i = 0; i < 110; i++) runtime.Session.Execute(1, home);
            Require(runtime.Session.GetHistory(1).Count > 100, "History beyond recent log window");

            yield return Perform(TargetKind.Celebration, 3);
            Require(runtime.IsCelebrationSequenceRunning, "Celebration enters countdown and fireworks presentation");
            yield return new WaitUntil(() => runtime.Hud.IsModalOpen);
            Require(!runtime.IsCelebrationSequenceRunning, "Celebration presentation ends before result modal");
            Require(runtime.Hud.IsModalOpen, "Celebration UI opens");
            for (int id = 1; id <= 3; id++)
                Require(runtime.Session.GetActor(id).TrophyStatus == TrophyStatus.Awarded, "Actor " + id + " awarded");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "02-celebration.png"));
            runtime.Hud.CloseModal();
            yield return Perform(TargetKind.Celebration, 1);
            Require(runtime.Session.History.Count(e => e.ActorText != null && e.ActorText.Contains("[奖杯]+1")) == 3,
                "Exactly three trophy awards after repeat celebration");
            runtime.Hud.CloseModal();

            for (int id = 1; id <= 3; id++)
            {
                yield return Perform(TargetKind.Trophy, id, 0, id);
                Require(runtime.Session.GetActor(id).TrophyStatus == TrophyStatus.Placed, "Actor " + id + " places trophy");
                Require(!runtime.Hud.IsModalOpen, "Placement does not inspect in the same input");
                yield return Perform(TargetKind.Trophy, id, 0, id);
                Require(runtime.Hud.IsModalOpen, "Actor " + id + " opens own full history");
                if (id == 1)
                {
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, "03-trophy-history.png"));
                }
                runtime.Hud.CloseModal();
                Require(!runtime.Hud.IsModalOpen, "History closes");
            }
            yield return Perform(TargetKind.Trophy, 1, 0, 1);
            runtime.SelectActor(2);
            Require(!runtime.Hud.IsModalOpen, "Switch closes history before changing identity");
            runtime.ResetDemo();
            yield return null;
            Require(runtime.Session.History.Count == 0 && !runtime.Session.HasCelebrated, "Round reset clears history and settlement");
            Require(runtime.Session.GetActor(1).TrophyStatus == TrophyStatus.Unawarded, "Round reset clears trophies");
            yield return new WaitForSeconds(.5f);
        }

        IEnumerator VerifyMovementAndBoundary()
        {
            runtime.ResetDemo();
            yield return null;

            var actor = runtime.GetActorView(1);
            Require(actor != null, "Primary actor available for movement checks");

            Vector3 axisStart = actor.transform.position;
            yield return HoldKeys(.35f, Key.W);
            float axisDistance = HorizontalDistance(axisStart, actor.transform.position);
            Require(axisDistance > .5f, "W moves the primary actor");

            runtime.ResetDemo();
            yield return null;
            Vector3 diagonalStart = actor.transform.position;
            yield return HoldKeys(.35f, Key.W, Key.D);
            float diagonalDistance = HorizontalDistance(diagonalStart, actor.transform.position);
            Require(diagonalDistance <= axisDistance * 1.12f + .1f,
                "Diagonal movement is capped at the single-axis speed");

            runtime.ResetDemo();
            yield return null;
            yield return HoldKeys(3.5f, Key.D);
            Require(actor.transform.position.x > 20.8f && actor.transform.position.x < 21.45f,
                "East boundary keeps the primary actor inside the ground");
            yield return HoldKeys(3.5f, Key.W);
            Require(actor.transform.position.z > 15.8f && actor.transform.position.z < 16.45f,
                "North boundary keeps the primary actor inside the ground");
            yield return HoldKeys(7f, Key.A);
            Require(actor.transform.position.x > -21.45f && actor.transform.position.x < -20.8f,
                "West boundary keeps the primary actor inside the ground");
            yield return HoldKeys(7f, Key.S);
            Require(actor.transform.position.z > -16.45f && actor.transform.position.z < -15.8f,
                "South boundary keeps the primary actor inside the ground");
        }

        IEnumerator VerifyActorSwitching()
        {
            runtime.ResetDemo();
            yield return null;

            int[] actorIds = { 1, 2, 3 };
            Key[] selectKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };
            Key[] movementKeys = { Key.D, Key.RightArrow, Key.A };
            for (int index = 0; index < actorIds.Length; index++)
            {
                int actorId = actorIds[index];
                yield return PressKey(selectKeys[index]);
                Require(runtime.ActiveActorId == actorId, "Keyboard " + actorId + " selects actor " + actorId);

                var before = runtime.Actors.ToDictionary(actor => actor.ActorId,
                    actor => actor.transform.position);
                foreach (var actor in runtime.Actors)
                    Require(actor != null && actor.gameObject.activeInHierarchy && actor.enabled,
                        "Actor remains enabled after selecting " + actorId);

                yield return HoldKeys(.15f, movementKeys[index]);
                Require(HorizontalDistance(before[actorId], runtime.GetActorView(actorId).transform.position) > .1f,
                    "Selected actor " + actorId + " responds to movement");
                foreach (var actor in runtime.Actors)
                    if (actor.ActorId != actorId)
                        Require(HorizontalDistance(before[actor.ActorId], actor.transform.position) < .05f,
                            "Unselected actor " + actor.ActorId + " keeps position while actor " + actorId + " moves");
            }

            yield return PressKey(Key.Digit1);
            Require(runtime.ActiveActorId == 1, "Switching back to actor 1 remains available");
        }

        IEnumerator VerifyCameraFollow()
        {
            runtime.ResetDemo();
            yield return null;

            var rig = runtime.CameraRig;
            var camera = rig == null ? null : rig.GetComponent<Camera>();
            var actor1 = runtime.GetActorView(1);
            Require(rig != null && camera != null && actor1 != null, "Fixed camera and primary actor available");

            Quaternion fixedRotation = camera.transform.rotation;
            float fixedSize = camera.orthographicSize;
            Vector3 cameraStart = camera.transform.position;
            Vector3 actorStart = actor1.transform.position;
            Vector3 followOffset = cameraStart - actorStart;

            yield return HoldKeys(.35f, Key.D);
            yield return new WaitForSeconds(1f);
            Require(HorizontalDistance(cameraStart, camera.transform.position) > .1f,
                "Camera translates after the active actor moves");
            Require(Vector3.Distance(camera.transform.position, actor1.transform.position + followOffset) < .25f,
                "Camera settles near the moving actor with its fixed offset");
            Require(Quaternion.Angle(fixedRotation, camera.transform.rotation) < .01f,
                "Camera rotation remains fixed while following");
            Require(Mathf.Abs(fixedSize - camera.orthographicSize) < .001f,
                "Camera orthographic size remains fixed while following");

            runtime.ResetDemo();
            yield return null;
            var actor3 = runtime.GetActorView(3);
            Vector3 beforeRapidSwitch = camera.transform.position;
            runtime.SelectActor(2);
            runtime.SelectActor(3);
            Require(rig.Target == actor3.transform, "Rapid switching leaves the latest actor as camera target");
            Require(Vector3.Distance(beforeRapidSwitch, camera.transform.position) < .001f,
                "Switching camera target does not teleport the camera");
            yield return new WaitForSeconds(1.4f);
            Require(Vector3.Distance(camera.transform.position, actor3.transform.position + followOffset) < .3f,
                "Camera settles near the latest switched actor");
        }

        IEnumerator VerifyTargetSelectionAndSinglePress()
        {
            runtime.ResetDemo();
            yield return null;

            var actor = runtime.GetActorView(1);
            var first = Target(TargetKind.HomeFruit);
            var second = Target(TargetKind.FruitPile);
            Require(actor != null && first != null && second != null, "A06 target selection fixtures available");

            var controller = actor.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            Vector3 originalPosition = actor.transform.position;
            Quaternion originalRotation = actor.transform.rotation;
            float firstRadius = first.InteractionRadius;
            float secondRadius = second.InteractionRadius;

            // Temporarily overlap two existing scene targets to exercise the
            // single-target tie-break without adding another runtime object.
            Vector3 midpoint = (first.transform.position + second.transform.position) * .5f;
            midpoint.y = 1.05f;
            float halfDistance = Vector3.Distance(first.transform.position, second.transform.position) * .5f;
            first.InteractionRadius = Mathf.Max(firstRadius, halfDistance + .1f);
            second.InteractionRadius = Mathf.Max(secondRadius, halfDistance + .1f);
            actor.transform.SetPositionAndRotation(midpoint, Quaternion.LookRotation(Vector3.forward));
            if (controller != null) controller.enabled = true;
            yield return null;

            var selected = runtime.CurrentTarget;
            Require(selected != null && (selected == first || selected == second),
                "One target selected when two targets overlap");
            Require(first.IsHighlighted != second.IsHighlighted,
                "Only the selected target is highlighted");
            Require(selected.IsHighlighted, "Selected target is highlighted");
            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
                Require(runtime.CurrentTarget == selected, "Overlapping target selection remains stable");
                Require(first.IsHighlighted != second.IsHighlighted,
                    "Stable selection keeps one highlighted target");
                Require(selected.IsHighlighted, "Stable target keeps its highlight");
            }

            // The stable key must win even if the serialized target array is
            // reordered while the two equal-distance candidates overlap.
            var authoredTargets = runtime.Targets;
            runtime.Targets = authoredTargets.Reverse().ToArray();
            runtime.SelectActor(1);
            yield return null;
            Require(runtime.CurrentTarget == selected, "Equal-distance tie-break ignores target array order");
            runtime.Targets = authoredTargets;

            var promptOffer = runtime.Session.Resolve(1, selected.Spec);
            var prompt = DemoHud.FormatTargetPrompt(selected, promptOffer);
            Require(prompt.StartsWith("[F]") && !prompt.Contains("\n"),
                "Target prompt shows only the F key and action");
            Require(prompt.Contains(promptOffer.Label), "Target prompt matches the resolved F action");
            Require(!prompt.Contains(selected.Spec.Id), "Target prompt does not expose internal target ID");

            first.InteractionRadius = firstRadius;
            second.InteractionRadius = secondRadius;
            if (controller != null) controller.enabled = false;
            actor.transform.SetPositionAndRotation(originalPosition, originalRotation);
            actor.transform.position += Vector3.right * 100f;
            if (controller != null) controller.enabled = true;
            yield return null;
            Require(runtime.CurrentTarget == null, "Target clears after leaving interaction range");
            Require(!selected.IsHighlighted, "Highlight clears after leaving interaction range");

            // Return to one real target and hold F for several frames. The
            // Input System's edge event must produce exactly one core action.
            if (controller != null) controller.enabled = false;
            Vector3 targetPosition = first.transform.position + Vector3.back * Mathf.Min(1.25f, first.InteractionRadius * .8f);
            targetPosition.y = 1.05f;
            actor.transform.SetPositionAndRotation(targetPosition, Quaternion.LookRotation(Vector3.forward));
            if (controller != null) controller.enabled = true;
            yield return null;
            Require(runtime.CurrentTarget == first && first.IsHighlighted, "Single target is selected before F");
            int historyBefore = runtime.Session.GetHistory(1).Count;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.F));
            yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            int historyAfter = runtime.Session.GetHistory(1).Count;
            Require(historyAfter == historyBefore + 1, "Holding F triggers exactly one interaction");
        }

        IEnumerator HoldKeys(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(keys));
            yield return new WaitForSeconds(seconds);
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        IEnumerator Perform(TargetKind kind, int actorId, int index = 0, int owner = 0)
        {
            runtime.SelectActor(actorId);
            var target = Target(kind, index, owner);
            var actor = runtime.GetActorView(actorId);
            Vector3 direction = Vector3.back;
            if (kind == TargetKind.CakeFruit || kind == TargetKind.CakeCream)
            {
                direction = target.transform.position;
                direction.y = 0;
                direction.Normalize();
            }
            var controller = actor.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            var position = target.transform.position + direction * Mathf.Min(1.25f, target.InteractionRadius * .8f);
            position.y = 1.05f;
            actor.transform.position = position;
            actor.transform.forward = -direction;
            if (controller != null) controller.enabled = true;
            yield return null;
            yield return null;
            Require(runtime.CurrentTarget == target, "Select target " + target.Spec.Id);
            Require(runtime.Session.Resolve(actorId, target.Spec).CanExecute, "Offer executable " + target.Spec.Id);
            yield return PressKey(Key.F);
            yield return null;
        }

        IEnumerator PressKey(Key key)
        {
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
        }

        TargetView Target(TargetKind kind, int index = 0, int owner = 0)
        {
            return runtime.Targets.First(t => t.Spec.Kind == kind
                && (kind != TargetKind.CakeFruit && kind != TargetKind.CakeCream || t.Spec.Index == index)
                && (kind != TargetKind.Trophy || t.Spec.OwnerActorId == owner));
        }

        void Require(bool condition, string description)
        {
            if (!condition) throw new Exception("Scene check failed: " + description);
            checks++;
            Debug.Log("SMOKE_PASS " + description);
        }

        void CountErrors(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++;
        }

        void Finish(bool success, string description)
        {
            Application.logMessageReceived -= CountErrors;
            if (testKeyboard != null) InputSystem.RemoveDevice(testKeyboard);
            var result = (success ? "PASS" : "FAIL") + "\nChecks: " + checks + "\n" + description;
            File.WriteAllText(Path.Combine(outputDirectory, "smoke-result.txt"), result);
            Debug.Log("CELEBRATION_SMOKE_" + result);
            Application.Quit(success ? 0 : 1);
        }
    }
}
