using UnityEngine;

namespace CelebrationDemo
{
    /// <summary>
    /// The visual and movement shell for one demo actor. Input is deliberately
    /// supplied by DemoRuntime; this component never reads a device itself.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ActorView : MonoBehaviour
    {
        // Keep the visual identity mapping in the actor domain so the scene
        // builder and HUD cannot drift apart when a role is added or moved.
        static readonly Color[] IdentityColors =
        {
            new Color(0.9f, 0.12f, 0.1f, 1f),
            new Color(0.95f, 0.72f, 0.08f, 1f),
            new Color(0.1f, 0.34f, 0.95f, 1f)
        };

        public int ActorId;
        public Transform HeadAnchor;

        public static Color ColorForActor(int actorId)
        {
            return actorId >= 1 && actorId <= IdentityColors.Length
                ? IdentityColors[actorId - 1]
                : Color.white;
        }

        [SerializeField] CharacterController controller;
        [SerializeField] GameObject selectedVisual;
        [SerializeField] float turnSpeed = 14f;

        Vector3 spawnPosition;
        Quaternion spawnRotation;
        bool hasSpawn;

        void Awake()
        {
            controller = controller != null ? controller : GetComponent<CharacterController>();
            if (HeadAnchor == null)
            {
                var anchor = new GameObject("HeadAnchor");
                anchor.transform.SetParent(transform, false);
                anchor.transform.localPosition = new Vector3(0f, 2.05f, 0f);
                HeadAnchor = anchor.transform;
            }

            if (selectedVisual == null)
            {
                var marker = transform.Find("SelectedMarker");
                if (marker != null) selectedVisual = marker.gameObject;
            }

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            hasSpawn = true;
        }

        /// <summary>Moves using the camera's horizontal plane as the input frame.</summary>
        public void Move(Vector2 input, float speed, Camera camera)
        {
            if (controller == null) controller = GetComponent<CharacterController>();

            Vector3 forward = camera != null ? camera.transform.forward : Vector3.forward;
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            forward.Normalize();
            right.Normalize();

            Vector3 direction = right * input.x + forward * input.y;
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            // Keep the controller grounded without introducing a second input or
            // physics loop. The scene ground is at y=0.
            Vector3 motion = direction * Mathf.Max(0f, speed) * Time.deltaTime;
            if (controller != null)
            {
                if (controller.isGrounded && motion.y <= 0f) motion.y = -0.05f;
                else motion.y = Physics.gravity.y * Time.deltaTime;
                controller.Move(motion);
            }
            else
            {
                transform.position += motion;
            }

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired,
                    1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }
        }

        public void SetActiveVisual(bool active)
        {
            if (selectedVisual == null)
            {
                var marker = transform.Find("SelectedMarker");
                if (marker != null) selectedVisual = marker.gameObject;
            }
            if (selectedVisual != null) selectedVisual.SetActive(active);
        }

        public void ResetPosition()
        {
            if (!hasSpawn) Awake();
            if (controller != null) controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (controller != null) controller.enabled = true;
        }
    }
}
