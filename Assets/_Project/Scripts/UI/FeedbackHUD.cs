using UnityEngine;
using UnityEngine.UI;

public class FeedbackHUD : MonoBehaviour
{
    public PlayerInteractor interactor;
    public Text feedbackText;
    [SerializeField] float visibleSeconds = 1.5f;
    float remaining;

    void Awake()
    {
        if (feedbackText == null)
        {
            var feedbackObject = transform.Find("Feedback");
            feedbackText = feedbackObject != null ? feedbackObject.GetComponent<Text>() : GetComponentInChildren<Text>();
        }
        if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
        if (feedbackText != null) feedbackText.text = string.Empty;
    }

    void OnEnable()
    {
        if (interactor != null) interactor.InteractionCompleted += HandleInteractionCompleted;
    }

    void OnDisable()
    {
        if (interactor != null) interactor.InteractionCompleted -= HandleInteractionCompleted;
    }

    void Update()
    {
        if (remaining <= 0f || feedbackText == null) return;
        remaining -= Time.deltaTime;
        if (remaining <= 0f) feedbackText.text = string.Empty;
    }

    void HandleInteractionCompleted(InteractionResult result)
    {
        if (feedbackText == null) return;
        feedbackText.text = string.IsNullOrEmpty(result.Message) ? (result.Succeeded ? "Done" : "Cannot interact") : result.Message;
        remaining = visibleSeconds;
    }
}
