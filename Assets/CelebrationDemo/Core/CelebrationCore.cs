using System;
using System.Collections.Generic;
using System.Linq;

namespace CelebrationDemo
{
    /// <summary>Every authored interaction point in the celebration scene.</summary>
    public enum TargetKind
    {
        HomeFruit,
        ShopEgg,
        FruitPile,
        EggPile,
        SlicedFruit,
        CreamPile,
        CutStation,
        WhipStation,
        Chopsticks,
        CakeFruit,
        CakeCream,
        Celebration,
        Trophy,
        // Appended to preserve serialized values in scenes authored before
        // the expanded three-home layout was introduced.
        HomeBanana,
        HomeOrange,
        BananaPile,
        OrangePile
    }

    [Serializable]
    public class TargetSpec
    {
        public string Id;
        public TargetKind Kind;
        public int Index;
        public int OwnerActorId;

        public TargetSpec() { }

        public TargetSpec(string id, TargetKind kind, int index = 0, int ownerActorId = 0)
        {
            Id = id;
            Kind = kind;
            Index = index;
            OwnerActorId = ownerActorId;
        }
    }

    [Serializable]
    public class DemoConfig
    {
        public double WorkSeconds = 10d;
        public float BaseSpeed = 10f;
        public double ChopsticksSeconds = 60d;
        public double SlowSeconds = 10d;
        public int MaxSlowStacks = 10;
        public int ChopsticksCost = 10;
        public int EggCost = 1;

        // These aliases make the configuration pleasant to consume from small adapters.
        public double WorkDurationSeconds { get { return WorkSeconds; } set { WorkSeconds = value; } }
        public double ChopsticksDurationSeconds { get { return ChopsticksSeconds; } set { ChopsticksSeconds = value; } }
        public double SlowDurationSeconds { get { return SlowSeconds; } set { SlowSeconds = value; } }

        public DemoConfig Clone()
        {
            return new DemoConfig
            {
                WorkSeconds = WorkSeconds,
                BaseSpeed = BaseSpeed,
                ChopsticksSeconds = ChopsticksSeconds,
                SlowSeconds = SlowSeconds,
                MaxSlowStacks = MaxSlowStacks,
                ChopsticksCost = ChopsticksCost,
                EggCost = EggCost
            };
        }
    }

    public enum TrophyStatus
    {
        Unawarded,
        Awarded,
        Placed
    }

    public enum TitleKind
    {
        Master,
        Artist,
        Glutton,
        Philanthropist
    }

    [Serializable]
    public sealed class ActorState
    {
        public int ActorId;
        public double ChopsticksExpiresAt;
        public int SlowStacks;
        public double SlowExpiresAt;
        public bool HasCraftParticipation;
        public bool HasArtParticipation;
        public bool HasEaten;
        public bool HasDonated;
        public TrophyStatus TrophyStatus;
        public TitleKind[] GrantedTitles;

        // Behaviour counts are history data, not inventory or balances.
        public int CutCompletionCount;
        public int WhipCompletionCount;
        public int FruitDecorationCount;
        public int CreamDecorationCount;
        public int EatCount;
        public int DonationCount;
        public int TrophyCount;

        public string TrophyActivityId;
        public string HomeSlotId;

        // Readable aliases used by presentation adapters.
        public int CutCompletedCount { get { return CutCompletionCount; } }
        public int WhipCompletedCount { get { return WhipCompletionCount; } }
        public int SlicedFruitCount { get { return FruitDecorationCount; } }
        public int CreamSpreadCount { get { return CreamDecorationCount; } }
        public int EatenCount { get { return EatCount; } }
        public int DonatedCount { get { return DonationCount; } }

        public ActorState(int actorId)
        {
            ActorId = actorId;
            TrophyStatus = TrophyStatus.Unawarded;
            GrantedTitles = Array.Empty<TitleKind>();
        }

        public ActorState Clone()
        {
            var copy = (ActorState)MemberwiseClone();
            copy.GrantedTitles = GrantedTitles == null ? Array.Empty<TitleKind>() : (TitleKind[])GrantedTitles.Clone();
            return copy;
        }
    }

    [Serializable]
    public sealed class StationState
    {
        public string Id;
        public float Progress;
        public bool IsRunning;
        public int BatchId;
        public List<int> ParticipantIds { get; private set; }

        // Kept public for a small world-view adapter; it is not part of the authored UI contract.
        public double LastAdvancedAt;
        public bool HasCompleted;

        public int ParticipantCount { get { return ParticipantIds.Count; } }

        public StationState(string id)
        {
            Id = id;
            ParticipantIds = new List<int>();
        }

        public int[] CopyParticipants()
        {
            return ParticipantIds.ToArray();
        }

        public void Reset()
        {
            Progress = 0f;
            IsRunning = false;
            BatchId = 0;
            LastAdvancedAt = 0d;
            HasCompleted = false;
            ParticipantIds.Clear();
        }
    }

    [Serializable]
    public sealed class CakeState
    {
        public int[] FruitStyles;
        public int[] CreamColors;
        public int[] FruitAuthors;
        public int[] CreamAuthors;

