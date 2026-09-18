using UnityEngine;

public class AddProgressAction : InteractionAction
{
    [SerializeField] float amount = .25f;
    [SerializeField] float maximum = 1f;
    float progress;

    public float Progress => progress;

    public override InteractionResult Execute(InteractionContext context)
    {
        progress = Mathf.Clamp(progress + amount, 0f, maximum);
        return InteractionResult.Success($"Progress {progress:0%}");
    }
}
