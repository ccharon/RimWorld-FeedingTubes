using System.Collections.Generic;
using Verse.AI;

namespace FeedingTube
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedType.Global
    internal class JobDriver_FillTube : JobDriver
    {
        private const TargetIndex TubeToFill = TargetIndex.A;
        private const TargetIndex FoodToLoad = TargetIndex.B;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(FoodToLoad), job, 1, job.GetTarget(FoodToLoad).Thing.stackCount) &&
                   pawn.Reserve(job.GetTarget(TubeToFill), job);
        }

        /// <summary>
        ///     creates a list of all the things that need to be done "toils"
        /// </summary>
        /// <returns>the toils to do</returns>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TubeToFill);
            this.FailOnForbidden(FoodToLoad);

            // first toil is to go to the foodstack (which was selected by the WorkGiver_FillTube
            yield return Toils_Goto.GotoThing(FoodToLoad, PathEndMode.ClosestTouch);

            // how much food to take? if the needed amount is greater than the stack size the stack size ist selected
            var foodNeeded = FeedingTube.MaxFoodStored - ((FeedingTube)job.GetTarget(TubeToFill).Thing).FoodCount();
            job.count = job.GetTarget(FoodToLoad).Thing.stackCount > foodNeeded
                ? foodNeeded
                : job.GetTarget(FoodToLoad).Thing.stackCount;

            // after reaching the food stack we should start to carry it
            yield return Toils_Haul.StartCarryThing(FoodToLoad, false, true);

            // now the food stack has to be brought to the feeding tube
            yield return Toils_Goto.GotoThing(TubeToFill, PathEndMode.Touch);

            // filling the feeding tube shall take 240 ticks, we want a progressbar and fail conditions, which Toils_General provides
            Toil waitingToil = null;
            waitingToil = Toils_General.Wait(240)
                .WithProgressBarToilDelay(TubeToFill)
                .FailOn(delegate()
                    {
                        // at the time this code is run curToil actually is not null as we are creating it now and this code is run later
                        var actor = waitingToil?.actor;
                        var curJob = actor?.jobs.curJob;
                        if (!(curJob?.GetTarget(TubeToFill).Thing is FeedingTube dest)) return true;

                        var food = curJob.GetTarget(FoodToLoad).Thing;
                        if (!dest.Storeable(food)) return true;

                        return dest.FoodCount() >= FeedingTube.MaxFoodStored;
                    }
                );

            // adding the wait to our things to do, the wait might be interrupted
            yield return waitingToil
                .FailOnSomeonePhysicallyInteracting(TubeToFill)
                .FailOnDespawnedNullOrForbidden(TubeToFill);

            // now the actual filling is done, to do this we create a custom toil
            var fillTubeToil = new Toil
            {
                initAction = () =>
                {
                    // important, use the same pawn that is already waiting at the tube
                    var actor = waitingToil.actor;
                    var curJob = actor.jobs.curJob;
                    if (!(curJob.GetTarget(TubeToFill).Thing is FeedingTube dest)) return;

                    var max = FeedingTube.MaxFoodStored - dest.FoodCount();
                    var food = curJob.GetTarget(FoodToLoad).Thing;

                    dest.LoadFood(max > food.stackCount ? food : food.SplitOff(max));
                }
            };

            // now this gets also added, this action also might be interrupted
            yield return fillTubeToil
                .FailOnSomeonePhysicallyInteracting(TubeToFill)
                .FailOnDespawnedNullOrForbidden(TubeToFill);
        }
    }
}