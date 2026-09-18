using UnityEngine;

public struct InteractionResult
{
    public bool Succeeded;
    public string Message;

    public InteractionResult(bool succeeded, string message = null)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
    }

    public static InteractionResult Success(string message = null) => new InteractionResult(true, message);
    public static InteractionResult Failure(string message = null) => new InteractionResult(false, message);
}
