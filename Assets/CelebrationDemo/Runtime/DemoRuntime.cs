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
            float bestScore = float.NegativeInfinity;
            foreach (var target in Targets)
            {
                if (target == null || !target.isActiveAndEnabled || target.Spec == null) continue;
                if (target.Spec.Kind == TargetKind.Trophy && target.Spec.OwnerActorId != ActiveActorId) continue;
                Vector3 offset = target.transform.position - actor.transform.position;
                offset.y = 0;
                float distance = offset.magnitude;
                if (distance > target.InteractionRadius) continue;
                float facing = distance > .05f ? Vector3.Dot(actor.transform.forward, offset / distance) : 1;
                // Distance dominates. Facing and a small sticky margin avoid flickering at boundaries.
                float score = -distance + .3f * facing + (target == CurrentTarget ? .18f : 0);
                if (score > bestScore) { bestScore = score; best = target; }
            }
            return best;
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
