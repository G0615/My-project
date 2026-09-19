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
        Vector3 authoredFollowOffset;
        Vector3 velocity;
        Quaternion fixedRotation;
        Quaternion authoredRotation;
        bool fixedOrthographic;
        float fixedOrthographicSize;
        float fixedFieldOfView;
        bool initialized;
        bool hasAuthoredFollowOffset;
        bool cinematicView;
        Vector3 cinematicFocus;
        Vector3 cinematicStartPosition;
        Quaternion cinematicStartRotation;
        Quaternion cinematicTargetRotation;
        float cinematicElapsed;
        float cinematicDuration;
        bool keepAuthoredOffsetOnNextTarget;

        /// <summary>The transform currently followed by this camera, if any.</summary>
        public Transform Target => target;

        /// <summary>Configured smoothing duration used for positional following.</summary>
        public float FollowSmoothTime => followSmoothTime;

        /// <summary>True while the celebration presentation owns the camera.</summary>
        public bool IsCinematicView => cinematicView;

        void Awake()
        {
            cameraComponent = GetComponent<Camera>();
            fixedRotation = transform.rotation;
            authoredRotation = fixedRotation;
            CacheFixedView();
        }

        public void SetTarget(Transform newTarget, bool immediate = false)
        {
            bool targetChanged = target != newTarget;
            if (!initialized)
            {
                if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
                fixedRotation = transform.rotation;
                authoredRotation = fixedRotation;
                CacheFixedView();
                if (newTarget != null)
                {
                    followOffset = transform.position - newTarget.position;
                    authoredFollowOffset = followOffset;
                    hasAuthoredFollowOffset = true;
                }
                initialized = true;
            }
            else if (target == null && newTarget != null)
            {
                if (!keepAuthoredOffsetOnNextTarget)
                    followOffset = transform.position - newTarget.position;
                keepAuthoredOffsetOnNextTarget = false;
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

        /// <summary>
        /// Blends from the authored top-down view to a simple front-facing
        /// celebration view. The camera keeps its orthographic projection and
        /// only changes pitch/position for the short presentation sequence.
        /// </summary>
        public void BeginCinematicView(Vector3 focus, float pitch, float duration)
        {
            if (cameraComponent == null) cameraComponent = GetComponent<Camera>();
            if (!initialized)
            {
                fixedRotation = transform.rotation;
                authoredRotation = fixedRotation;
                CacheFixedView();
                initialized = true;
            }

            cinematicView = true;
            target = null;
            keepAuthoredOffsetOnNextTarget = false;
            cinematicFocus = focus;
            cinematicStartPosition = transform.position;
            cinematicStartRotation = transform.rotation;
            cinematicTargetRotation = Quaternion.Euler(pitch, 0f, 0f);
            cinematicElapsed = 0f;
            cinematicDuration = Mathf.Max(0.1f, duration);
            velocity = Vector3.zero;
        }

        /// <summary>Returns to the authored view and follows the active actor.</summary>
        public void EndCinematicView(Transform resumeTarget)
        {
            cinematicView = false;
            fixedRotation = authoredRotation;
            target = resumeTarget;
            followOffset = hasAuthoredFollowOffset ? authoredFollowOffset : followOffset;
            keepAuthoredOffsetOnNextTarget = resumeTarget == null && hasAuthoredFollowOffset;
            velocity = Vector3.zero;
            if (target != null && hasAuthoredFollowOffset)
                transform.position = target.position + followOffset;
            KeepFixedView();
        }

        void LateUpdate()
        {
            if (cinematicView)
            {
                cinematicElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(cinematicElapsed / cinematicDuration);
                t = t * t * (3f - 2f * t);
                Vector3 offset = -(cinematicTargetRotation * Vector3.forward).normalized * 21f + Vector3.up;
                transform.position = Vector3.Lerp(cinematicStartPosition, cinematicFocus + offset, t);
                transform.rotation = Quaternion.Slerp(cinematicStartRotation, cinematicTargetRotation, t);
                KeepFixedProjection();
                return;
            }

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
            KeepFixedProjection();
        }

        void KeepFixedProjection()
        {
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
