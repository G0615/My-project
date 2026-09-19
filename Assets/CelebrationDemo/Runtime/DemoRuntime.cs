using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CelebrationDemo
{
    /// <summary>One input owner and one clock for all three actors.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class DemoRuntime : MonoBehaviour
    {
        public ActorView[] Actors;
        public TargetView[] Targets;
        public FixedAngleCamera CameraRig;
        public DemoHud Hud;
        public DemoConfig Settings = new DemoConfig();

        public DemoSession Session { get; private set; }
        public int ActiveActorId { get; private set; } = 1;
        public TargetView CurrentTarget { get; private set; }

        const float SelectionDistanceEpsilon = 0.01f;
        const float SelectionFacingEpsilon = 0.001f;

        double elapsed;
        Camera gameplayCamera;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Session = new DemoSession(Settings);
            Session.EventRecorded += PresentAction;
        }

        void Start()
        {
            // Serialized scene references are preferred; these fallbacks help hand-edited scenes.
            if (Actors == null || Actors.Length == 0)
                Actors = FindObjectsByType<ActorView>();
            if (Targets == null || Targets.Length == 0)
                Targets = FindObjectsByType<TargetView>();
            if (CameraRig == null) CameraRig = FindAnyObjectByType<FixedAngleCamera>();
            if (Hud == null) Hud = FindAnyObjectByType<DemoHud>();
            gameplayCamera = CameraRig != null ? CameraRig.GetComponent<Camera>() : Camera.main;
            if (Hud != null) Hud.Initialize(this);
            SelectActor(1);
            var actor = GetActorView(ActiveActorId);
            if (actor != null && CameraRig != null) CameraRig.SetTarget(actor.transform, true);
            RefreshWorld();
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            Session.Advance(elapsed);
            var keyboard = Keyboard.current;
            bool switched = false;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                { SelectActor(1); switched = true; }
                else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                { SelectActor(2); switched = true; }
                else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                { SelectActor(3); switched = true; }

                if (keyboard.escapeKey.wasPressedThisFrame && Hud != null) Hud.CloseModal();
                if (keyboard.rKey.wasPressedThisFrame && (Hud == null || !Hud.IsModalOpen))
                { ResetDemo(); return; }
            }

            bool blocked = Hud != null && Hud.IsModalOpen;
            Vector2 movement = !blocked && !switched && keyboard != null
                ? ReadMovement(keyboard)
                : Vector2.zero;
            RouteMovement(movement);

            SetTarget(blocked ? null : FindTarget());
            if (!blocked && !switched && keyboard != null && keyboard.fKey.wasPressedThisFrame)
                Interact();
            RefreshWorld();
        }

        /// <summary>Reads the one shared keyboard and keeps diagonal input normalized.</summary>
        static Vector2 ReadMovement(Keyboard keyboard)
        {
            var movement = new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0));
            return Vector2.ClampMagnitude(movement, 1f);
        }

        /// <summary>
        /// Routes the shared input to the active actor only. Other ActorView instances
        /// remain enabled and keep their own transforms and state untouched.
        /// </summary>
        void RouteMovement(Vector2 movement)
        {
            var actor = GetActorView(ActiveActorId);
            if (actor == null || Session == null) return;
            actor.Move(movement, Session.GetSpeed(ActiveActorId), gameplayCamera);
        }

        public ActorView GetActorView(int actorId)
        {
            if (Actors != null)
                foreach (var actor in Actors)
                    if (actor != null && actor.ActorId == actorId) return actor;
            return null;
        }

        public void SelectActor(int actorId)
        {
            var selected = GetActorView(actorId);
            if (selected == null) return;
            if (Hud != null) Hud.CloseModal();
            ActiveActorId = actorId;
            foreach (var actor in Actors)
                if (actor != null) actor.SetActiveVisual(actor.ActorId == actorId);
            if (CameraRig != null) CameraRig.SetTarget(selected.transform);
            SetTarget(null);
        }

        public void Interact()
        {
            if (Hud != null && Hud.IsModalOpen) return;
            // Resolve again at the current shared clock so tool expiry and prompts agree.
            var target = CurrentTarget;
            if (target == null) return;
            var offer = Session.Resolve(ActiveActorId, target.Spec);
            if (!offer.CanExecute) return;
            var outcome = Session.Execute(ActiveActorId, target.Spec);
            RefreshWorld();
            if (Hud != null && outcome != null)
            {
                if (outcome.OpenHistory) Hud.ShowHistory(outcome.ActorId);
                else if (outcome.OpenCelebration) Hud.ShowCelebration();
            }
        }

        public void ResetDemo()
        {
            elapsed = 0;
            Session.Reset();
            if (Hud != null) Hud.ResetView();
            if (Actors != null)
                foreach (var actor in Actors)
                    if (actor != null) actor.ResetPosition();
            SelectActor(1);
            var selected = GetActorView(1);
            if (selected != null && CameraRig != null) CameraRig.SetTarget(selected.transform, true);
            RefreshWorld();
        }

        TargetView FindTarget()
        {
            var actor = GetActorView(ActiveActorId);
            if (actor == null || Targets == null) return null;
            TargetView best = null;
            float bestDistance = float.PositiveInfinity;
            float bestFacing = float.NegativeInfinity;
            foreach (var target in Targets)
            {
                if (target == null || !target.isActiveAndEnabled || target.Spec == null) continue;
                if (target.Spec.Kind == TargetKind.Trophy && target.Spec.OwnerActorId != ActiveActorId) continue;
                var offer = Session == null ? null : Session.Resolve(ActiveActorId, target.Spec);
                if (offer == null || !offer.CanExecute) continue;
                Vector3 offset = target.transform.position - actor.transform.position;
                offset.y = 0;
                float distance = offset.magnitude;
                if (distance > target.InteractionRadius) continue;
                float facing = distance > .05f ? Vector3.Dot(actor.transform.forward, offset / distance) : 1;
                if (IsBetterTarget(target, distance, facing, best, bestDistance, bestFacing, CurrentTarget))
                {
                    bestDistance = distance;
                    bestFacing = facing;
                    best = target;
                }
            }
            return best;
        }

        static bool IsBetterTarget(TargetView candidate, float candidateDistance, float candidateFacing,
            TargetView current, float currentDistance, float currentFacing, TargetView stickyTarget)
        {
            if (current == null) return true;

            // Keep distance primary, while treating tiny movement/float noise as
            // an intentional tie instead of allowing the winner to alternate.
            if (candidateDistance < currentDistance - SelectionDistanceEpsilon) return true;
            if (candidateDistance > currentDistance + SelectionDistanceEpsilon) return false;

            // Once a target is selected, keep it through an equal-distance band.
            // This is especially important when two adjacent targets share a
            // boundary and the actor position changes by only a few millimetres.
            if (candidate == stickyTarget) return true;
            if (current == stickyTarget) return false;

            if (candidateFacing > currentFacing + SelectionFacingEpsilon) return true;
            if (candidateFacing < currentFacing - SelectionFacingEpsilon) return false;
            return IsStableTieWinner(candidate, current);
        }

        // Scene arrays are serialized in authoring order, but a stable ID tie-break
        // keeps selection deterministic if a hand-edited scene reorders references.
        static bool IsStableTieWinner(TargetView candidate, TargetView current)
        {
            if (current == null) return true;
            return string.CompareOrdinal(candidate.StableSelectionKey, current.StableSelectionKey) < 0;
        }

        void SetTarget(TargetView target)
        {
            if (CurrentTarget == target) return;
            if (CurrentTarget != null) CurrentTarget.SetHighlighted(false);
            CurrentTarget = target;
            if (CurrentTarget != null) CurrentTarget.SetHighlighted(true);
        }

        void RefreshWorld()
        {
            if (Targets == null) return;
            foreach (var target in Targets)
                if (target != null) target.ApplyState(Session);
        }

        void PresentAction(ActionEvent action)
        {
            // A presentation failure must never roll back a completed batch or trophy award.
            if (Hud == null) return;
            try { Hud.OnAction(action); }
            catch (Exception exception) { Debug.LogException(exception, Hud); }
        }

        void OnDestroy()
        {
            if (Session != null) Session.EventRecorded -= PresentAction;
        }
    }
}
