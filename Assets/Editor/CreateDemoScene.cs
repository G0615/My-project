using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Editor-only scene authoring. This creates real scene objects once; it does
// not participate in the game's runtime startup path.
[InitializeOnLoad]
public static class CreateDemoScene
{
    const string ScenePath = "Assets/Scenes/CelebrationDemo.unity";

    static CreateDemoScene()
    {
        EditorApplication.delayCall += AutoCreate;
    }

    static void AutoCreate()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            Create();
            return;
        }

    }

    [MenuItem("Tools/Celebration Demo/Create Graybox Scene")]
    public static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var environment = new GameObject("Environment");
        CreatePrimitive("Floor", PrimitiveType.Plane, environment.transform, Vector3.zero, Vector3.one * 3f);
        var testInteractable = CreatePrimitive("TestInteractable", PrimitiveType.Cube, environment.transform, new Vector3(0f, .75f, 2f), Vector3.one * 1.5f, true);
        var materialAction = testInteractable.AddComponent<ChangeMaterialAction>();
        testInteractable.GetComponent<Interactable>().actions = new InteractionAction[] { materialAction };
        CreatePrimitive("ShopBlock", PrimitiveType.Cube, environment.transform, new Vector3(-5f, 1f, 3f), new Vector3(2f, 2f, 2f));
        CreatePrimitive("PlayerHomeBlock", PrimitiveType.Cube, environment.transform, new Vector3(5f, 1f, 3f), new Vector3(2f, 2f, 2f));
        CreatePrimitive("ProcessingStation", PrimitiveType.Cube, environment.transform, new Vector3(0f, .5f, 6f), new Vector3(2f, 1f, 2f), true);

        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1f, -4f);
        player.AddComponent<CharacterController>();
        player.AddComponent<PlayerController>();
        var interactor = player.AddComponent<PlayerInteractor>();

        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "PlayerVisual";
        visual.transform.SetParent(player.transform);
        visual.transform.localPosition = Vector3.zero;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.AddComponent<Camera>();
        var cameraFollow = cameraObject.AddComponent<ThirdPersonCamera>();
        cameraFollow.target = player.transform;
        cameraObject.transform.position = player.transform.position + cameraFollow.offset;

        var hud = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var promptObject = new GameObject("InteractionPrompt", typeof(Text));
        promptObject.transform.SetParent(hud.transform, false);
        var prompt = promptObject.GetComponent<Text>();
        prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        prompt.fontSize = 28;
        prompt.alignment = TextAnchor.MiddleCenter;
        prompt.color = Color.white;
        prompt.rectTransform.anchorMin = new Vector2(.5f, .15f);
        prompt.rectTransform.anchorMax = new Vector2(.5f, .15f);
        prompt.rectTransform.sizeDelta = new Vector2(600f, 60f);
        var interactionHud = hud.AddComponent<InteractionHUD>();
        interactionHud.interactor = interactor;
        interactionHud.promptText = prompt;

        var feedbackObject = new GameObject("Feedback", typeof(Text));
        feedbackObject.transform.SetParent(hud.transform, false);
        var feedback = feedbackObject.GetComponent<Text>();
        feedback.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        feedback.fontSize = 22;
        feedback.alignment = TextAnchor.MiddleCenter;
        feedback.color = Color.yellow;
        feedback.rectTransform.anchorMin = new Vector2(.5f, .08f);
        feedback.rectTransform.anchorMax = new Vector2(.5f, .08f);
        feedback.rectTransform.sizeDelta = new Vector2(600f, 45f);
        var feedbackHud = hud.AddComponent<FeedbackHUD>();
        feedbackHud.interactor = interactor;
        feedbackHud.feedbackText = feedback;

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = player;
        Debug.Log("Created and saved the CelebrationDemo scene with persistent environment and gameplay objects.");
    }

    static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, bool interactable = false)
    {
        var gameObject = GameObject.CreatePrimitive(type);
        gameObject.name = name;
        gameObject.transform.SetParent(parent);
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        if (interactable) gameObject.AddComponent<Interactable>();
        return gameObject;
    }
}
