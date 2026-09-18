using UnityEngine;

public class ChangeMaterialAction : InteractionAction
{
    [SerializeField] Renderer targetRenderer;
    [SerializeField] Color activeColor = new Color(.2f, .9f, 1f);
    [SerializeField] Color inactiveColor = Color.white;
    bool active;

    public override InteractionResult Execute(InteractionContext context)
    {
        if (targetRenderer == null) targetRenderer = GetComponentInParent<Renderer>();
        if (targetRenderer == null) return InteractionResult.Failure("No Renderer configured");
        active = !active;
        targetRenderer.material.color = active ? activeColor : inactiveColor;
        return InteractionResult.Success("Material changed");
    }
}
