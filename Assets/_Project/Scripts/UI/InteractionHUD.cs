using UnityEngine;
using UnityEngine.UI;

public class InteractionHUD : MonoBehaviour
{
    public PlayerInteractor interactor;
    public Text promptText;
    [SerializeField] string keyLabel = "F";

    void Awake()
    {
        if (promptText == null)
        {
            var promptObject = transform.Find("InteractionPrompt");
            promptText = promptObject != null ? promptObject.GetComponent<Text>() : GetComponentInChildren<Text>();
        }
        if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
    }

    void OnEnable()
    {
        if (interactor != null) interactor.TargetChanged += HandleTargetChanged;
        Refresh(interactor != null ? interactor.CurrentTarget : null);
    }

    void OnDisable()
    {
        if (interactor != null) interactor.TargetChanged -= HandleTargetChanged;
    }

    void HandleTargetChanged(IInteractable target) => Refresh(target);

    void Refresh(IInteractable target)
    {
        if (promptText == null) return;
        promptText.text = target == null ? string.Empty : $"[{keyLabel}] {target.InteractionPrompt}";
    }
}
