using UnityEngine;

namespace CelebrationDemo
{
    /// <summary>Orthographic camera that only translates while following a target.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class FixedAngleCamera : MonoBehaviour
    {
        [SerializeField] float followSmoothTime = 0.5f;

        Camera cameraComponent;
        Transform target;
        Vector3 followOffset;
        Vector3 velocity;
        Quaternion fixedRotation;
        float fixedOrthographicSize;
        bool initialized;

        void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            fixedRotation = transform.rotation;
            if (cameraComponent != null && cameraComponent.orthographic)
                fixedOrthographicSize = cameraComponent.orthographicSize;
        }

        public void SetTarget(Transform newTarget, bool immediate = false)
        {
            if (!initialized)
            {
                if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
                fixedRotation = transform.rotation;
                fixedOrthographicSize = cameraComponent != null && cameraComponent.orthographic
                    ? cameraComponent.orthographicSize
                    : 10f;
                if (newTarget != null) followOffset = transform.position - newTarget.position;
                initialized = true;
            }
            else if (target == null && newTarget != null)
            {
                followOffset = transform.position - newTarget.position;
            }

            target = newTarget;
            if (immediate && target != null)
            {
                transform.position = target.position + followOffset;
                velocity = Vector3.zero;
            }
            KeepFixedView();
        }

        void LateUpdate()
        {
            if (target != null)
            {
                Vector3 desired = target.position + followOffset;
                transform.position = Vector3.SmoothDamp(transform.position, desired,
                    ref velocity, Mathf.Max(0.01f, followSmoothTime));
            }
            KeepFixedView();
        }

        void KeepFixedView()
        {
            transform.rotation = fixedRotation;
            if (cameraComponent != null && cameraComponent.orthographic)
                cameraComponent.orthographicSize = fixedOrthographicSize;
        }
    }
}
