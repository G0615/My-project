using UnityEngine;

public class Interactable : MonoBehaviour, IInteractable
{
    [SerializeField] string prompt = "Interact";
    public InteractionAction[] actions;
    [SerializeField] bool useDefaultMaterialToggle = true;
    [SerializeField] Color activeColor = new Color(.2f, .9f, 1f);

    Renderer targetRenderer;
    Vector3 baseScale;
    bool active;

    public string InteractionPrompt => prompt;

    void Awake()
    {
        targetRenderer = GetComponentInChildren<Renderer>();
        baseScale = transform.localScale;
    }

    public bool CanInteract(InteractionContext context) => isActiveAndEnabled;

    public InteractionResult Interact(InteractionContext context)
    {
        if (!CanInteract(context)) return InteractionResult.Failure();

        bool ranAction = false;
        if (actions != null)
        {
            foreach (var action in actions)
            {
                if (action == null || !action.isActiveAndEnabled) continue;
                var result = action.Execute(context);
                ranAction = true;
                if (!result.Succeeded) return result;
            }
        }

        if (!ranAction && useDefaultMaterialToggle)
        {
            active = !active;
            if (targetRenderer != null) targetRenderer.material.color = active ? activeColor : Color.white;
            transform.localScale = baseScale * (active ? 1.15f : 1f);
        }

        return InteractionResult.Success(prompt + " complete");
    }
}