        public CakeState()
        {
            FruitStyles = new int[3];
            CreamColors = new int[3];
            FruitAuthors = new int[3];
            CreamAuthors = new int[3];
        }

        public void Reset()
        {
            Array.Clear(FruitStyles, 0, FruitStyles.Length);
            Array.Clear(CreamColors, 0, CreamColors.Length);
            Array.Clear(FruitAuthors, 0, FruitAuthors.Length);
            Array.Clear(CreamAuthors, 0, CreamAuthors.Length);
        }

        public CakeState Clone()
        {
            return new CakeState
            {
                FruitStyles = (int[])FruitStyles.Clone(),
                CreamColors = (int[])CreamColors.Clone(),
                FruitAuthors = (int[])FruitAuthors.Clone(),
                CreamAuthors = (int[])CreamAuthors.Clone()
            };
        }
    }

    [Serializable]
    public sealed class ActionEvent
    {
        public string ActivityId;
        public long Sequence;
        public string EventId;
        public double Time;
        public int ActorId;
        public int[] ParticipantIds;
        public string TargetId;
        public string Message;
        public string ActorText;
        public string TargetText;
        public TitleKind[] NewlyQualifiedTitles;
        public TitleKind[] GrantedTitles;

        // Optional structured metadata for richer HUDs. The frozen fields above remain the minimum API.
        public string ActionType;
        public string MessageKind;
        public string[] DisplayDeltas;
        public string[] StatChanges;
        public string EffectChange;

        public ActionEvent()
        {
            ParticipantIds = Array.Empty<int>();
            NewlyQualifiedTitles = Array.Empty<TitleKind>();
            GrantedTitles = Array.Empty<TitleKind>();
            DisplayDeltas = Array.Empty<string>();
            StatChanges = Array.Empty<string>();
        }

        public ActionEvent Clone()
        {
            return new ActionEvent
            {
                ActivityId = ActivityId,
                Sequence = Sequence,
                EventId = EventId,
                Time = Time,
                ActorId = ActorId,
                ParticipantIds = ParticipantIds == null ? Array.Empty<int>() : (int[])ParticipantIds.Clone(),
                TargetId = TargetId,
                Message = Message,
                ActorText = ActorText,
                TargetText = TargetText,
                NewlyQualifiedTitles = NewlyQualifiedTitles == null ? Array.Empty<TitleKind>() : (TitleKind[])NewlyQualifiedTitles.Clone(),
                GrantedTitles = GrantedTitles == null ? Array.Empty<TitleKind>() : (TitleKind[])GrantedTitles.Clone(),
                ActionType = ActionType,
                MessageKind = MessageKind,
                DisplayDeltas = DisplayDeltas == null ? Array.Empty<string>() : (string[])DisplayDeltas.Clone(),
                StatChanges = StatChanges == null ? Array.Empty<string>() : (string[])StatChanges.Clone(),
                EffectChange = EffectChange
            };
        }
    }

    [Serializable]
    public sealed class InteractionOffer
    {
        public string Label;
        public bool CanExecute;

        public InteractionOffer() { }

        public InteractionOffer(string label, bool canExecute)
        {
            Label = label;
            CanExecute = canExecute;
        }
    }

    [Serializable]
    public sealed class ActionOutcome
    {
        public string Message;
        public bool OpenHistory;
        public bool OpenCelebration;
        public int ActorId;
        public bool Success;

        public ActionOutcome() { }

        public ActionOutcome(string message, bool openHistory = false, bool openCelebration = false, int actorId = 0, bool success = true)
        {
            Message = message;
            OpenHistory = openHistory;
            OpenCelebration = openCelebration;
            ActorId = actorId;
            Success = success;
        }
    }

    [Serializable]
    public sealed class CelebrationSnapshot
    {
        public string ActivityId;
        public double Time;
        public ActorState[] Actors;
        public CakeState Cake;

        public CelebrationSnapshot Clone()
        {
            return new CelebrationSnapshot
            {
                ActivityId = ActivityId,
                Time = Time,
                Actors = Actors == null ? Array.Empty<ActorState>() : Actors.Select(a => a.Clone()).ToArray(),
                Cake = Cake == null ? new CakeState() : Cake.Clone()
            };
        }
    }

    /// <summary>
    /// Unity-independent authoritative state for one celebration activity.
    /// There are intentionally no inventory or balance fields: deltas live only on events.
    /// </summary>
    public sealed class DemoSession
    {
        const int FirstActorId = 1;
        const int LastActorId = 3;

        readonly Dictionary<int, ActorState> actors = new Dictionary<int, ActorState>();
        readonly List<ActionEvent> history = new List<ActionEvent>();
        long nextSequence;
        int activityNumber;

        public DemoConfig Config { get; private set; }
        public double Now { get; private set; }
        public string ActivityId { get; private set; }
        public bool HasCelebrated { get; private set; }
        public StationState CutStation { get; private set; }
        public StationState WhipStation { get; private set; }
        public CakeState Cake { get; private set; }
        public CelebrationSnapshot Celebration { get; private set; }
        public IReadOnlyList<ActionEvent> History { get { return history; } }

        public event Action<ActionEvent> EventRecorded;

        public DemoSession(DemoConfig config = null)
        {
            Config = (config ?? new DemoConfig()).Clone();
            activityNumber = 1;
            InitializeActivity();
        }

