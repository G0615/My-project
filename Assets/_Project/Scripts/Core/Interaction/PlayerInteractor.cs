using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] float interactionRange = 2.5f;
    [SerializeField] float minimumFacingDot = -0.15f;
    [SerializeField] LayerMask interactionMask = ~0;
    [SerializeField] InputActionReference interactAction;
    [SerializeField] Transform viewOrigin;

    public event Action<IInteractable> TargetChanged;
    public event Action<InteractionResult> InteractionCompleted;
    public IInteractable CurrentTarget { get; private set; }
    public float InteractionRange => interactionRange;

    void Awake()
    {
        if (viewOrigin == null) viewOrigin = Camera.main != null ? Camera.main.transform : transform;
    }

    void OnEnable()
    {
        if (interactAction != null) interactAction.action.Enable();
    }

    void OnDisable()
    {
        if (interactAction != null) interactAction.action.Disable();
    }

    void Update()
    {
        SetCurrentTarget(FindBestTarget());
        if (CurrentTarget != null && WasInteractPressed()) TryInteract();
    }

    public bool TryInteract()
    {
        if (CurrentTarget == null) return false;

        var targetBehaviour = CurrentTarget as MonoBehaviour;
        var targetObject = targetBehaviour != null ? targetBehaviour.gameObject : gameObject;
        var point = targetObject.transform.position;
        var context = new InteractionContext(this, CurrentTarget, targetObject, point);
        var result = CurrentTarget.Interact(context);
        InteractionCompleted?.Invoke(result);
        return result.Succeeded;
    }

    bool WasInteractPressed()
    {
        if (interactAction != null) return interactAction.action.WasPressedThisFrame();
        return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
    }

    IInteractable FindBestTarget()
    {
        var origin = viewOrigin != null ? viewOrigin.position : transform.position;
        var forward = viewOrigin != null ? viewOrigin.forward : transform.forward;
        IInteractable best = null;
        float bestScore = float.MinValue;

        foreach (var collider in Physics.OverlapSphere(transform.position, interactionRange, interactionMask, QueryTriggerInteraction.Collide))
        {
            var behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (!(behaviour is IInteractable candidate)) continue;
                var candidateObject = behaviour.gameObject;
                var point = collider.ClosestPoint(origin);
                var direction = (point - origin).normalized;
                var facing = Vector3.Dot(forward, direction);
                if (facing < minimumFacingDot) continue;

                var context = new InteractionContext(this, candidate, candidateObject, point);
                if (!candidate.CanInteract(context)) continue;
                var distanceScore = 1f - Mathf.Clamp01(Vector3.Distance(transform.position, point) / interactionRange);
                var score = facing * .65f + distanceScore * .35f;
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
                break;
            }
        }
        return best;
    }

    void SetCurrentTarget(IInteractable target)
    {
        if (ReferenceEquals(CurrentTarget, target)) return;
        CurrentTarget = target;
        TargetChanged?.Invoke(target);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
