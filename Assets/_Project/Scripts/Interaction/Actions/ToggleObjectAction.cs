using UnityEngine;

public class ToggleObjectAction : InteractionAction
{
    [SerializeField] GameObject targetObject;

    public override InteractionResult Execute(InteractionContext context)
    {
        if (targetObject == null) return InteractionResult.Failure("No target object configured");
        targetObject.SetActive(!targetObject.activeSelf);
        return InteractionResult.Success(targetObject.activeSelf ? "Object enabled" : "Object disabled");
    }
}
