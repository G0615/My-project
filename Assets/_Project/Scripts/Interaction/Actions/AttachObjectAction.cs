using UnityEngine;

public class AttachObjectAction : InteractionAction
{
    [SerializeField] GameObject objectToAttach;
    [SerializeField] Transform attachPoint;

    public override InteractionResult Execute(InteractionContext context)
    {
        if (objectToAttach == null || attachPoint == null) return InteractionResult.Failure("Attach target is not configured");
        objectToAttach.transform.SetParent(attachPoint, false);
        objectToAttach.transform.localPosition = Vector3.zero;
        objectToAttach.transform.localRotation = Quaternion.identity;
        return InteractionResult.Success("Object attached");
    }
}