        void InitializeActivity()
        {
            Now = 0d;
            ActivityId = "celebration-" + activityNumber;
            HasCelebrated = false;
            Celebration = null;
            nextSequence = 0L;
            history.Clear();
            actors.Clear();
            for (int id = FirstActorId; id <= LastActorId; id++)
                actors.Add(id, new ActorState(id));
            CutStation = new StationState("cut");
            WhipStation = new StationState("whip");
            Cake = new CakeState();
        }

        public ActorState GetActor(int actorId)
        {
            ActorState state;
            return actors.TryGetValue(actorId, out state) ? state : null;
        }

        public float GetSpeed(int actorId)
        {
            var actor = GetActor(actorId);
            if (actor == null) return 0f;
            ExpireActor(actor);
            if (IsActorProcessing(actorId)) return 0f;
            var value = Config.BaseSpeed - actor.SlowStacks;
            return Math.Max(0f, value);
        }

        /// <summary>Returns true while the actor is an active participant in a running station batch.</summary>
        public bool IsActorProcessing(int actorId)
        {
            return IsStationParticipant(CutStation, actorId) || IsStationParticipant(WhipStation, actorId);
        }

        static bool IsStationParticipant(StationState station, int actorId)
        {
            return station != null && station.IsRunning && station.ParticipantIds != null &&
                station.ParticipantIds.Contains(actorId);
        }

        public bool HasChopsticks(int actorId)
        {
            var actor = GetActor(actorId);
            return actor != null && actor.ChopsticksExpiresAt > Now;
        }

        public InteractionOffer Resolve(int actorId, TargetSpec target)
        {
            if (target == null) return new InteractionOffer("无可用目标", false);
            var actor = GetActor(actorId);
            if (actor == null) return new InteractionOffer("无效角色", false);
            ExpireActor(actor);

            switch (target.Kind)
            {
                case TargetKind.HomeFruit:
                    return new InteractionOffer("领取水果", true);
                case TargetKind.HomeBanana:
                    return new InteractionOffer("领取香蕉", true);
                case TargetKind.HomeOrange:
                    return new InteractionOffer("领取橘子", true);
                case TargetKind.ShopEgg:
                    return new InteractionOffer("购买鸡蛋", true);
                case TargetKind.FruitPile:
                    return new InteractionOffer(HasChopsticks(actorId) ? "偷吃水果" : "捐献水果", true);
                case TargetKind.BananaPile:
                    return new InteractionOffer(HasChopsticks(actorId) ? "偷吃香蕉" : "捐献香蕉", true);
                case TargetKind.OrangePile:
                    return new InteractionOffer(HasChopsticks(actorId) ? "偷吃橘子" : "捐献橘子", true);
                case TargetKind.EggPile:
                    return new InteractionOffer(HasChopsticks(actorId) ? "偷吃鸡蛋" : "捐献鸡蛋", true);
                case TargetKind.SlicedFruit:
                    return HasChopsticks(actorId)
                        ? new InteractionOffer("偷吃果切", true)
                        : new InteractionOffer("请先获得筷子", false);
                case TargetKind.CreamPile:
                    return HasChopsticks(actorId)
                        ? new InteractionOffer("偷吃奶油", true)
                        : new InteractionOffer("请先获得筷子", false);
                case TargetKind.CutStation:
                    return StationOffer("切水果");
                case TargetKind.WhipStation:
                    return StationOffer("打发奶油");
                case TargetKind.Chopsticks:
                    return new InteractionOffer("购买筷子（金币-10）", true);
                case TargetKind.CakeFruit:
                    return ValidCakeIndex(target.Index) ? new InteractionOffer("贴果切", true) : new InteractionOffer("无效果切挂点", false);
                case TargetKind.CakeCream:
                    return ValidCakeIndex(target.Index) ? new InteractionOffer("抹奶油", true) : new InteractionOffer("无效奶油区域", false);
                case TargetKind.Celebration:
                    return new InteractionOffer(HasCelebrated ? "查看庆典结果" : "举办庆典", true);
                case TargetKind.Trophy:
                    return ResolveTrophy(actorId, target);
                default:
                    return new InteractionOffer("无可用动作", false);
            }
        }

        static InteractionOffer StationOffer(string name)
        {
            return new InteractionOffer(name, true);
        }

        InteractionOffer ResolveTrophy(int actorId, TargetSpec target)
        {
            if (target.OwnerActorId != actorId)
                return new InteractionOffer("这是" + target.OwnerActorId + "号玩家的奖杯", false);
            var actor = GetActor(actorId);
            if (actor == null || actor.TrophyStatus == TrophyStatus.Unawarded)
                return new InteractionOffer("请先举办庆典", false);
            if (actor.TrophyStatus == TrophyStatus.Awarded)
                return new InteractionOffer("放置奖杯", true);
            return new InteractionOffer("查看活动回顾", true);
        }

