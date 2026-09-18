using UnityEngine;

public interface IInteractionAction
{
    InteractionResult Execute(InteractionContext context);
}

public abstract class InteractionAction : MonoBehaviour, IInteractionAction
{
    public abstract InteractionResult Execute(InteractionContext context);
}
