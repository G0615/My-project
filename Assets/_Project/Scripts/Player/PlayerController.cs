using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;
    CharacterController characterController;
    Camera playerCamera;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerCamera = Camera.main;
    }

    void Update()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            input = new Vector2(
                (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
                (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0));
        }

        if (playerCamera == null) playerCamera = Camera.main;
        var forward = playerCamera != null ? Vector3.Scale(playerCamera.transform.forward, new Vector3(1f, 0f, 1f)).normalized : Vector3.forward;
        var right = playerCamera != null ? playerCamera.transform.right : Vector3.right;
        var direction = (forward * input.y + right * input.x).normalized;
        if (direction.sqrMagnitude > .01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 12f * Time.deltaTime);
        characterController.SimpleMove(direction * moveSpeed);
    }
}
