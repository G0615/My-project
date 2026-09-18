using UnityEngine;

public class PlayAnimationAction : InteractionAction
{
    [SerializeField] Animator animator;
    [SerializeField] string triggerName = "Interact";

    public override InteractionResult Execute(InteractionContext context)
    {
        if (animator == null) animator = GetComponentInParent<Animator>();
        if (animator == null) return InteractionResult.Failure("No Animator configured");
        animator.SetTrigger(triggerName);
        return InteractionResult.Success("Animation played");
    }
}
