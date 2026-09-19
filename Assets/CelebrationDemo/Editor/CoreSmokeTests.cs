using System;
using System.Collections.Generic;
using System.IO;
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
            SingleInteractionCarriesIdentityAndSequence();
            HomeFruitInteractionKeepsActorOwnershipAndCountsActions();
            ShopEggInteractionKeepsActorOwnershipAndCountsPurchases();
            FruitPileDonationKeepsActorOwnershipWithoutInventoryPrerequisite();
            EventHistoryIsCommittedBeforeNotification();
            SharedWorkIsUniqueAndKeepsParticipants();
            SharedWorkUsesOneTwoThreePersonDurations();
            EffectsUseIndependentDeadlinesAndRealDeltas();
            CakeIsDirectAndTitlesAreParticipationBased();
            CelebrationAwardsEveryActorOnceAndSupportsTrophyFlow();
            PersonalHistoryIsCompleteAndScoped();
            RecentLogReadsDoNotRecordAgain();
            FeedbackFieldsStayHumanReadableAndHudUsesTheirLifecycle();
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

        static void SingleInteractionCarriesIdentityAndSequence()
        {
            var session = new DemoSession();
            var target = new TargetSpec("home-fruit", TargetKind.HomeFruit);
            ActionEvent notified = null;
            session.EventRecorded += action => notified = action;

            var outcome = session.Execute(1, target);
            Check(outcome.Success, "valid interaction succeeds");
            Check(session.History.Count == 1, "one valid interaction appends one event");
            Check(notified != null && ReferenceEquals(session.History[0], notified), "the notified event is the committed event");
            Check(notified.ActivityId == session.ActivityId, "event keeps the current activity");
            Check(notified.ActorId == 1 && notified.TargetId == target.Id, "event identifies actor and target");
            Check(notified.Sequence == 1L && notified.EventId == session.ActivityId + "-1", "first event has sequence and event ID");

            session.Execute(1, new TargetSpec("shop-egg", TargetKind.ShopEgg));
            Check(session.History.Count == 2 && session.History[1].Sequence == 2L, "sequence advances for the next interaction");
        }

        static void HomeFruitInteractionKeepsActorOwnershipAndCountsActions()
        {
            var session = new DemoSession();
            var target = new TargetSpec("home-fruit", TargetKind.HomeFruit);

            // The same home point is available to each currently selected actor.
            for (var actorId = 1; actorId <= 3; actorId++)
            {
                var offer = session.Resolve(actorId, target);
                Check(offer.CanExecute && offer.Label == "领取水果",
                    "home fruit is executable for actor " + actorId);

                var outcome = session.Execute(actorId, target);
                Check(outcome.Success && outcome.ActorId == actorId && outcome.Message == "水果 +1",
                    "home fruit returns the fruit plus one result for actor " + actorId);
            }

            // Repeated F is a repeated action. It appends events and never turns
            // the event-only delta into a persistent inventory or a balance change.
            session.Execute(2, target);
            session.Execute(2, target);
            Check(session.History.Count == 5, "three first claims plus two repeated claims are five events");
            Check(session.GetHistory(1).Count == 1 && session.GetHistory(1)[0].ActorId == 1,
                "actor one history keeps only actor one home claims");
            Check(session.GetHistory(2).Count == 3 && session.GetHistory(2).All(item => item.ActorId == 2),
                "actor two history counts repeated home claims without cross attribution");
            Check(session.GetHistory(3).Count == 1 && session.GetHistory(3)[0].ActorId == 3,
                "actor three history keeps only actor three home claims");

            for (var index = 0; index < session.History.Count; index++)
            {
                var action = session.History[index];
                var actorId = index < 3 ? index + 1 : 2;
                Check(action.ActionType == "领取水果" && action.MessageKind == "已领取" &&
                    action.ActorId == actorId && action.TargetId == target.Id,
                    "home fruit event keeps action, actor, and target identity " + (index + 1));
                Check(action.ActorText == "[水果]+1" && action.TargetText == "已领取水果" &&
                    action.Message.Contains("领取了[水果]×1") && action.Message.Contains("[水果]+1"),
                    "home fruit event exposes human-readable actor and target feedback " + (index + 1));
                Check(action.DisplayDeltas.Length == 1 && action.DisplayDeltas[0] == actorId + "号玩家[水果]+1" &&
                    action.StatChanges.Length == 0 && !action.Message.Contains("金币"),
                    "home fruit event records only the selected actor fruit delta " + (index + 1));
                Check(action.Sequence == index + 1L && action.EventId == session.ActivityId + "-" + (index + 1L),
                    "home fruit repeated actions keep monotonic event identity " + (index + 1));
            }

            Check(!session.GetActor(1).HasDonated && !session.GetActor(2).HasDonated && !session.GetActor(3).HasDonated &&
                !session.GetActor(1).HasEaten && !session.GetActor(2).HasEaten && !session.GetActor(3).HasEaten,
                "home fruit claims do not create donation or stealing participation");
        }

        static void ShopEggInteractionKeepsActorOwnershipAndCountsPurchases()
        {
            var session = new DemoSession();
            var target = new TargetSpec("shop-egg", TargetKind.ShopEgg);

            // The same shop point is available to each currently selected actor.
            for (var actorId = 1; actorId <= 3; actorId++)
            {
                var offer = session.Resolve(actorId, target);
                Check(offer.CanExecute && offer.Label == "购买鸡蛋",
                    "shop egg is executable for actor " + actorId);

                var outcome = session.Execute(actorId, target);
                Check(outcome.Success && outcome.ActorId == actorId && outcome.Message == "金币 -1，鸡蛋 +1",
                    "shop egg returns the coin minus one and egg plus one result for actor " + actorId);
            }

            // Repeated F is a repeated purchase action. It appends events and keeps
            // the displayed deltas out of persistent balance or inventory state.
            session.Execute(2, target);
            session.Execute(2, target);
            Check(session.History.Count == 5, "three first purchases plus two repeated purchases are five events");
            Check(session.GetHistory(1).Count == 1 && session.GetHistory(1)[0].ActorId == 1,
                "actor one history keeps only actor one egg purchases");
            Check(session.GetHistory(2).Count == 3 && session.GetHistory(2).All(item => item.ActorId == 2),
                "actor two history counts repeated egg purchases without cross attribution");
            Check(session.GetHistory(3).Count == 1 && session.GetHistory(3)[0].ActorId == 3,
                "actor three history keeps only actor three egg purchases");

            for (var index = 0; index < session.History.Count; index++)
            {
                var action = session.History[index];
                var actorId = index < 3 ? index + 1 : 2;
                Check(action.ActionType == "购买鸡蛋" && action.MessageKind == "已购买" &&
                    action.ActorId == actorId && action.TargetId == target.Id,
                    "shop egg event keeps action, actor, and target identity " + (index + 1));
                Check(action.ActorText == "[金币]-1，[鸡蛋]+1" && action.TargetText == "已购买鸡蛋" &&
                    action.Message.Contains("购买了[鸡蛋]×1") && action.Message.Contains("[金币]-1") &&
                    action.Message.Contains("[鸡蛋]+1"),
                    "shop egg event exposes human-readable purchase feedback " + (index + 1));
                Check(action.DisplayDeltas.Length == 2 &&
                    action.DisplayDeltas[0] == actorId + "号玩家[金币]-1" &&
                    action.DisplayDeltas[1] == actorId + "号玩家[鸡蛋]+1" &&
                    action.StatChanges.Length == 0 && action.EffectChange == string.Empty,
                    "shop egg event records only the selected actor display deltas " + (index + 1));
                Check(action.NewlyQualifiedTitles.Length == 0 && action.GrantedTitles.Length == 0 &&
                    action.Sequence == index + 1L && action.EventId == session.ActivityId + "-" + (index + 1L),
                    "shop egg repeated actions keep monotonic event identity without title changes " + (index + 1));
            }

            Check(!session.GetActor(1).HasDonated && !session.GetActor(2).HasDonated && !session.GetActor(3).HasDonated &&
                !session.GetActor(1).HasEaten && !session.GetActor(2).HasEaten && !session.GetActor(3).HasEaten,
                "shop egg purchases do not create donation or stealing participation");
        }

        static void FruitPileDonationKeepsActorOwnershipWithoutInventoryPrerequisite()
        {
            var session = new DemoSession();
            var target = new TargetSpec("fruit-pile", TargetKind.FruitPile);

            // B06 deliberately starts from a fresh session: donation does not
            // require the actor to claim fruit first and no chopsticks are held.
            for (var actorId = 1; actorId <= 3; actorId++)
            {
                Check(!session.HasChopsticks(actorId) && !session.GetActor(actorId).HasDonated &&
                    !session.GetActor(actorId).HasEaten,
                    "fresh actor " + actorId + " can donate without a prerequisite or stealing state");
                var offer = session.Resolve(actorId, target);
                Check(offer.CanExecute && offer.Label == "捐献水果",
                    "fruit pile offers donation to actor " + actorId + " without chopsticks");

                var outcome = session.Execute(actorId, target);
                Check(outcome.Success && outcome.ActorId == actorId && outcome.Message == "捐献水果",
                    "fruit donation returns the fruit donation result for actor " + actorId);
            }

            // Repeated F is another donation event for the active actor. It
            // never becomes a persistent inventory total.
            session.Execute(2, target);
            session.Execute(2, target);
            var expectedActors = new[] { 1, 2, 3, 2, 2 };
            Check(session.History.Count == expectedActors.Length,
                "three first donations plus two repeated donations are five events");
            Check(session.GetHistory(1).Count == 1 && session.GetHistory(1).All(item => item.ActorId == 1),
                "actor one history keeps only actor one donations");
            Check(session.GetHistory(2).Count == 3 && session.GetHistory(2).All(item => item.ActorId == 2),
                "actor two history counts repeated donations without cross attribution");
            Check(session.GetHistory(3).Count == 1 && session.GetHistory(3).All(item => item.ActorId == 3),
                "actor three history keeps only actor three donations");

            for (var index = 0; index < session.History.Count; index++)
            {
                var action = session.History[index];
                var actorId = expectedActors[index];
                Check(action.ActionType == "捐献" && action.MessageKind == "物品堆" &&
                    action.ActorId == actorId && action.TargetId == target.Id,
                    "fruit donation event keeps action, actor, and target identity " + (index + 1));
                Check(action.ActorText == "[水果]-1" && action.TargetText == "广场[水果]+1" &&
                    action.Message.Contains("捐献了[水果]×1") && action.Message.Contains("[水果]-1") &&
                    action.Message.Contains("广场[水果]+1"),
                    "fruit donation exposes exact actor and target feedback " + (index + 1));
                Check(action.DisplayDeltas.Length == 2 &&
                    action.DisplayDeltas[0] == actorId + "号玩家[水果]-1" &&
                    action.DisplayDeltas[1] == "广场[水果]+1" &&
                    action.StatChanges.Length == 1 &&
                    action.StatChanges[0] == actorId + "号玩家[捐献次数]+1",
                    "fruit donation separates resource feedback from the donation statistic " + (index + 1));
                Check(action.Sequence == index + 1L && action.EventId == session.ActivityId + "-" + (index + 1L),
                    "fruit donation repeated actions keep monotonic event identity " + (index + 1));
            }

            Check(session.GetActor(1).DonationCount == 1 && session.GetActor(2).DonationCount == 3 &&
                session.GetActor(3).DonationCount == 1,
                "repeated donations count actions without inventing an inventory total");
            Check(session.GetActor(1).HasDonated && session.GetActor(2).HasDonated && session.GetActor(3).HasDonated &&
                !session.GetActor(1).HasEaten && !session.GetActor(2).HasEaten && !session.GetActor(3).HasEaten &&
                session.GetActor(1).EatCount == 0 && session.GetActor(2).EatCount == 0 && session.GetActor(3).EatCount == 0,
                "fruit donations grant donation participation without creating stealing state");
            Check(session.History.Count(item => item.NewlyQualifiedTitles.Contains(TitleKind.Philanthropist)) == 3 &&
                session.History.All(item => item.ActionType != "偷吃"),
                "each actor qualifies once and donation history contains no stealing event");
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

        static void RecentLogReadsDoNotRecordAgain()
        {
            var session = new DemoSession();
            for (var i = 0; i < 105; i++)
                session.Execute(1, new TargetSpec("home-" + i, TargetKind.HomeFruit));

            var beforeRefreshes = session.History.Count;
            var firstRefresh = ReadRecentWindow(session.History, 100);
            var secondRefresh = ReadRecentWindow(session.History, 100);

            Check(firstRefresh.Count == 100 && firstRefresh[0].Sequence == 6,
                "recent log reads keep only the latest one hundred events");
            Check(secondRefresh.Count == 100 && secondRefresh[0].EventId == firstRefresh[0].EventId,
                "repeated recent log refresh reads the same event window");
            Check(session.History.Count == beforeRefreshes,
                "recent log refresh does not record another event");
        }

        static void FeedbackFieldsStayHumanReadableAndHudUsesTheirLifecycle()
        {
            var session = new DemoSession();
            var target = new TargetSpec("internal-home-fruit-key", TargetKind.HomeFruit);
            ActionEvent action = null;
            session.EventRecorded += item => action = item;
            session.Execute(1, target);

            Check(action != null && !string.IsNullOrWhiteSpace(action.ActorText),
                "actor feedback is supplied by the committed ActionEvent");
            Check(action != null && !string.IsNullOrWhiteSpace(action.TargetText),
                "target feedback is supplied by the committed ActionEvent");
            Check(action != null && !action.ActorText.Contains(target.Id) && !action.TargetText.Contains(target.Id),
                "core feedback fields do not expose the internal target ID");

            var hudSource = FindProjectFile(Path.Combine("Assets", "CelebrationDemo", "UI", "DemoHud.cs"));
            Check(!string.IsNullOrEmpty(hudSource), "DemoHud source is available for the feedback contract check");
            Check(hudSource.Contains("ShortFeedback(action.ActorText") &&
                hudSource.Contains("ShortFeedback(action.TargetText"),
                "DemoHud reads actor and target feedback from ActionEvent fields");
            Check(hudSource.Contains("ActorColor(action.ActorId)") &&
                hudSource.Contains("ActorColor(actorId)") &&
                hudSource.Contains("TargetText, action.Message, action.TargetId), MutedText"),
                "DemoHud keeps actor colors and neutral target feedback distinct");
            Check(hudSource.Contains("PruneExpiredBubbles") &&
                hudSource.Contains("now >= bubble.expiresAt") &&
                hudSource.Contains("DestroyBubbleAt(i)"),
                "DemoHud removes expired feedback bubbles");
            Check(hudSource.Contains("RemoveInternalTargetId") &&
                !hudSource.Contains("ShortFeedback(action.TargetId"),
                "DemoHud filters target IDs instead of displaying them as feedback");
        }

        static string FindProjectFile(string relativePath)
        {
            var starts = new[]
            {
                new DirectoryInfo(Directory.GetCurrentDirectory()),
                new DirectoryInfo(AppContext.BaseDirectory)
            };
            foreach (var start in starts)
            {
                var directory = start;
                for (var depth = 0; directory != null && depth < 10; depth++, directory = directory.Parent)
                {
                    var candidate = Path.Combine(directory.FullName, relativePath);
                    if (File.Exists(candidate)) return File.ReadAllText(candidate);
                }
            }
            return string.Empty;
        }

        static IReadOnlyList<ActionEvent> ReadRecentWindow(IReadOnlyList<ActionEvent> history, int limit)
        {
            if (history == null || limit <= 0) return Array.Empty<ActionEvent>();
            var first = Math.Max(0, history.Count - limit);
            var result = new List<ActionEvent>();
            for (var i = first; i < history.Count; i++)
                if (history[i] != null) result.Add(history[i]);
            return result;
        }

        static void Check(bool condition, string description)
        {
            checks++;
            if (!condition) throw new Exception("CoreSmokeTests failed: " + description);
        }
    }
}
