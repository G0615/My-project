using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CelebrationDemo
{
    /// <summary>
    /// The single screen HUD for the celebration prototype.
    ///
    /// This component is deliberately presentation-only.  It reads the shared
    /// session through DemoRuntime and never executes an action or changes core
    /// state.  Runtime creates the HUD in a scene, then calls Initialize once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DemoHud : MonoBehaviour
    {
        const int RecentLogLimit = 100;
        const float BubbleLifetime = 1.2f;
        const float ReferenceWidth = 1280f;
        const float ReferenceHeight = 720f;
        const float ScreenEdgePadding = 12f;
        const float CompletionFlashPhase = 0.14f;
        const int CompletionFlashCount = 4;

        static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.085f, 0.90f);
        static readonly Color PanelLightColor = new Color(0.08f, 0.115f, 0.17f, 0.94f);
        static readonly Color MutedText = new Color(0.72f, 0.78f, 0.86f, 1f);
        static readonly Color Accent = new Color(1f, 0.78f, 0.28f, 1f);
        static readonly Color CompletionFlashColor = new Color(1f, 0.92f, 0.36f, 1f);
        [SerializeField] DemoRuntime runtime;

        Canvas canvas;
        RectTransform canvasRect;
        Font chineseFont;
        GameObject hudRoot;
        GameObject modalRoot;
        RectTransform worldBubbleRoot;
        readonly Dictionary<TargetView, WorldLabel> worldLabels = new Dictionary<TargetView, WorldLabel>();

        readonly List<Text> logEntries = new List<Text>();
        readonly List<string> recentLogEventIds = new List<string>();
        readonly List<FloatingBubble> bubbles = new List<FloatingBubble>();
        readonly Text[] actorCardTexts = new Text[3];
        readonly Image[] stationProgressImages = new Image[2];
        readonly Image[] stationProgressBackgrounds = new Image[2];
        readonly GameObject[] stationProgressRoots = new GameObject[2];
        readonly StationProgressVisual[] stationProgressVisuals =
            { new StationProgressVisual(), new StationProgressVisual() };
        readonly Text[] stationTexts = new Text[2];

        Text activeActorText;
        Text targetPromptText;
        Text controlText;
        Text logTitleText;
        ScrollRect logScroll;
        RectTransform logContent;
        Text modalTitleText;
        Text modalSubtitleText;
        ScrollRect modalScroll;
        RectTransform modalContent;
        Text celebrationHintText;

        bool initialized;
        bool modalOpen;
        int modalActorId;

        /// <summary>True while the modal review or celebration panel is visible.</summary>
        public bool IsModalOpen { get { return modalOpen; } }

        void Awake()
        {
            if (runtime == null) runtime = GetComponent<DemoRuntime>();
        }

        /// <summary>Builds the runtime UI and attaches it to the one shared runtime.</summary>
        public void Initialize(DemoRuntime demoRuntime)
        {
            runtime = demoRuntime;
            EnsureCanvas();
            EnsureInputModule();
            BuildHud();
            initialized = true;
            modalOpen = false;
            if (modalRoot != null) modalRoot.SetActive(false);
            RefreshHud();
        }

        /// <summary>Receives an already-recorded core event and presents it.</summary>
        public void OnAction(ActionEvent action)
        {
            if (!initialized || action == null) return;

            // EventRecorded can arrive before LateUpdate. Prune here as well as
            // in the frame loop so a rapid sequence cannot stack against an
            // already expired bubble that is waiting for deferred destruction.
            PruneExpiredBubbles();

            // Record is already committed before EventRecorded is raised. Read
            // the session window here so this event also follows the same
            // idempotent path used by the per-frame HUD refresh.
            RefreshRecentLogs();

            // Shared completion events carry a participant list.  Give every
            // real participant the same short actor feedback and keep the
            // single shared output line in the log.
            if (action.ActorId != 0)
            {
                var actor = FindActor(action.ActorId);
                SpawnBubble(actor != null ? ActorAnchor(actor) : null,
                    ShortFeedback(action.ActorText, action.Message, action.TargetId), ActorColor(action.ActorId));
            }
            else if (action.ParticipantIds != null)
            {
                foreach (var actorId in action.ParticipantIds.Distinct())
                {
                    var actor = FindActor(actorId);
                    SpawnBubble(actor != null ? ActorAnchor(actor) : null,
                        ShortFeedback(action.ActorText, action.Message, action.TargetId), ActorColor(actorId));
                }
            }

            var target = FindTarget(action.TargetId);
            if (target != null)
                SpawnBubble(TargetAnchor(target),
                    ShortFeedback(action.TargetText, action.Message, action.TargetId), MutedText);
        }

        /// <summary>Shows the complete personal history for the actor who viewed a trophy.</summary>
        public void ShowHistory(int actorId)
        {
            if (!initialized || runtime == null || runtime.Session == null) return;

            modalActorId = actorId;
            OpenModal("活动回顾", actorId + "号玩家 · 本次活动回顾");
            ClearModalRows();

            var state = runtime.Session.GetActor(actorId);
            AddModalRow("称号  " + FormatTitles(state), ActorColor(actorId), true);
            AddModalRow("下面是从活动开始到现在的完整行为记录。", MutedText, false);

            var history = runtime.Session.GetHistory(actorId);
            if (history == null || history.Count == 0)
            {
                AddModalRow("暂无行为记录。", MutedText, false);
            }
            else
            {
                foreach (var action in history)
                {
                    if (action == null) continue;
                    AddModalRow(FormatActionMessage(action), LogColor(action), false);
                }
            }

            if (celebrationHintText != null)
                celebrationHintText.text = "鼠标滚轮浏览 · 按 Esc 或点击关闭，继续体验";
            ScrollToLatest(modalScroll);
        }

        /// <summary>Shows the three-player trophy and title result after a celebration.</summary>
        public void ShowCelebration()
        {
            if (!initialized || runtime == null || runtime.Session == null) return;

            modalActorId = 0;
            OpenModal("庆典完成", "三人奖杯已颁发 · 本轮称号结算结果");
            ClearModalRows();

            AddModalRow("★  庆典已举办！每位玩家都获得了一座奖杯。", Accent, true);
            for (var actorId = 1; actorId <= 3; actorId++)
            {
                var state = runtime.Session.GetActor(actorId);
                var title = actorId + "号玩家    " + FormatTitles(state);
                AddModalRow(title + "    奖杯 +1", ActorColor(actorId), true);
            }
            AddModalRow("后续可回到家园放置奖杯，再次按 F 查看个人完整回顾。", MutedText, false);
            if (celebrationHintText != null)
                celebrationHintText.text = "按 Esc 或点击关闭，继续体验";

            ScrollToLatest(modalScroll);
        }

        /// <summary>Closes a modal without touching the world or core session.</summary>
        public void CloseModal()
        {
            modalOpen = false;
            if (modalRoot != null) modalRoot.SetActive(false);
        }

        /// <summary>Clears presentation state after DemoRuntime has reset the session.</summary>
        public void ResetView()
        {
            CloseModal();
            ClearRecentLogs();

            for (var i = bubbles.Count - 1; i >= 0; i--)
                DestroyBubbleAt(i);

            foreach (var pair in worldLabels)
                if (pair.Value != null && pair.Value.root != null) Destroy(pair.Value.root);
            worldLabels.Clear();

            ResetStationProgressVisuals();
            RefreshHud();
        }

        void LateUpdate()
        {
            if (!initialized) return;
            RefreshHud();
            RefreshWorldLabels();
            UpdateBubbles();
            UpdateWorldLabels();
        }

        void EnsureCanvas()
        {
            if (canvas != null) return;

            var canvasObject = new GameObject("CelebrationDemo Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;
            chineseFont = FindChineseFont();
        }

        void EnsureInputModule()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventObject = new GameObject("CelebrationDemo EventSystem",
                    typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem = eventObject.GetComponent<EventSystem>();
            }

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                var oldModule = eventSystem.GetComponent<StandaloneInputModule>();
                if (oldModule != null) Destroy(oldModule);
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        void BuildHud()
        {
            if (hudRoot != null) Destroy(hudRoot);
            logEntries.Clear();
            recentLogEventIds.Clear();

            hudRoot = new GameObject("HUD", typeof(RectTransform));
            hudRoot.transform.SetParent(canvas.transform, false);
            var hudRect = hudRoot.GetComponent<RectTransform>();
            Stretch(hudRect);

            BuildActorStrip(hudRect);
            BuildStationStrip(hudRect);
            BuildTargetPrompt(hudRect);
            BuildControlHint(hudRect);
            BuildLogPanel(hudRect);
            BuildWorldBubbles(hudRect);
            BuildModal(hudRect);
        }

        void BuildActorStrip(RectTransform parent)
        {
            var panel = Panel(parent, "角色状态", new Vector2(0.025f, 0.765f), new Vector2(0.68f, 0.965f), PanelColor);
            AddText(panel, "三人协作 · 当前状态", 17, Accent, TextAnchor.UpperLeft,
                new Vector2(0.025f, 0.72f), new Vector2(0.32f, 0.99f));

            activeActorText = AddText(panel, "当前主控", 15, Color.white, TextAnchor.UpperRight,
                new Vector2(0.52f, 0.72f), new Vector2(0.98f, 0.99f));

            for (var i = 0; i < 3; i++)
            {
                var card = Panel(panel, "角色" + (i + 1),
                    new Vector2(0.012f + i * 0.326f, 0.08f),
                    new Vector2(0.32f + i * 0.326f, 0.69f),
                    new Color(0.08f, 0.10f, 0.145f, 0.95f));
                var marker = Image(card.gameObject, "颜色", ActorColor(i + 1));
                var markerRect = marker.rectTransform;
                markerRect.anchorMin = new Vector2(0.02f, 0.16f);
                markerRect.anchorMax = new Vector2(0.045f, 0.86f);
                markerRect.offsetMin = markerRect.offsetMax = Vector2.zero;
                actorCardTexts[i] = AddText(card, "", 14, Color.white, TextAnchor.UpperLeft,
                    new Vector2(0.07f, 0.06f), new Vector2(0.98f, 0.93f));
            }
        }

        void BuildStationStrip(RectTransform parent)
        {
            var panel = Panel(parent, "共享工位", new Vector2(0.025f, 0.57f), new Vector2(0.335f, 0.745f), PanelColor);
            AddText(panel, "共享工位", 16, Accent, TextAnchor.UpperLeft,
                new Vector2(0.04f, 0.76f), new Vector2(0.43f, 0.98f));
            AddText(panel, "多人加入会实时加速；离开后仍继续", 12, MutedText, TextAnchor.UpperRight,
                new Vector2(0.40f, 0.76f), new Vector2(0.96f, 0.98f));

            for (var i = 0; i < 2; i++)
            {
                var row = Panel(panel, i == 0 ? "切果工位" : "打发工位",
                    new Vector2(0.04f, 0.40f - i * 0.32f),
                    new Vector2(0.96f, 0.68f - i * 0.32f),
                    new Color(0.07f, 0.09f, 0.13f, 0.95f));
                stationTexts[i] = AddText(row, "", 13, Color.white, TextAnchor.UpperLeft,
                    new Vector2(0.04f, 0.50f), new Vector2(0.96f, 0.93f));

                var barBack = Image(row.gameObject, "进度底", new Color(0.02f, 0.025f, 0.04f, 1f));
                var barBackRect = barBack.rectTransform;
                barBackRect.anchorMin = new Vector2(0.04f, 0.13f);
                barBackRect.anchorMax = new Vector2(0.96f, 0.42f);
                barBackRect.offsetMin = barBackRect.offsetMax = Vector2.zero;
                stationProgressRoots[i] = barBack.gameObject;
                stationProgressBackgrounds[i] = barBack;
                var fill = Image(barBack.gameObject, "进度", i == 0 ? new Color(0.98f, 0.34f, 0.32f, 1f) : new Color(0.36f, 0.76f, 1f, 1f));
                var fillRect = fill.rectTransform;
                Stretch(fillRect);
                fill.type = UnityEngine.UI.Image.Type.Filled;
                fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                fill.fillOrigin = 0;
                fill.fillAmount = 0f;
                stationProgressImages[i] = fill;
                stationProgressRoots[i].SetActive(false);
                stationProgressVisuals[i].normalFill = fill.color;
                stationProgressVisuals[i].normalBackground = barBack.color;
            }
        }

        void BuildTargetPrompt(RectTransform parent)
        {
            var panel = Panel(parent, "交互提示", new Vector2(0.285f, 0.025f), new Vector2(0.695f, 0.18f), PanelLightColor);
            targetPromptText = AddText(panel, "靠近目标以互动", 20, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.24f), new Vector2(0.97f, 0.91f));
            AddText(panel, "唯一交互提示", 11, MutedText, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.24f));
        }

        void BuildControlHint(RectTransform parent)
        {
            var panel = Panel(parent, "操作说明", new Vector2(0.025f, 0.025f), new Vector2(0.265f, 0.18f), PanelColor);
            controlText = AddText(panel,
                "WASD / 方向键  移动\n1 / 2 / 3  切换角色\nF  互动     Esc  关闭回顾\nR  重置本轮",
                13, MutedText, TextAnchor.MiddleLeft,
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
        }

        void BuildLogPanel(RectTransform parent)
        {
            var panel = Panel(parent, "行为日志", new Vector2(0.715f, 0.145f), new Vector2(0.98f, 0.965f), PanelColor);
            logTitleText = AddText(panel, "最近行为 · 0 / 100", 17, Accent, TextAnchor.UpperLeft,
                new Vector2(0.045f, 0.94f), new Vector2(0.95f, 0.995f));

            var viewportImage = Image(panel.gameObject, "日志视口", new Color(0.015f, 0.022f, 0.038f, 0.50f));
            var viewport = viewportImage.rectTransform;
            viewport.anchorMin = new Vector2(0.025f, 0.045f);
            viewport.anchorMax = new Vector2(0.975f, 0.91f);
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject("日志内容", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            logContent = contentObject.GetComponent<RectTransform>();
            logContent.anchorMin = new Vector2(0f, 1f);
            logContent.anchorMax = new Vector2(1f, 1f);
            logContent.pivot = new Vector2(0.5f, 1f);
            logContent.anchoredPosition = Vector2.zero;
            logContent.sizeDelta = new Vector2(0f, 0f);
            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            logScroll = panel.gameObject.AddComponent<ScrollRect>();
            logScroll.viewport = viewport;
            logScroll.content = logContent;
            logScroll.horizontal = false;
            logScroll.vertical = true;
            logScroll.movementType = ScrollRect.MovementType.Clamped;
            logScroll.scrollSensitivity = 30f;
        }

        void BuildWorldBubbles(RectTransform parent)
        {
            var bubbleObject = new GameObject("世界反馈", typeof(RectTransform));
            bubbleObject.transform.SetParent(parent, false);
            worldBubbleRoot = bubbleObject.GetComponent<RectTransform>();
            Stretch(worldBubbleRoot);
        }

        void BuildModal(RectTransform parent)
        {
            modalRoot = new GameObject("回顾与庆典弹窗", typeof(RectTransform), typeof(CanvasGroup));
            modalRoot.transform.SetParent(parent, false);
            var modalRect = modalRoot.GetComponent<RectTransform>();
            Stretch(modalRect);

            var backdrop = Image(modalRoot, "遮罩", new Color(0.005f, 0.01f, 0.025f, 0.82f));
            Stretch(backdrop.rectTransform);

            var panel = Panel(modalRect, "弹窗面板", new Vector2(0.12f, 0.09f), new Vector2(0.88f, 0.91f), PanelLightColor);
            modalTitleText = AddText(panel, "", 26, Accent, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.89f), new Vector2(0.72f, 0.97f));
            modalSubtitleText = AddText(panel, "", 14, MutedText, TextAnchor.UpperLeft,
                new Vector2(0.06f, 0.82f), new Vector2(0.80f, 0.90f));

            var closeButton = Button(panel, "关闭", "关闭", new Vector2(0.84f, 0.875f), new Vector2(0.95f, 0.96f));
            closeButton.onClick.AddListener(CloseModal);

            var viewportImage = Image(panel.gameObject, "回顾视口", new Color(0.012f, 0.02f, 0.035f, 0.72f));
            var viewport = viewportImage.rectTransform;
            viewport.anchorMin = new Vector2(0.045f, 0.12f);
            viewport.anchorMax = new Vector2(0.955f, 0.79f);
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject("回顾内容", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            modalContent = contentObject.GetComponent<RectTransform>();
            modalContent.anchorMin = new Vector2(0f, 1f);
            modalContent.anchorMax = new Vector2(1f, 1f);
            modalContent.pivot = new Vector2(0.5f, 1f);
            modalContent.anchoredPosition = Vector2.zero;
            modalContent.sizeDelta = Vector2.zero;
            var layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(16, 16, 14, 14);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            modalScroll = panel.gameObject.AddComponent<ScrollRect>();
            modalScroll.viewport = viewport;
            modalScroll.content = modalContent;
            modalScroll.horizontal = false;
            modalScroll.vertical = true;
            modalScroll.movementType = ScrollRect.MovementType.Clamped;
            modalScroll.scrollSensitivity = 35f;

            celebrationHintText = AddText(panel, "按 Esc 或点击关闭，继续体验", 13, MutedText, TextAnchor.MiddleCenter,
                new Vector2(0.20f, 0.035f), new Vector2(0.80f, 0.10f));
        }

        void RefreshHud()
        {
            if (runtime == null || runtime.Session == null) return;

            RefreshRecentLogs();

            var active = runtime.ActiveActorId;
            if (activeActorText != null)
                activeActorText.text = "当前主控  " + active + "号玩家";

            for (var actorId = 1; actorId <= 3; actorId++)
            {
                var state = runtime.Session.GetActor(actorId);
                if (actorCardTexts[actorId - 1] == null) continue;
                var speed = runtime.Session.GetSpeed(actorId);
                var chopsticks = runtime.Session.HasChopsticks(actorId);
                var chopstickSeconds = state == null ? 0 : Math.Max(0, runtime.Session.Config.ChopsticksSeconds > 0
                    ? state.ChopsticksExpiresAt - runtime.Session.Now : 0);
                var slowSeconds = state == null ? 0 : Math.Max(0, state.SlowExpiresAt - runtime.Session.Now);
                var marker = actorId == active ? "▶ " : "  ";
                actorCardTexts[actorId - 1].text = marker + actorId + "号玩家\n" +
                    "移速 " + speed.ToString("0.0") + "    " +
                    (chopsticks ? "筷子 " + chopstickSeconds.ToString("0") + "秒" : "无筷子") + "\n" +
                    (state != null && state.SlowStacks > 0
                        ? "减速 ×" + state.SlowStacks + "  " + slowSeconds.ToString("0") + "秒"
                        : "状态正常");
                actorCardTexts[actorId - 1].color = ActorColor(actorId);
            }

            RefreshStations();
            RefreshTargetPrompt();
        }

        void RefreshWorldLabels()
        {
            if (runtime == null || runtime.Targets == null || worldBubbleRoot == null) return;

            var visibleTargets = new HashSet<TargetView>();
            foreach (var target in runtime.Targets)
            {
                if (target == null || target.Spec == null) continue;
                visibleTargets.Add(target);
                WorldLabel label;
                if (!worldLabels.TryGetValue(target, out label) || label == null || label.root == null)
                {
                    label = CreateWorldLabel(target);
                    worldLabels[target] = label;
                }

                var title = string.IsNullOrEmpty(target.DisplayName)
                    ? TargetKindName(target.Spec.Kind)
                    : target.DisplayName;
                var station = target.Spec.Kind == TargetKind.CutStation
                    ? runtime.Session.CutStation
                    : target.Spec.Kind == TargetKind.WhipStation ? runtime.Session.WhipStation : null;
                if (station != null)
                {
                    var people = station.ParticipantIds == null ? 0 : station.ParticipantIds.Count;
                    label.title.text = title + "\n" + (station.IsRunning ? "制作中 " : "待机 ") +
                        people + "人";
                }
                else
                {
                    label.title.text = (target == runtime.CurrentTarget ? "◆ " : "") + title;
                }
                label.anchor = TargetAnchor(target);
                label.root.SetActive(target == runtime.CurrentTarget);
            }

            var stale = new List<TargetView>();
            foreach (var pair in worldLabels)
                if (!visibleTargets.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (var target in stale)
            {
                if (worldLabels[target] != null && worldLabels[target].root != null)
                    Destroy(worldLabels[target].root);
                worldLabels.Remove(target);
            }
        }

        WorldLabel CreateWorldLabel(TargetView target)
        {
            var objectLabel = new GameObject("世界目标标签", typeof(RectTransform), typeof(Image));
            objectLabel.transform.SetParent(worldBubbleRoot, false);
            var rect = objectLabel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(175f, 46f);
            var background = objectLabel.GetComponent<Image>();
            background.color = new Color(0.015f, 0.025f, 0.045f, 0.78f);
            var title = AddText(rect, "", 13, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.03f, 0.27f), new Vector2(0.97f, 0.97f));
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            return new WorldLabel
            {
                root = objectLabel,
                rect = rect,
                title = title,
                anchor = TargetAnchor(target)
            };
        }

        void RefreshStations()
        {
            var stations = new[] { runtime.Session.CutStation, runtime.Session.WhipStation };
            var labels = new[] { "切水果", "打发奶油" };
            for (var i = 0; i < stations.Length; i++)
            {
                var station = stations[i];
                if (station == null) continue;
                var participants = station.ParticipantIds == null
                    ? ""
                    : string.Join("、", station.ParticipantIds.Distinct().Select(id => id + "号"));
                stationTexts[i].text = labels[i] + "    " + (station.IsRunning ? "制作中" : "空闲") +
                    (string.IsNullOrEmpty(participants) ? "" : "    参与 " + participants);
                RefreshStationProgress(i, station);
            }
        }

        void RefreshStationProgress(int index, StationState station)
        {
            var visual = stationProgressVisuals[index];
            var root = stationProgressRoots[index];
            var background = stationProgressBackgrounds[index];
            var fill = stationProgressImages[index];
            if (visual == null || root == null || background == null || fill == null || station == null)
                return;

            if (station.IsRunning)
            {
                visual.batchId = station.BatchId;
                visual.wasRunning = true;
                visual.completionAnimating = false;
                root.SetActive(true);
                background.color = visual.normalBackground;
                fill.color = visual.normalFill;
                fill.fillAmount = Mathf.Clamp01(station.Progress);
                return;
            }

            if (visual.wasRunning && station.Progress >= 1f && !visual.completionAnimating)
            {
                visual.wasRunning = false;
                visual.completionAnimating = true;
                visual.completionStartedAt = Time.unscaledTime;
            }

            if (visual.completionAnimating)
            {
                var phase = Mathf.FloorToInt((Time.unscaledTime - visual.completionStartedAt) /
                    CompletionFlashPhase);
                if (phase >= CompletionFlashCount)
                {
                    visual.completionAnimating = false;
                    root.SetActive(false);
                    background.color = visual.normalBackground;
                    fill.color = visual.normalFill;
                    fill.fillAmount = 0f;
                    return;
                }

                var visible = phase % 2 == 0;
                root.SetActive(visible);
                background.color = visible ? CompletionFlashColor : visual.normalBackground;
                fill.color = visible ? CompletionFlashColor : visual.normalFill;
                fill.fillAmount = 1f;
                return;
            }

            visual.wasRunning = false;
            root.SetActive(false);
            fill.fillAmount = 0f;
        }

        void ResetStationProgressVisuals()
        {
            for (var i = 0; i < stationProgressVisuals.Length; i++)
            {
                var visual = stationProgressVisuals[i];
                visual.wasRunning = false;
                visual.completionAnimating = false;
                visual.completionStartedAt = 0f;
                visual.batchId = 0;
                if (stationProgressRoots[i] != null) stationProgressRoots[i].SetActive(false);
                if (stationProgressBackgrounds[i] != null)
                    stationProgressBackgrounds[i].color = visual.normalBackground;
                if (stationProgressImages[i] != null)
                {
                    stationProgressImages[i].color = visual.normalFill;
                    stationProgressImages[i].fillAmount = 0f;
                }
            }
        }

        void RefreshTargetPrompt()
        {
            if (targetPromptText == null) return;
            var target = runtime.CurrentTarget;
            if (target == null || target.Spec == null)
            {
                targetPromptText.text = "靠近目标以互动";
                return;
            }

            var offer = runtime.Session.Resolve(runtime.ActiveActorId, target.Spec);
            targetPromptText.text = FormatTargetPrompt(target, offer);
        }

        /// <summary>
        /// Builds the small prompt from the same resolved offer that F uses.
        /// DisplayName is the readable target text; TargetSpec.Id stays internal.
        /// </summary>
        internal static string FormatTargetPrompt(TargetView target, InteractionOffer offer)
        {
            if (target == null || target.Spec == null) return "靠近目标以互动";
            var displayName = string.IsNullOrEmpty(target.DisplayName)
                ? TargetKindName(target.Spec.Kind)
                : target.DisplayName;
            if (offer != null && offer.CanExecute)
                return "[ F ]  " + offer.Label + "\n" + displayName;
            if (offer != null)
                return displayName + "\n" + offer.Label;
            return "[ F ]  互动\n" + displayName;
        }

        void RefreshRecentLogs()
        {
            if (!initialized || runtime == null || runtime.Session == null || logContent == null) return;

            var history = runtime.Session.History;
            var first = history == null ? 0 : Math.Max(0, history.Count - RecentLogLimit);
            var recent = new List<ActionEvent>();
            if (history != null)
            {
                for (var i = first; i < history.Count; i++)
                {
                    var action = history[i];
                    if (action != null) recent.Add(action);
                }
            }

            var ids = recent.Select(ActionEventKey).ToList();
            if (ids.SequenceEqual(recentLogEventIds)) return;

            ClearRecentLogs();
            for (var i = 0; i < recent.Count; i++)
            {
                var action = recent[i];
                AddRecentLogRow(FormatActionMessage(action), LogColor(action));
                recentLogEventIds.Add(ids[i]);
            }
            if (logTitleText != null) logTitleText.text = "最近行为 · " + logEntries.Count + " / 100";
            ScrollToLatest(logScroll);
        }

        void ClearRecentLogs()
        {
            if (logContent != null) logContent.gameObject.SetActive(false);
            for (var i = 0; i < logEntries.Count; i++)
            {
                var entry = logEntries[i];
                if (entry == null) continue;
                entry.gameObject.SetActive(false);
                Destroy(entry.gameObject);
            }
            logEntries.Clear();
            recentLogEventIds.Clear();
            if (logContent != null) logContent.gameObject.SetActive(true);
            if (logTitleText != null) logTitleText.text = "最近行为 · 0 / 100";
        }

        static string ActionEventKey(ActionEvent action)
        {
            if (action == null) return string.Empty;
            if (!string.IsNullOrEmpty(action.EventId)) return action.EventId;
            return (action.ActivityId ?? string.Empty) + "#" + action.Sequence.ToString();
        }

        void AddRecentLogRow(string message, Color rowColor)
        {
            if (logContent == null) return;
            var row = AddText(logContent, message, 15, Color.white, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one);
            row.rectTransform.anchorMin = new Vector2(0f, 1f);
            row.rectTransform.anchorMax = new Vector2(1f, 1f);
            row.rectTransform.pivot = new Vector2(0.5f, 1f);
            row.rectTransform.sizeDelta = new Vector2(0f, 42f);
            row.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.verticalOverflow = VerticalWrapMode.Overflow;
            row.supportRichText = true;
            row.color = rowColor;
            logEntries.Add(row);
        }

        void OpenModal(string title, string subtitle)
        {
            modalOpen = true;
            if (modalRoot != null) modalRoot.SetActive(true);
            if (modalTitleText != null) modalTitleText.text = title;
            if (modalSubtitleText != null) modalSubtitleText.text = subtitle;
        }

        void ClearModalRows()
        {
            if (modalContent == null) return;
            // Destroy is deferred by Unity.  Disable the content while old
            // rows are pending destruction so they cannot participate in the
            // same-frame layout as the newly opened review.
            modalContent.gameObject.SetActive(false);
            for (var i = modalContent.childCount - 1; i >= 0; i--)
            {
                modalContent.GetChild(i).gameObject.SetActive(false);
                Destroy(modalContent.GetChild(i).gameObject);
            }
            modalContent.gameObject.SetActive(true);
        }

        void AddModalRow(string message, Color color, bool emphasize)
        {
            var row = AddText(modalContent, message, emphasize ? 18 : 15, color,
                TextAnchor.MiddleLeft, Vector2.zero, Vector2.one);
            row.rectTransform.anchorMin = new Vector2(0f, 1f);
            row.rectTransform.anchorMax = new Vector2(1f, 1f);
            row.rectTransform.pivot = new Vector2(0.5f, 1f);
            row.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.verticalOverflow = VerticalWrapMode.Overflow;
            row.supportRichText = true;
        }

        void SpawnBubble(Transform anchor, string content, Color color)
        {
            if (anchor == null || string.IsNullOrEmpty(content) || worldBubbleRoot == null) return;
            PruneExpiredBubbles();
            var objectBubble = new GameObject("反馈飘字", typeof(RectTransform), typeof(Image));
            objectBubble.transform.SetParent(worldBubbleRoot, false);
            var rect = objectBubble.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 38f);
            var background = objectBubble.GetComponent<Image>();
            background.color = new Color(0.015f, 0.025f, 0.045f, 0.88f);
            var text = AddText(rect, content, 16, color, TextAnchor.MiddleCenter,
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = true;

            var stack = bubbles.Count(b => b != null && b.anchor == anchor);
            bubbles.Add(new FloatingBubble
            {
                root = objectBubble,
                rect = rect,
                anchor = anchor,
                expiresAt = Time.unscaledTime + BubbleLifetime,
                stack = stack
            });
        }

        void UpdateBubbles()
        {
            PruneExpiredBubbles();
            var camera = CameraForWorld();
            for (var i = bubbles.Count - 1; i >= 0; i--)
            {
                var bubble = bubbles[i];
                if (bubble == null || bubble.root == null || bubble.anchor == null)
                {
                    DestroyBubbleAt(i);
                    continue;
                }
                if (camera == null)
                {
                    bubble.root.SetActive(false);
                    continue;
                }

                var screen = camera.WorldToScreenPoint(bubble.anchor.position + Vector3.up * 0.15f);
                if (screen.z <= 0f || screen.x < 0f || screen.y < 0f ||
                    screen.x > Screen.width || screen.y > Screen.height)
                {
                    // Off-camera feedback is omitted rather than shown as a
                    // misleading edge notification.
                    bubble.root.SetActive(false);
                    continue;
                }

                bubble.root.SetActive(true);
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                    bubble.rect.anchoredPosition = ClampToCanvas(bubble.rect,
                        local + new Vector2(0f, 24f + bubble.stack * 34f));
            }
        }

        void PruneExpiredBubbles()
        {
            var now = Time.unscaledTime;
            for (var i = bubbles.Count - 1; i >= 0; i--)
            {
                var bubble = bubbles[i];
                if (bubble == null || bubble.root == null || bubble.anchor == null ||
                    now >= bubble.expiresAt)
                    DestroyBubbleAt(i);
            }
        }

        void UpdateWorldLabels()
        {
            var camera = CameraForWorld();
            foreach (var pair in worldLabels)
            {
                var label = pair.Value;
                if (label == null || label.root == null || label.anchor == null)
                {
                    if (label != null && label.root != null) label.root.SetActive(false);
                    continue;
                }
                if (runtime == null || pair.Key != runtime.CurrentTarget)
                {
                    label.root.SetActive(false);
                    continue;
                }
                if (camera == null)
                {
                    label.root.SetActive(false);
                    continue;
                }
                var screen = camera.WorldToScreenPoint(label.anchor.position + Vector3.up * 0.1f);
                if (screen.z <= 0f || screen.x < 0f || screen.y < 0f ||
                    screen.x > Screen.width || screen.y > Screen.height)
                {
                    label.root.SetActive(false);
                    continue;
                }
                label.root.SetActive(true);
                Vector2 local;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
                    // Keep the persistent target tag just below the anchor so
                    // short-lived interaction feedback can float above it.
                    label.rect.anchoredPosition = ClampToCanvas(label.rect,
                        local + new Vector2(0f, -34f));
            }
        }

        /// <summary>
        /// Keeps world-space labels and feedback bubbles fully inside the
        /// screen-space canvas.  Their anchors follow 3D targets, so simply
        /// checking the target's screen point is not enough: a label close to
        /// an edge can still extend beyond the frame by half its width.
        /// </summary>
        Vector2 ClampToCanvas(RectTransform rect, Vector2 position)
        {
            if (canvasRect == null || rect == null) return position;

            var canvasBounds = canvasRect.rect;
            var size = rect.rect.size;
            if (size.sqrMagnitude < 0.01f) size = rect.sizeDelta;
            var pivot = rect.pivot;
            var left = size.x * pivot.x + ScreenEdgePadding;
            var right = size.x * (1f - pivot.x) + ScreenEdgePadding;
            var bottom = size.y * pivot.y + ScreenEdgePadding;
            var top = size.y * (1f - pivot.y) + ScreenEdgePadding;

            var minX = canvasBounds.xMin + left;
            var maxX = canvasBounds.xMax - right;
            var minY = canvasBounds.yMin + bottom;
            var maxY = canvasBounds.yMax - top;
            if (minX > maxX) minX = maxX = canvasBounds.center.x;
            if (minY > maxY) minY = maxY = canvasBounds.center.y;

            return new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY));
        }

        void DestroyBubbleAt(int index)
        {
            if (index < 0 || index >= bubbles.Count) return;
            var bubble = bubbles[index];
            if (bubble != null && bubble.root != null) Destroy(bubble.root);
            bubbles.RemoveAt(index);
        }

        string FormatActionMessage(ActionEvent action)
        {
            var message = action.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                message = action.ActorText;
                if (!string.IsNullOrWhiteSpace(action.TargetText))
                    message = string.IsNullOrWhiteSpace(message) ? action.TargetText : message + "；" + action.TargetText;
            }
            if (string.IsNullOrWhiteSpace(message)) message = "记录了一次互动。";
            if (!ContainsActorAttribution(message, action))
            {
                var attribution = ActorAttribution(action);
                if (!string.IsNullOrEmpty(attribution)) message = attribution + "：" + message;
            }
            return ColorizePlayers(message);
        }

        static bool ContainsActorAttribution(string message, ActionEvent action)
        {
            if (string.IsNullOrEmpty(message) || action == null) return false;
            if (action.ActorId >= 1 && action.ActorId <= 3)
                return message.IndexOf(action.ActorId + "号玩家", StringComparison.Ordinal) >= 0;
            if (action.ActorId == 0 && action.ParticipantIds != null)
            {
                var participants = action.ParticipantIds
                    .Where(id => id >= 1 && id <= 3)
                    .Distinct()
                    .ToArray();
                return participants.Length > 0 && participants.All(id =>
                    message.IndexOf(id + "号玩家", StringComparison.Ordinal) >= 0);
            }
            return false;
        }

        static string ActorAttribution(ActionEvent action)
        {
            if (action == null) return string.Empty;
            if (action.ActorId >= 1 && action.ActorId <= 3)
                return action.ActorId + "号玩家";
            if (action.ActorId == 0 && action.ParticipantIds != null)
            {
                var participants = action.ParticipantIds
                    .Where(id => id >= 1 && id <= 3)
                    .Distinct()
                    .Select(id => id + "号玩家")
                    .ToArray();
                if (participants.Length > 0) return "参与者 " + string.Join("、", participants);
            }
            return action.ActorId == 0 ? "公共行为" : string.Empty;
        }

        string ShortFeedback(string preferred, string fallback, string targetId)
        {
            var text = string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
            if (string.IsNullOrWhiteSpace(text)) return "完成";
            text = text.Replace("\n", " ").Trim();
            text = RemoveInternalTargetId(text, targetId);
            if (string.IsNullOrWhiteSpace(text)) return "完成";
            return text.Length > 24 ? text.Substring(0, 24) + "…" : text;
        }

        static string RemoveInternalTargetId(string text, string targetId)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(targetId)) return text;
            // TargetSpec.Id is an internal lookup key. It may be present in a
            // custom event's fallback message, but must never become visible UI.
            return text.Replace(targetId, string.Empty)
                .Trim(' ', '\t', '·', '；', ';', '，', ',', ':', '：', '-', '_', '[', ']');
        }

        string ColorizePlayers(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            for (var actorId = 1; actorId <= 3; actorId++)
            {
                var token = actorId + "号玩家";
                text = text.Replace(token, "<color=#" + ColorUtility.ToHtmlStringRGB(ActorColor(actorId)) + ">" + token + "</color>");
            }
            return text;
        }

        Color LogColor(ActionEvent action)
        {
            if (action.ActorId >= 1 && action.ActorId <= 3) return ActorColor(action.ActorId);
            // Shared output belongs to the public result.  Keep its resource
            // part neutral; ColorizePlayers still colors each participant name.
            return new Color(0.88f, 0.91f, 0.96f, 1f);
        }

        ActorView FindActor(int actorId)
        {
            if (runtime == null || runtime.Actors == null) return null;
            foreach (var actor in runtime.Actors)
                if (actor != null && actor.ActorId == actorId) return actor;
            return null;
        }

        TargetView FindTarget(string targetId)
        {
            if (string.IsNullOrEmpty(targetId) || runtime == null || runtime.Targets == null) return null;
            foreach (var target in runtime.Targets)
                if (target != null && target.Spec != null && target.Spec.Id == targetId) return target;
            return null;
        }

        Transform ActorAnchor(ActorView actor)
        {
            return actor.HeadAnchor != null ? actor.HeadAnchor : actor.transform;
        }

        Transform TargetAnchor(TargetView target)
        {
            return target.FeedbackAnchor != null ? target.FeedbackAnchor : target.transform;
        }

        Camera CameraForWorld()
        {
            if (runtime != null && runtime.CameraRig != null)
            {
                var camera = runtime.CameraRig.GetComponent<Camera>();
                if (camera != null) return camera;
            }
            return Camera.main;
        }

        static Color ActorColor(int actorId)
        {
            return ActorView.ColorForActor(actorId);
        }

        static string FormatTitles(ActorState state)
        {
            if (state == null || state.GrantedTitles == null || state.GrantedTitles.Length == 0)
                return "暂无称号";
            var titles = state.GrantedTitles
                .Distinct()
                .Select(TitleName)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
            return titles.Length == 0 ? "暂无称号" : string.Join("  ·  ", titles);
        }

        static string TitleName(TitleKind title)
        {
            switch (title)
            {
                case TitleKind.Master: return "大师";
                case TitleKind.Artist: return "艺术家";
                case TitleKind.Glutton: return "馋鬼";
                case TitleKind.Philanthropist: return "慈善家";
                default: return title.ToString();
            }
        }

        static string TargetKindName(TargetKind kind)
        {
            switch (kind)
            {
                case TargetKind.HomeFruit: return "家园水果点";
                case TargetKind.ShopEgg: return "商店";
                case TargetKind.FruitPile: return "水果堆";
                case TargetKind.EggPile: return "鸡蛋堆";
                case TargetKind.SlicedFruit: return "果切";
                case TargetKind.CreamPile: return "奶油";
                case TargetKind.CutStation: return "切果工位";
                case TargetKind.WhipStation: return "打发工位";
                case TargetKind.Chopsticks: return "筷子区";
                case TargetKind.CakeFruit: return "蛋糕果切挂点";
                case TargetKind.CakeCream: return "蛋糕奶油区域";
                case TargetKind.Celebration: return "庆典触发器";
                case TargetKind.Trophy: return "奖杯展示位";
                default: return kind.ToString();
            }
        }

        static Font FindChineseFont()
        {
            var candidates = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimSun", "Noto Sans CJK SC", "Arial" };
            foreach (var candidate in candidates)
            {
                var font = Font.CreateDynamicFontFromOSFont(candidate, 20);
                if (font != null) return font;
            }
            var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return legacy != null ? legacy : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        Text AddText(Transform parent, string text, int size, Color color, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var objectText = new GameObject("文字", typeof(RectTransform), typeof(Text));
            objectText.transform.SetParent(parent, false);
            var rect = objectText.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = objectText.GetComponent<Text>();
            label.text = text ?? "";
            label.font = chineseFont;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            return label;
        }

        static Image Image(GameObject parent, string name, Color color)
        {
            var objectImage = new GameObject(name, typeof(RectTransform), typeof(Image));
            objectImage.transform.SetParent(parent.transform, false);
            var image = objectImage.GetComponent<Image>();
            image.color = color;
            image.sprite = WhiteSprite();
            return image;
        }

        static Sprite whiteSprite;

        static Sprite WhiteSprite()
        {
            if (whiteSprite != null) return whiteSprite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "CelebrationDemo UI White";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            whiteSprite.name = "CelebrationDemo UI White Sprite";
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return whiteSprite;
        }

        static Button Button(Transform parent, string name, string labelText, Vector2 anchorMin, Vector2 anchorMax)
        {
            var objectButton = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            objectButton.transform.SetParent(parent, false);
            var rect = objectButton.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = objectButton.GetComponent<Image>();
            image.color = new Color(0.13f, 0.19f, 0.29f, 1f);
            image.sprite = WhiteSprite();
            var button = objectButton.GetComponent<Button>();
            button.targetGraphic = image;
            var textObject = new GameObject("按钮文字", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(objectButton.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            Stretch(textRect);
            var label = textObject.GetComponent<Text>();
            label.text = labelText;
            label.font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei UI", 18);
            label.fontSize = 15;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return button;
        }

        static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = Image(parent.gameObject, name, color);
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void ScrollToLatest(ScrollRect scroll)
        {
            if (scroll == null) return;
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }

        sealed class FloatingBubble
        {
            public GameObject root;
            public RectTransform rect;
            public Transform anchor;
            public float expiresAt;
            public int stack;
        }

        sealed class StationProgressVisual
        {
            public int batchId;
            public bool wasRunning;
            public bool completionAnimating;
            public float completionStartedAt;
            public Color normalFill;
            public Color normalBackground;
        }

        sealed class WorldLabel
        {
            public GameObject root;
            public RectTransform rect;
            public Text title;
            public Transform anchor;
        }
    }
}
