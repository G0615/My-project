using System;
using System.Linq;
using CelebrationDemo;

namespace CelebrationDemo
{
    /// <summary>Framework-free invariants for the Unity-independent celebration rules.</summary>
    public static class CoreSmokeTests
    {
        static int checks;

        public static int RunAll()
        {
            checks = 0;
            ModelsHaveFrozenDefaults();
            EventHistoryIsCommittedBeforeNotification();
            SharedWorkIsUniqueAndKeepsParticipants();
            SharedWorkUsesOneTwoThreePersonDurations();
            EffectsUseIndependentDeadlinesAndRealDeltas();
            CakeIsDirectAndTitlesAreParticipationBased();
            CelebrationAwardsEveryActorOnceAndSupportsTrophyFlow();
            PersonalHistoryIsCompleteAndScoped();
            ResetStartsANewEmptyActivity();
            return checks;
        }

        static void ModelsHaveFrozenDefaults()
        {
            var session = new DemoSession();
            Check(session.Now == 0d, "session starts at time zero");
            Check(session.CutStation.Id == "cut" && session.WhipStation.Id == "whip", "station IDs are frozen");
            Check(session.CutStation.ParticipantIds.Count == 0, "stations start empty");
            Check(session.Cake.FruitStyles.Length == 3 && session.Cake.CreamColors.Length == 3, "cake has three plus three slots");
            Check(session.GetActor(1).GrantedTitles.Length == 0, "actors start without formal titles");
            Check(!session.GetActor(1).HasDonated && !session.GetActor(1).HasEaten, "actors start without participation");
        }

        static void EventHistoryIsCommittedBeforeNotification()
        {
            var session = new DemoSession();
            int callbackCount = 0;
            int observedHistoryCount = 0;
            session.EventRecorded += action =>
            {
                callbackCount++;
                observedHistoryCount = session.History.Count;
                Check(session.History.Any(item => item.EventId == action.EventId), "event is in history before callback");
            };
            session.Execute(1, new TargetSpec("home-fruit", TargetKind.HomeFruit));
            Check(callbackCount == 1 && observedHistoryCount == 1, "one action creates one committed event");
            Check(session.History[0].Message.Contains("水果]+1"), "simple action message contains its delta");
        }

        static void SharedWorkIsUniqueAndKeepsParticipants()
        {
            var session = new DemoSession();
            var cut = new TargetSpec("cut", TargetKind.CutStation);
            session.Execute(1, cut);
            session.Advance(4d);
            session.Execute(2, cut);
            session.Execute(2, cut); // duplicate must be feedback only
            Check(session.CutStation.ParticipantIds.Count == 2, "duplicate work join does not add a participant");
            Check(Math.Abs(session.CutStation.Progress - .4f) < .0001f, "join first advances with old participant count");
            session.Advance(7d);
            Check(!session.CutStation.IsRunning && session.CutStation.Progress == 1f, "shared batch completes at expected time");
            var completion = session.History.Where(item => item.ActionType == "切水果完成" && item.ActorId == 0).ToArray();
            Check(completion.Length == 1, "shared public output is recorded once");
            Check(completion[0].ParticipantIds.SequenceEqual(new[] { 1, 2 }), "completion keeps a copied participant list");
            Check(completion[0].Time == 7d, "completion uses Advance timestamp");
            Check(session.GetActor(1).CutCompletionCount == 1 && session.GetActor(2).CutCompletionCount == 1, "each participant receives one completion count");
            Check(session.GetActor(1).HasCraftParticipation && session.GetActor(2).HasCraftParticipation, "joining immediately grants master qualification");

            // A completed station creates a fresh batch; the old batch cannot be completed again.
            session.Execute(1, cut);
            Check(session.CutStation.BatchId == 2 && session.CutStation.ParticipantIds.SequenceEqual(new[] { 1 }), "next work action starts a new batch");
        }

        static void SharedWorkUsesOneTwoThreePersonDurations()
        {
            var one = new DemoSession();
            var cut = new TargetSpec("cut", TargetKind.CutStation);
            one.Execute(1, cut);
            one.Advance(9.99d);
            Check(one.CutStation.IsRunning, "one-person work is not early");
            one.Advance(10d);
            Check(!one.CutStation.IsRunning, "one-person work takes ten seconds");

            var two = new DemoSession();
            two.Execute(1, cut);
            two.Execute(2, cut);
            two.Advance(4.99d);
            Check(two.CutStation.IsRunning, "two-person work is not early");
            two.Advance(5d);
            Check(!two.CutStation.IsRunning, "two-person work takes five seconds");

            var three = new DemoSession();
            three.Execute(1, cut);
            three.Execute(2, cut);
            three.Execute(3, cut);
            three.Advance(3.32d);
            Check(three.CutStation.IsRunning, "three-person work is not early");
            three.Advance(10d / 3d);
            Check(!three.CutStation.IsRunning, "three-person work takes one third of ten seconds");

            // Workstation clocks are independent and remain active while the same actor joins both.
            var overlap = new DemoSession();
            overlap.Execute(1, new TargetSpec("cut", TargetKind.CutStation));
            overlap.Execute(1, new TargetSpec("whip", TargetKind.WhipStation));
            overlap.Advance(10d);
            Check(!overlap.CutStation.IsRunning && !overlap.WhipStation.IsRunning, "one actor can work both stations");
        }