        public ActionOutcome Execute(int actorId, TargetSpec target)
        {
            if (target == null) return Fail(actorId, "无可用目标");
            var actor = GetActor(actorId);
            if (actor == null) return Fail(actorId, "无效角色");
            ExpireActor(actor);

            var offer = Resolve(actorId, target);
            if (!offer.CanExecute) return Fail(actorId, offer.Label);

            switch (target.Kind)
            {
                case TargetKind.HomeFruit:
                    RecordSimple(actorId, target.Id,
                        actorId + "号玩家领取了[水果]×1；" + actorId + "号玩家[水果]+1。", "领取水果", "已领取",
                        new[] { actorId + "号玩家[水果]+1" }, null);
                    return Success(actorId, "水果 +1");
                case TargetKind.HomeBanana:
                    return CollectHomeFruit(actorId, target, "香蕉", "领取香蕉");
                case TargetKind.HomeOrange:
                    return CollectHomeFruit(actorId, target, "橘子", "领取橘子");
                case TargetKind.ShopEgg:
                    RecordSimple(actorId, target.Id,
                        actorId + "号玩家购买了[鸡蛋]×1；" + actorId + "号玩家[金币]-1，" + actorId + "号玩家[鸡蛋]+1。", "购买鸡蛋", "已购买",
                        new[] { actorId + "号玩家[金币]-1", actorId + "号玩家[鸡蛋]+1" }, null);
                    return Success(actorId, "金币 -1，鸡蛋 +1");
                case TargetKind.FruitPile:
                    return HasChopsticks(actorId)
                        ? Eat(actorId, target, "水果")
                        : Donate(actorId, target, "水果");
                case TargetKind.BananaPile:
                    return HasChopsticks(actorId)
                        ? Eat(actorId, target, "香蕉")
                        : Donate(actorId, target, "香蕉");
                case TargetKind.OrangePile:
                    return HasChopsticks(actorId)
                        ? Eat(actorId, target, "橘子")
                        : Donate(actorId, target, "橘子");
                case TargetKind.EggPile:
                    return HasChopsticks(actorId)
                        ? Eat(actorId, target, "鸡蛋")
                        : Donate(actorId, target, "鸡蛋");
                case TargetKind.SlicedFruit:
                    return Eat(actorId, target, "果切");
                case TargetKind.CreamPile:
                    return Eat(actorId, target, "奶油");
                case TargetKind.CutStation:
                    return JoinWork(actorId, CutStation, "切水果");
                case TargetKind.WhipStation:
                    return JoinWork(actorId, WhipStation, "打发奶油");
                case TargetKind.Chopsticks:
                    return BuyChopsticks(actorId, target);
                case TargetKind.CakeFruit:
                    return DecorateFruit(actorId, target);
                case TargetKind.CakeCream:
                    return DecorateCream(actorId, target);
                case TargetKind.Celebration:
                    return Celebrate(actorId, target.Id);
                case TargetKind.Trophy:
                    return ExecuteTrophy(actorId, target);
                default:
                    return Fail(actorId, "无可用动作");
            }
        }

        ActionOutcome CollectHomeFruit(int actorId, TargetSpec target, string item, string actionType)
        {
            RecordSimple(actorId, target.Id,
                actorId + "号玩家领取了[" + item + "]×1；" + actorId + "号玩家[" + item + "]+1。",
                actionType, "已领取",
                new[] { actorId + "号玩家[" + item + "]+1" }, null);
            return Success(actorId, item + " +1");
        }

        public void Advance(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < Now) return;
            if (now == Now)
            {
                ExpireAllActors();
                return;
            }

            // The station owns elapsed time. A participant remains in the batch after leaving or switching.
            // Set the shared clock first so completion events carry the exact Advance timestamp.
            Now = now;
            AdvanceStation(CutStation, now);
            AdvanceStation(WhipStation, now);
            ExpireAllActors();
        }

        void AdvanceStation(StationState station, double now)
        {
            if (!station.IsRunning)
            {
                station.LastAdvancedAt = now;
                return;
            }

            var delta = Math.Max(0d, now - station.LastAdvancedAt);
            var workSeconds = Math.Max(0.000001d, Config.WorkSeconds);
            station.Progress = (float)Math.Min(1d, station.Progress + delta * station.ParticipantIds.Count / workSeconds);
            station.LastAdvancedAt = now;
            if (station.Progress < 1f) return;

            station.Progress = 1f;
            station.IsRunning = false;
            station.HasCompleted = true;
            var participants = station.CopyParticipants();
            var batchId = station.BatchId;
            station.ParticipantIds.Clear();

            bool cutting = station.Id == "cut";
            string action = cutting ? "切水果" : "打发奶油";
            string source = cutting ? "水果" : "鸡蛋";
            string product = cutting ? "果切" : "奶油";
            var names = string.Join("、", participants.Select(id => id + "号玩家").ToArray());
            RecordShared(0, station.Id,
                names + "完成[" + action + "]；广场[" + source + "]-1，[" + product + "]+1。",
                action + "完成", "完成",
                participants,
                new[] { "广场[" + source + "]-1", "广场[" + product + "]+1" },
                null,
                "batch-" + batchId);

            foreach (var participant in participants)
            {
                var actor = GetActor(participant);
                if (actor == null) continue;
                if (cutting) actor.CutCompletionCount++;
                else actor.WhipCompletionCount++;
                RecordSimple(participant, station.Id,
                    participant + "号玩家[" + (cutting ? "切果完成" : "打发完成") + "次数]+1。",
                    action + "完成统计", "完成",
                    new[] { participant + "号玩家[" + (cutting ? "切果完成" : "打发完成") + "次数]+1" },
                    null,
                    participantIds: new[] { participant },
                    effectChange: "batch-" + batchId);
            }
        }

