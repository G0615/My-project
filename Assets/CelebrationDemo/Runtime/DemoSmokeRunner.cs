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
            Require(runtime.Actors.Length == 3 && runtime.Targets.Length == 19, "Scene contents");
            runtime.ResetDemo();
            yield return null;
            yield return PressKey(Key.Digit2);
            Require(runtime.ActiveActorId == 2, "Keyboard 2 selects the second actor");
            var firstPosition = runtime.GetActorView(1).transform.position;
            var secondPosition = runtime.GetActorView(2).transform.position;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.D));
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
            Require(Vector2.Distance(new Vector2(firstPosition.x, firstPosition.z),
                new Vector2(runtime.GetActorView(1).transform.position.x, runtime.GetActorView(1).transform.position.z)) < .05f,
                "WASD does not move the unselected actor");
            Require(Vector3.Distance(secondPosition, runtime.GetActorView(2).transform.position) > .1f,
                "WASD moves the selected actor");
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