        static void EffectsUseIndependentDeadlinesAndRealDeltas()
        {
            var config = new DemoConfig { ChopsticksSeconds = 5d, SlowSeconds = 3d, MaxSlowStacks = 10 };
            var session = new DemoSession(config);
            var chopsticks = new TargetSpec("chopsticks", TargetKind.Chopsticks);
            var fruit = new TargetSpec("fruit-pile", TargetKind.FruitPile);
            var sliced = new TargetSpec("sliced-fruit", TargetKind.SlicedFruit);

            Check(session.Resolve(1, fruit).Label.Contains("捐献"), "without chopsticks the pile offers donation");
            Check(!session.Resolve(1, sliced).CanExecute, "without a tool sliced fruit is unavailable");
            session.Execute(1, chopsticks);
            Check(session.HasChopsticks(1) && session.Resolve(1, fruit).Label.Contains("偷吃"), "chopsticks switch pile action automatically");
            var eat = session.Execute(1, fruit);
            Check(eat.Success && session.GetSpeed(1) == 9f && session.GetActor(1).EatCount == 1, "stealing applies one slow layer");
            session.Execute(1, fruit);
            Check(session.GetSpeed(1) == 8f, "second steal applies a second layer");
            var lastEat = session.History.Last(item => item.ActionType == "偷吃");
            Check(lastEat.EffectChange.Contains("9→8"), "normal steal reports actual minus one speed");
            session.Advance(3d);
            Check(session.GetSpeed(1) == 10f && session.GetActor(1).SlowStacks == 0, "shared slow deadline clears all layers");
            Check(session.HasChopsticks(1), "tool deadline is independent of slow deadline");
            session.Advance(5d);
            Check(!session.HasChopsticks(1) && session.Resolve(1, fruit).Label.Contains("捐献"), "tool expiry restores donation automatically");
            Check(session.Resolve(1, sliced).CanExecute == false, "sliced fruit still requires an active tool");

            var donation = session.Execute(1, fruit);
            Check(donation.Success && session.GetActor(1).HasDonated, "expired chopsticks allow donation");
            Check(session.History.Last(item => item.ActionType == "捐献").TargetText.Contains("+1"), "donation target feedback includes public plus one");

            // At the speed floor another steal remains a valid action and reports zero actual change.
            session.Execute(1, chopsticks);
            session.Execute(1, fruit);
            session.Execute(1, fruit);
            var floorEvent = session.History.Last(item => item.ActionType == "偷吃");
            Check(session.GetSpeed(1) == 8f, "test config applies normal slow layers");
            for (int i = 0; i < 9; i++) session.Execute(1, fruit);
            Check(session.GetSpeed(1) == 0f && session.History.Last(item => item.ActionType == "偷吃").EffectChange.Contains("0→0"), "eleven real steals reach the speed floor without failing");

            var refresh = new DemoSession(new DemoConfig { ChopsticksSeconds = 100d, SlowSeconds = 10d });
            refresh.Execute(1, chopsticks);
            refresh.Execute(1, fruit);
            refresh.Advance(6d);
            refresh.Execute(1, fruit);
            refresh.Advance(15d);
            Check(refresh.GetSpeed(1) == 8f, "second steal refreshes one shared slow deadline");
            refresh.Advance(16d);
            Check(refresh.GetSpeed(1) == 10f, "refreshed shared slow deadline clears all layers together");
        }

        static void CakeIsDirectAndTitlesAreParticipationBased()
        {
            var session = new DemoSession();
            var fruit = new TargetSpec("cake-fruit-1", TargetKind.CakeFruit, 0);
            var cream = new TargetSpec("cake-cream-1", TargetKind.CakeCream, 0);
            session.Execute(1, fruit);
            session.Execute(1, cream);
            Check(session.Cake.FruitStyles[0] > 0 && session.Cake.FruitAuthors[0] == 1, "fruit decoration works without an inventory");
            Check(session.Cake.CreamColors[0] > 0 && session.Cake.CreamAuthors[0] == 1, "cream decoration works without an inventory");
            Check(session.GetActor(1).HasArtParticipation, "first decoration grants artist qualification");
            Check(session.History.Count(item => item.NewlyQualifiedTitles.Contains(TitleKind.Artist)) == 1, "artist qualification is granted once");
            session.Execute(1, fruit);
            Check(session.GetActor(1).FruitDecorationCount == 2 && session.History.Count(item => item.NewlyQualifiedTitles.Contains(TitleKind.Artist)) == 1, "repeat decoration changes state without repeating qualification");
        }