        public ActionOutcome JoinWork(int actorId, string stationId, double now)
        {
            if (now > Now) Advance(now);
            return JoinWork(actorId, stationId == "whip" ? WhipStation : CutStation, stationId == "whip" ? "打发奶油" : "切水果");
        }

        public ActionOutcome AdvanceStation(string stationId, double now)
        {
            Advance(now);
            return Success(0, stationId == "whip" ? WhipStation.Progress.ToString("0.###") : CutStation.Progress.ToString("0.###"));
        }

        public ActionOutcome CompleteBatch(string stationId, int batchId)
        {
            var station = stationId == "whip" ? WhipStation : CutStation;
            if (!station.IsRunning || station.BatchId != batchId) return Fail(0, "批次已完成或不存在");
            station.Progress = 1f;
            AdvanceStation(station, Now);
            return Success(0, "批次完成");
        }

        ActionOutcome JoinWork(int actorId, StationState station, string action)
        {
            var actor = GetActor(actorId);
            if (actor == null) return Fail(actorId, "无效角色");

            // An old batch may complete exactly when a later participant joins.
            if (station.IsRunning && station.LastAdvancedAt < Now)
                AdvanceStation(station, Now);
            if (!station.IsRunning)
            {
                station.BatchId++;
                station.Progress = 0f;
                station.IsRunning = true;
                station.HasCompleted = false;
                station.LastAdvancedAt = Now;
                station.ParticipantIds.Clear();
                station.ParticipantIds.Add(actorId);
                var newlyQualified = Qualify(actor, TitleKind.Master);
                RecordSimple(actorId, station.Id,
                    actorId + "号玩家开始[" + action + "]。",
                    action + "开始", "工位",
                    new[] { actorId + "号玩家加入协作" }, newlyQualified,
                    participantIds: new[] { actorId }, effectChange: "batch-" + station.BatchId);
                return Success(actorId, "开始" + action);
            }

            if (station.ParticipantIds.Contains(actorId))
            {
                RecordSimple(actorId, station.Id,
                    actorId + "号玩家已在本批[" + action + "]协作中。",
                    action + "重复加入", "工位",
                    null, null, participantIds: new[] { actorId }, effectChange: "batch-" + station.BatchId);
                return Success(actorId, "已在本批协作");
            }

            station.ParticipantIds.Add(actorId);
            var newlyJoinedQualification = Qualify(actor, TitleKind.Master);
            RecordSimple(actorId, station.Id,
                actorId + "号玩家加入本批[" + action + "]协作。",
                action + "加入", "工位",
                new[] { actorId + "号玩家加入协作" }, newlyJoinedQualification,
                participantIds: new[] { actorId }, effectChange: "batch-" + station.BatchId);
            return Success(actorId, "已加入协作");
        }

        ActionOutcome BuyChopsticks(int actorId, TargetSpec target)
        {
            var actor = GetActor(actorId);
            actor.ChopsticksExpiresAt = Now + Math.Max(0d, Config.ChopsticksSeconds);
            RecordSimple(actorId, target.Id,
                actorId + "号玩家购买了[筷子]×1；" + actorId + "号玩家[金币]-" + Config.ChopsticksCost + "，" + actorId + "号玩家[筷子]+1。",
                "购买筷子", "筷子区",
                new[] { actorId + "号玩家[金币]-" + Config.ChopsticksCost, actorId + "号玩家[筷子]+1" }, null,
                effectChange: "有效期至" + actor.ChopsticksExpiresAt.ToString("0.###"));
            return Success(actorId, "获得筷子，持续" + Config.ChopsticksSeconds.ToString("0.###") + "秒");
        }

        ActionOutcome Donate(int actorId, TargetSpec target, string item)
        {
            var actor = GetActor(actorId);
            actor.DonationCount++;
            var newlyQualified = Qualify(actor, TitleKind.Philanthropist);
            RecordSimple(actorId, target.Id,
                actorId + "号玩家捐献了[" + item + "]×1；" + actorId + "号玩家[" + item + "]-1，广场[" + item + "]+1。",
                "捐献", "物品堆",
                new[] { actorId + "号玩家[" + item + "]-1", "广场[" + item + "]+1" },
                newlyQualified,
                statChanges: new[] { actorId + "号玩家[捐献次数]+1" });
            return Success(actorId, "捐献" + item);
        }

