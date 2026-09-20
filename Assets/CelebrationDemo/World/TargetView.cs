using UnityEngine;

namespace CelebrationDemo
{
    /// <summary>
    /// A stable interaction point. It owns only presentation; all decisions are
    /// made by DemoSession and forwarded here by DemoRuntime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TargetView : MonoBehaviour
    {
        public TargetSpec Spec;
        public string DisplayName;
        public Transform FeedbackAnchor;
        public float InteractionRadius = 2.4f;
        // For cream slots this points at the matching 60-degree cake sector.
        // The presentation colours the whole sector rather than a small dot.
        public GameObject CakeSectorVisual;

        /// <summary>
        /// Internal deterministic key used only when two targets tie during
        /// selection. It deliberately never appears in the HUD prompt.
        /// </summary>
        public string StableSelectionKey
        {
            get
            {
                if (Spec != null && !string.IsNullOrEmpty(Spec.Id)) return Spec.Id;
                if (!string.IsNullOrEmpty(DisplayName)) return DisplayName;
                return name ?? string.Empty;
            }
        }

        /// <summary>Whether this target is the runtime's current interaction target.</summary>
        public bool IsHighlighted { get; private set; }

        [SerializeField] GameObject highlightVisual;

        float toolAngle;

        void Awake()
        {
            CacheChildren();
            HideLegacyStationVisuals();
        }

        void CacheChildren()
        {
            if (FeedbackAnchor == null)
            {
                var anchor = transform.Find("FeedbackAnchor");
                if (anchor != null) FeedbackAnchor = anchor;
            }
            if (highlightVisual == null)
            {
                var highlight = transform.Find("Highlight");
                if (highlight != null) highlightVisual = highlight.gameObject;
            }
        }

        public void SetHighlighted(bool value)
        {
            IsHighlighted = value;
            if (highlightVisual == null) CacheChildren();
            if (highlightVisual != null) highlightVisual.SetActive(value);
        }

        public void ApplyState(DemoSession session)
        {
            if (session == null || Spec == null) return;

            switch (Spec.Kind)
            {
                case TargetKind.CakeFruit:
                    ApplyCakeFruit(session);
                    break;
                case TargetKind.CakeCream:
                    ApplyCakeCream(session);
                    break;
                case TargetKind.CutStation:
                    ApplyStation(session.CutStation, false);
                    break;
                case TargetKind.WhipStation:
                    ApplyStation(session.WhipStation, true);
                    break;
                case TargetKind.Trophy:
                    ApplyTrophy(session);
                    break;
            }
        }

        void ApplyCakeFruit(DemoSession session)
        {
            int style = ReadCakeValue(session.Cake != null ? session.Cake.FruitStyles : null, Spec.Index);
            // One interaction target represents a small three-fruit cluster.
            // Keep the target and state slot singular, while applying the
            // same visibility and style to every authored FruitDecoration
            // child (FruitDecoration, FruitDecoration 2, FruitDecoration 3).
            var children = GetComponentsInChildren<Transform>(true);
            Color color = FruitColor(style);
            for (int i = 0; i < children.Length; i++)
            {
                var decoration = children[i];
                if (decoration == transform || !decoration.name.StartsWith("FruitDecoration"))
                    continue;

                decoration.gameObject.SetActive(style > 0);
                SetColor(decoration, color);
            }
        }

        void ApplyCakeCream(DemoSession session)
        {
            int style = ReadCakeValue(session.Cake != null ? session.Cake.CreamColors : null, Spec.Index);
            var sector = CakeSectorVisual != null ? CakeSectorVisual.transform : null;
            if (sector == null && Spec != null)
            {
                // Older serialized scenes may not yet contain the new object
                // reference. Resolve the alternating cream sector by its
                // stable authored name so the whole wedge still updates.
                var sectorName = "Cake Sector " + (Spec.Index * 2 + 2) + " Top";
                var legacySceneSector = GameObject.Find(sectorName);
                if (legacySceneSector != null) sector = legacySceneSector.transform;
            }
            if (sector != null) sector.gameObject.SetActive(true);
            SetColor(sector, CreamColor(style));
            var legacyPoint = transform.Find("CreamSurface");
            if (legacyPoint != null) legacyPoint.gameObject.SetActive(false);
        }

        void ApplyStation(StationState station, bool whisk)
        {
            if (station == null) return;
            HideLegacyStationVisuals();

            var tool = transform.Find("Tool");
            if (tool != null && station.IsRunning && IsStationVisualOwner(station))
            {
                toolAngle += Time.deltaTime * (whisk ? 420f : 250f);
                tool.localRotation = Quaternion.Euler(0f, toolAngle, whisk ? 18f : 0f);
            }
        }

        bool IsStationVisualOwner(StationState station)
        {
            if (Spec == null || station == null || !station.IsRunning) return false;
            // Older callers may not set the target ID. In that case the
            // canonical station target owns the presentation.
            string owner = string.IsNullOrEmpty(station.ActiveTargetId) ? station.Id : station.ActiveTargetId;
            return string.Equals(Spec.Id, owner, System.StringComparison.Ordinal);
        }

        void HideLegacyStationVisuals()
        {
            if (Spec == null || (Spec.Kind != TargetKind.CutStation && Spec.Kind != TargetKind.WhipStation))
                return;

            var progress = transform.Find("ProgressBar");
            if (progress != null) progress.gameObject.SetActive(false);
            var progressBack = transform.Find("ProgressBarBack");
            if (progressBack != null) progressBack.gameObject.SetActive(false);
            var participants = transform.Find("Participants");
            if (participants != null) participants.gameObject.SetActive(false);
        }

        void ApplyTrophy(DemoSession session)
        {
            ActorState actor = session.GetActor(Spec.OwnerActorId);
            bool placed = actor != null && actor.TrophyStatus == TrophyStatus.Placed;
            var trophy = transform.Find("TrophyVisual");
            if (trophy != null) trophy.gameObject.SetActive(placed);
            Color ownerColor = ActorColor(Spec.OwnerActorId);
            SetColor(trophy, ownerColor);
        }

        static int ReadCakeValue(int[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : 0;
        }

        static Color FruitColor(int style)
        {
            switch (Mathf.Abs(style) % 4)
            {
                case 1: return new Color(0.95f, 0.16f, 0.1f);
                case 2: return new Color(1f, 0.56f, 0.08f);
                case 3: return new Color(0.95f, 0.84f, 0.1f);
                default: return new Color(0.68f, 0.12f, 0.08f);
            }
        }

        static Color CreamColor(int style)
        {
            switch (Mathf.Abs(style) % 4)
            {
                case 1: return new Color(1f, 0.46f, 0.7f);
                case 2: return new Color(0.56f, 0.82f, 1f);
                case 3: return new Color(0.75f, 0.5f, 1f);
                default: return new Color(0.98f, 0.93f, 0.78f);
            }
        }

        static Color ActorColor(int actorId)
        {
            switch (actorId)
            {
                case 1: return new Color(0.95f, 0.18f, 0.17f);
                case 2: return new Color(1f, 0.82f, 0.12f);
                case 3: return new Color(0.16f, 0.48f, 1f);
                default: return Color.white;
            }
        }

        static void SetColor(Transform root, Color color)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMaterial == null) continue;
                // Each generated visual has its own material. Use an instance so
                // a state change cannot recolor another interaction point, and
                // write both common color property names for URP and fallback
                // built-in shaders.
                var material = renderer.material;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            }
        }
    }
}
