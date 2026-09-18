using UnityEngine;

public sealed class InteractionContext
{
    public PlayerInteractor Interactor { get; }
    public IInteractable Target { get; }
    public GameObject TargetObject { get; }
    public Vector3 InteractionPoint { get; }

    public InteractionContext(PlayerInteractor interactor, IInteractable target, GameObject targetObject, Vector3 interactionPoint)
    {
        Interactor = interactor;
        Target = target;
        TargetObject = targetObject;
        InteractionPoint = interactionPoint;
    }
}