        ActionOutcome Eat(int actorId, TargetSpec target, string item)
        {
            var actor = GetActor(actorId);
            if (!HasChopsticks(actorId)) return Fail(actorId, "请先获得筷子");

            var before = GetSpeedWithoutExpiry(actor);
            actor.SlowStacks = Math.Min(Math.Max(0, Config.MaxSlowStacks), actor.SlowStacks + 1);
            actor.SlowExpiresAt = Now + Math.Max(0d, Config.SlowSeconds);
            var after = GetSpeedWithoutExpiry(actor);
            actor.EatCount++;
            var newlyQualified = Qualify(actor, TitleKind.Glutton);
            string speedText = before.ToString("0.###") + "→" + after.ToString("0.###");
            var speedDelta = after - before;
            if (actor.SlowStacks >= Config.MaxSlowStacks && before == after)
                speedText += "，已达下限，刷新" + Config.SlowSeconds.ToString("0.###") + "秒";
            else
                speedText += "，持续" + Config.SlowSeconds.ToString("0.###") + "秒";
            RecordSimple(actorId, target.Id,
                actorId + "号玩家偷吃了[" + item + "]×1；广场[" + item + "]-1，" + actorId + "号玩家[移速]" + speedDelta.ToString("+0.###;-0.###;0") + "（" + speedText + "）。",
                "偷吃", "物品",
                new[] { "广场[" + item + "]-1", actorId + "号玩家[偷吃次数]+1" }, newlyQualified,
                effectChange: speedText);
            return Success(actorId, "偷吃" + item + "，移速" + speedText);
        }

        ActionOutcome DecorateFruit(int actorId, TargetSpec target)
        {
            if (!ValidCakeIndex(target.Index)) return Fail(actorId, "无效果切挂点");
            var actor = GetActor(actorId);
            Cake.FruitStyles[target.Index] = Cake.FruitStyles[target.Index] % 3 + 1;
            Cake.FruitAuthors[target.Index] = actorId;
            actor.FruitDecorationCount++;
            var newlyQualified = Qualify(actor, TitleKind.Artist);
            RecordSimple(actorId, target.Id,
                actorId + "号玩家给[蛋糕挂点" + (target.Index + 1) + "]贴了果切；广场[果切]-1，" + actorId + "号玩家[贴果切次数]+1。",
                "贴果切", "蛋糕挂点" + (target.Index + 1),
                new[] { "广场[果切]-1", actorId + "号玩家[贴果切次数]+1" }, newlyQualified);
            return Success(actorId, "贴果切");
        }

        ActionOutcome DecorateCream(int actorId, TargetSpec target)
        {
            if (!ValidCakeIndex(target.Index)) return Fail(actorId, "无效奶油区域");
            var actor = GetActor(actorId);
            Cake.CreamColors[target.Index] = Cake.CreamColors[target.Index] % 4 + 1;
            Cake.CreamAuthors[target.Index] = actorId;
            actor.CreamDecorationCount++;
            var newlyQualified = Qualify(actor, TitleKind.Artist);
            RecordSimple(actorId, target.Id,
                actorId + "号玩家给[蛋糕区域" + (target.Index + 1) + "]抹了奶油；广场[奶油]-1，" + actorId + "号玩家[抹奶油次数]+1。",
                "抹奶油", "蛋糕区域" + (target.Index + 1),
                new[] { "广场[奶油]-1", actorId + "号玩家[抹奶油次数]+1" }, newlyQualified);
            return Success(actorId, "抹奶油");
        }

        ActionOutcome Celebrate(int actorId, string targetId)
        {
            if (HasCelebrated)
                return Success(actorId, "庆典已举办，可查看结算结果", false, true);

            // Mark before recording the first event so event listeners cannot re-enter and award twice.
            HasCelebrated = true;
            // Resolve every recipient and trophy state before any event is published. This makes the
            // celebration snapshot authoritative even if a listener immediately queries it.
            foreach (var id in new[] { 1, 2, 3 })
            {
                var recipient = GetActor(id);
                recipient.TrophyStatus = TrophyStatus.Awarded;
                recipient.TrophyCount = 1;
                recipient.TrophyActivityId = ActivityId;
                recipient.HomeSlotId = "trophy-" + id;
                recipient.GrantedTitles = TitlesFor(recipient);
            }
            Celebration = new CelebrationSnapshot
            {
                ActivityId = ActivityId,
                Time = Now,
                Actors = actors.Values.OrderBy(a => a.ActorId).Select(a => a.Clone()).ToArray(),
                Cake = Cake.Clone()
            };

            RecordSimple(actorId, targetId,
                actorId + "号玩家举办了庆典。三人称号已按本轮参与资格结算。",
                "举办庆典", "庆典", null, null,
                participantIds: new[] { 1, 2, 3 }, effectChange: "庆典快照已保存");

            foreach (var id in new[] { 1, 2, 3 })
            {
                var recipient = GetActor(id);
                RecordSimple(id, "trophy-" + id,
                    id + "号玩家获得了庆典[奖杯]×1；" + id + "号玩家[奖杯]+1。",
                    "颁发奖杯", "奖杯",
                    new[] { id + "号玩家[奖杯]+1" }, null,
                    new[] { id }, null,
                    null, recipient.GrantedTitles);
            }
            return Success(actorId, "庆典已举办，三人各获得一座奖杯", false, true);
        }

