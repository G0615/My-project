using UnityEngine;

namespace CelebrationDemo
{
    /// <summary>Orthographic camera that only translates while following a target.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class FixedAngleCamera : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] float followSmoothTime = 0.5f;

        Camera cameraComponent;
        Transform target;
        Vector3 followOffset;
        Vector3 velocity;
        Quaternion fixedRotation;
        bool fixedOrthographic;
        float fixedOrthographicSize;
        float fixedFieldOfView;
        bool initialized;

        /// <summary>The transform currently followed by this camera, if any.</summary>
        public Transform Target => target;

        /// <summary>Configured smoothing duration used for positional following.</summary>
        public float FollowSmoothTime => followSmoothTime;

        void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            fixedRotation = transform.rotation;
            CacheFixedView();
        }

        public void SetTarget(Transform newTarget, bool immediate = false)
        {
            bool targetChanged = target != newTarget;
            if (!initialized)
            {
                if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
                fixedRotation = transform.rotation;
                CacheFixedView();
                if (newTarget != null) followOffset = transform.position - newTarget.position;
                initialized = true;
            }
            else if (target == null && newTarget != null)
            {
                followOffset = transform.position - newTarget.position;
            }

            // A target switch starts from the camera's current position. Carrying
            // velocity from the previous actor would make rapid 1 -> 2 -> 3
            // switches behave like one chained animation and can overshoot the
            // latest target.
            if (targetChanged) velocity = Vector3.zero;
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
            if (cameraComponent == null) return;
            cameraComponent.orthographic = fixedOrthographic;
            if (fixedOrthographic)
                cameraComponent.orthographicSize = fixedOrthographicSize;
            else
                cameraComponent.fieldOfView = fixedFieldOfView;
        }

        void CacheFixedView()
        {
            fixedOrthographic = cameraComponent != null && cameraComponent.orthographic;
            if (fixedOrthographic)
                fixedOrthographicSize = cameraComponent.orthographicSize;
            else if (cameraComponent != null)
                fixedFieldOfView = cameraComponent.fieldOfView;
        }
    }
}