        static void CelebrationAwardsEveryActorOnceAndSupportsTrophyFlow()
        {
            var session = new DemoSession();
            var trigger = new TargetSpec("celebration", TargetKind.Celebration);
            var fruit = new TargetSpec("fruit-pile", TargetKind.FruitPile);
            var chopsticks = new TargetSpec("chopsticks", TargetKind.Chopsticks);
            var cut = new TargetSpec("cut", TargetKind.CutStation);
            var cake = new TargetSpec("cake-fruit-1", TargetKind.CakeFruit, 0);
            // Build all four qualifications for actor one, including an in-progress batch, while holding chopsticks.
            session.Execute(1, fruit);
            session.Execute(1, chopsticks);
            session.Execute(1, fruit);
            session.Execute(1, cut);
            session.Execute(1, cake);
            var first = session.Execute(2, trigger);
            Check(first.Success && session.HasCelebrated, "any actor can hold the celebration");
            Check(session.GetActor(1).TrophyStatus == TrophyStatus.Awarded && session.GetActor(2).TrophyStatus == TrophyStatus.Awarded && session.GetActor(3).TrophyStatus == TrophyStatus.Awarded, "all three actors receive trophies");
            Check(session.History.Count(item => item.ActionType == "颁发奖杯" && item.ActorText.Contains("[奖杯]+1")) == 3, "trophy award feedback is one per recipient");
            Check(session.Celebration != null && session.Celebration.Actors[0].GrantedTitles.Contains(TitleKind.Master)
                && session.Celebration.Actors[0].GrantedTitles.Contains(TitleKind.Artist)
                && session.Celebration.Actors[0].GrantedTitles.Contains(TitleKind.Glutton)
                && session.Celebration.Actors[0].GrantedTitles.Contains(TitleKind.Philanthropist), "snapshot captures all four qualifying titles");
            Check(session.Celebration.Actors[1].GrantedTitles.Length == 0 && session.Celebration.Actors[2].GrantedTitles.Length == 0, "unqualified actors keep an empty title snapshot");
            var snapshotTitleCount = session.Celebration.Actors[0].GrantedTitles.Length;
            session.Execute(1, new TargetSpec("cake-cream-1", TargetKind.CakeCream, 0));
            Check(session.Celebration.Actors[0].GrantedTitles.Length == snapshotTitleCount, "later actions cannot rewrite the frozen title snapshot");
            session.Advance(10d);
            Check(session.History.Any(item => item.ActionType == "切水果完成"), "active work continues after awards and appends history");
            int count = session.History.Count;
            var repeated = session.Execute(1, trigger);
            Check(repeated.OpenCelebration && session.History.Count == count, "repeat celebration opens result without new awards");

            var trophy1 = new TargetSpec("trophy-1", TargetKind.Trophy, 0, 1);
            Check(!session.Execute(2, trophy1).Success, "a non-owner cannot use another actor's trophy");
            var place = session.Execute(1, trophy1);
            Check(place.Success && session.GetActor(1).TrophyStatus == TrophyStatus.Placed, "awarded trophy transitions to placed");
            var inspect = session.Execute(1, trophy1);
            Check(inspect.OpenHistory && session.GetActor(1).TrophyStatus == TrophyStatus.Placed, "placed trophy opens history without changing status");
            Check(session.History.Count(item => item.ActionType == "查看奖杯") == 1, "inspect is recorded once per F");
        }

        static void PersonalHistoryIsCompleteAndScoped()
        {
            var session = new DemoSession();
            session.Execute(1, new TargetSpec("cut", TargetKind.CutStation));
            session.Execute(2, new TargetSpec("cut", TargetKind.CutStation));
            session.Advance(10d);
            session.Execute(3, new TargetSpec("home", TargetKind.HomeFruit));
            var actor1 = session.GetHistory(1);
            var actor2 = session.GetHistory(2);
            Check(actor1.Any(item => item.ActionType == "切水果完成" && item.ActorId == 0), "actor one sees shared completion");
            Check(actor2.Any(item => item.ActionType == "切水果完成" && item.ActorId == 0), "actor two sees shared completion");
            Check(actor1.All(item => item.ActorId != 3), "actor one does not see actor three's independent action");
            Check(actor1.SequenceEqual(actor1.OrderBy(item => item.Sequence)), "personal history is sequence ordered");
            var many = new DemoSession();
            for (int i = 0; i < 110; i++) many.Execute(1, new TargetSpec("home", TargetKind.HomeFruit));
            Check(many.History.Count == 110 && many.GetHistory(1).Count == 110, "history is not truncated at one hundred");
        }

        static void ResetStartsANewEmptyActivity()
        {
            var session = new DemoSession();
            var oldActivity = session.ActivityId;
            session.Execute(1, new TargetSpec("cake", TargetKind.CakeFruit, 0));
            session.Reset();
            Check(session.ActivityId != oldActivity && session.Now == 0d, "reset starts a new activity clock");
            Check(session.History.Count == 0 && !session.HasCelebrated, "reset clears history and settlement");
            Check(session.GetActor(1).TrophyStatus == TrophyStatus.Unawarded && session.Cake.FruitStyles[0] == 0, "reset clears trophies and cake");
        }

        static void Check(bool condition, string description)
        {
            checks++;
            if (!condition) throw new Exception("CoreSmokeTests failed: " + description);
        }
    }
}