        ActionOutcome ExecuteTrophy(int actorId, TargetSpec target)
        {
            var actor = GetActor(actorId);
            if (target.OwnerActorId != actorId) return Fail(actorId, "这是" + target.OwnerActorId + "号玩家的奖杯");
            if (actor.TrophyStatus == TrophyStatus.Unawarded) return Fail(actorId, "请先举办庆典");
            if (actor.TrophyStatus == TrophyStatus.Awarded)
            {
                actor.TrophyStatus = TrophyStatus.Placed;
                RecordSimple(actorId, target.Id,
                    actorId + "号玩家在家园放置了[奖杯]×1；" + actorId + "号玩家[奖杯]-1，家园[奖杯]+1。",
                    "放置奖杯", "家园奖杯位",
                    new[] { actorId + "号玩家[奖杯]-1", "家园[奖杯]+1" }, null);
                return Success(actorId, "奖杯已放置");
            }

            RecordSimple(actorId, target.Id,
                actorId + "号玩家查看了本次活动回顾。",
                "查看奖杯", "奖杯回顾", null, null);
            return Success(actorId, "打开活动回顾", true, false);
        }

        public ActionOutcome Celebrate(int actorId, string targetId, double now)
        {
            if (now > Now) Advance(now);
            return Celebrate(actorId, targetId);
        }

        public ActionOutcome PlaceTrophy(int actorId, string homeSlotId)
        {
            var target = new TargetSpec(homeSlotId, TargetKind.Trophy, 0, actorId);
            var state = GetActor(actorId);
            if (state == null || state.HomeSlotId != homeSlotId)
                return Fail(actorId, "不是本人的奖杯位置");
            return ExecuteTrophy(actorId, target);
        }

        public IReadOnlyList<ActionEvent> GetHistory(int actorId)
        {
            if (!actors.ContainsKey(actorId)) return Array.Empty<ActionEvent>();
            var result = new List<ActionEvent>();
            var seen = new HashSet<string>();
            foreach (var item in history.OrderBy(e => e.Sequence))
            {
                if (item == null || item.ActivityId != ActivityId || !seen.Add(item.EventId)) continue;
                if (item.ActorId == actorId || (item.ParticipantIds != null && item.ParticipantIds.Contains(actorId)))
                    result.Add(item);
            }
            return result;
        }

        public IReadOnlyList<ActionEvent> GetActorHistory(int actorId)
        {
            return GetHistory(actorId);
        }

        public IReadOnlyList<ActionEvent> OpenTrophyHistory(int actorId, string trophyId)
        {
            var actor = GetActor(actorId);
            if (actor == null || actor.TrophyStatus != TrophyStatus.Placed || actor.HomeSlotId != trophyId)
                return Array.Empty<ActionEvent>();
            ExecuteTrophy(actorId, new TargetSpec(trophyId, TargetKind.Trophy, 0, actorId));
            return GetHistory(actorId);
        }

        public void Reset()
        {
            activityNumber++;
            InitializeActivity();
        }

        void ExpireAllActors()
        {
            foreach (var actor in actors.Values.OrderBy(a => a.ActorId).ToArray()) ExpireActor(actor);
        }

        void ExpireActor(ActorState actor)
        {
            if (actor == null) return;
            if (actor.ChopsticksExpiresAt > 0d && Now >= actor.ChopsticksExpiresAt)
            {
                actor.ChopsticksExpiresAt = 0d;
                RecordSimple(actor.ActorId, "chopsticks", actor.ActorId + "号玩家的筷子已到期，捐献动作恢复。",
                    "筷子到期", "筷子区", null, null);
            }
            if (actor.SlowStacks > 0 && actor.SlowExpiresAt > 0d && Now >= actor.SlowExpiresAt)
            {
                var old = GetSpeedWithoutExpiry(actor);
                actor.SlowStacks = 0;
                actor.SlowExpiresAt = 0d;
                var restored = GetSpeedWithoutExpiry(actor);
                RecordSimple(actor.ActorId, "slow", actor.ActorId + "号玩家减速效果到期，移速" + old.ToString("0.###") + "→" + restored.ToString("0.###") + "。",
                    "减速到期", "角色效果", null, null, effectChange: old.ToString("0.###") + "→" + restored.ToString("0.###"));
            }
        }

        float GetSpeedWithoutExpiry(ActorState actor)
        {
            return Math.Max(0f, Config.BaseSpeed - actor.SlowStacks);
        }

        bool ValidCakeIndex(int index)
        {
            return index >= 0 && index < 3;
        }

        TitleKind[] Qualify(ActorState actor, TitleKind title)
        {
            bool had;
            switch (title)
            {
                case TitleKind.Master: had = actor.HasCraftParticipation; actor.HasCraftParticipation = true; break;
                case TitleKind.Artist: had = actor.HasArtParticipation; actor.HasArtParticipation = true; break;
                case TitleKind.Glutton: had = actor.HasEaten; actor.HasEaten = true; break;
                case TitleKind.Philanthropist: had = actor.HasDonated; actor.HasDonated = true; break;
                default: had = true; break;
            }
            return had ? Array.Empty<TitleKind>() : new[] { title };
        }

        TitleKind[] TitlesFor(ActorState actor)
        {
            var result = new List<TitleKind>();
            if (actor.HasCraftParticipation) result.Add(TitleKind.Master);
            if (actor.HasArtParticipation) result.Add(TitleKind.Artist);
            if (actor.HasEaten) result.Add(TitleKind.Glutton);
            if (actor.HasDonated) result.Add(TitleKind.Philanthropist);
            return result.ToArray();
        }

        ActionOutcome BuyOrFail(int actorId, string message, bool success)
        {
            return success ? Success(actorId, message) : Fail(actorId, message);
        }

        ActionOutcome Success(int actorId, string message, bool openHistory = false, bool openCelebration = false)
        {
            return new ActionOutcome(message, openHistory, openCelebration, actorId, true);
        }

        ActionOutcome Fail(int actorId, string message)
        {
            return new ActionOutcome(message, false, false, actorId, false);
        }

        void RecordShared(int actorId, string targetId, string message, string actionType, string messageKind,
            int[] participantIds, string[] deltas, string[] statChanges, string effectChange)
        {
            Record(actorId, targetId, message, actionType, messageKind, participantIds, deltas, statChanges,
                Array.Empty<TitleKind>(), Array.Empty<TitleKind>(), effectChange);
        }

        void RecordSimple(int actorId, string targetId, string message, string actionType, string messageKind,
            string[] deltas, TitleKind[] newlyQualified, int[] participantIds = null, string effectChange = null,
            string[] statChanges = null, TitleKind[] grantedTitles = null)
        {
            Record(actorId, targetId, message, actionType, messageKind,
                participantIds ?? Array.Empty<int>(), deltas ?? Array.Empty<string>(), statChanges ?? Array.Empty<string>(),
                newlyQualified ?? Array.Empty<TitleKind>(), grantedTitles ?? Array.Empty<TitleKind>(), effectChange);
        }

        void Record(int actorId, string targetId, string message, string actionType, string messageKind,
            int[] participantIds, string[] deltas, string[] statChanges, TitleKind[] newlyQualified,
            TitleKind[] grantedTitles, string effectChange)
        {
            var sequence = ++nextSequence;
            var safeParticipants = participantIds == null ? Array.Empty<int>() : (int[])participantIds.Clone();
            var safeDeltas = deltas == null ? Array.Empty<string>() : (string[])deltas.Clone();
            var item = new ActionEvent
            {
                ActivityId = ActivityId,
                Sequence = sequence,
                EventId = ActivityId + "-" + sequence,
                Time = Now,
                ActorId = actorId,
                ParticipantIds = safeParticipants,
                TargetId = targetId ?? string.Empty,
                Message = message ?? string.Empty,
                ActorText = ComposeActorText(actorId, safeParticipants, actionType, safeDeltas),
                TargetText = ComposeTargetText(targetId, actionType, safeDeltas),
                NewlyQualifiedTitles = newlyQualified == null ? Array.Empty<TitleKind>() : (TitleKind[])newlyQualified.Clone(),
                GrantedTitles = grantedTitles == null ? Array.Empty<TitleKind>() : (TitleKind[])grantedTitles.Clone(),
                ActionType = actionType ?? string.Empty,
                MessageKind = messageKind ?? string.Empty,
                DisplayDeltas = safeDeltas,
                StatChanges = statChanges == null ? Array.Empty<string>() : (string[])statChanges.Clone(),
                EffectChange = effectChange ?? string.Empty
            };
            // The history write deliberately precedes the callback. Presentation is never authoritative.
            history.Add(item);
            EventRecorded?.Invoke(item);
        }

        static string ComposeActorText(int actorId, int[] participants, string actionType, string[] deltas)
        {
            if (actorId == 0)
            {
                return "协作完成";
            }

            var name = actorId + "号玩家";
            var own = (deltas ?? Array.Empty<string>())
                .Where(delta => !string.IsNullOrEmpty(delta) && (delta.Contains(name) || delta.Contains("[奖杯]+1")))
                // Keep the leading '[' from resource tokens such as
                // "[水果]+1". Trimming it here made personal head feedback
                // render as "水果]+1", even though the event log was correct.
                .Select(delta => delta.Replace(name, string.Empty).Trim(' ', '：', ':', '；', ';', '，', ','))
                .Where(delta => !string.IsNullOrEmpty(delta))
                .ToArray();
            return own.Length == 0 ? name : string.Join("，", own);
        }

        static string ComposeTargetText(string targetId, string actionType, string[] deltas)
        {
            var target = targetId ?? string.Empty;
            var publicChanges = (deltas ?? Array.Empty<string>())
                .Where(delta => !string.IsNullOrEmpty(delta) &&
                    (delta.StartsWith("广场", StringComparison.Ordinal) || delta.StartsWith("家园", StringComparison.Ordinal)))
                .ToArray();
            if (publicChanges.Length > 0)
                return string.Join("，", publicChanges);
            if (string.Equals(actionType, "颁发奖杯", StringComparison.Ordinal))
                return "奖杯已颁发";
            if (string.Equals(actionType, "领取水果", StringComparison.Ordinal)) return "已领取水果";
            if (string.Equals(actionType, "购买鸡蛋", StringComparison.Ordinal)) return "已购买鸡蛋";
            if (string.Equals(actionType, "购买筷子", StringComparison.Ordinal)) return "已获得筷子";
            if (string.Equals(actionType, "查看奖杯", StringComparison.Ordinal)) return "打开活动回顾";
            return actionType ?? string.Empty;
        }
    }
}
