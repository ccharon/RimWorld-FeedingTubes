﻿using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace FeedingTube
{
    internal class Job_FillTube : JobDriver
    {
        private const TargetIndex tubeToFill = TargetIndex.A;
        private const TargetIndex foodToLoad = TargetIndex.B;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(foodToLoad), job, 1, job.GetTarget(foodToLoad).Thing.stackCount) &&
                   pawn.Reserve(job.GetTarget(tubeToFill), job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(tubeToFill);
            this.FailOnForbidden(foodToLoad);

            yield return Toils_Goto.GotoThing(foodToLoad, PathEndMode.ClosestTouch);
            var foodNeeded = FeedingTube.MaxFoodStored - ((FeedingTube)job.GetTarget(tubeToFill).Thing).foodCount();
            job.count = job.GetTarget(foodToLoad).Thing.stackCount > foodNeeded
                ? foodNeeded
                : job.GetTarget(foodToLoad).Thing.stackCount;

            yield return Toils_Haul.StartCarryThing(foodToLoad, false, true);
            yield return Toils_Goto.GotoThing(tubeToFill, PathEndMode.Touch);
            Toil curToil = null;
            curToil = Toils_General.Wait(240).WithProgressBarToilDelay(TargetIndex.A).FailOn(delegate()
            {
                var actor = curToil?.actor;
                var curJob = actor?.jobs.curJob;
                var dest = curJob?.GetTarget(tubeToFill).Thing as FeedingTube;
                if (dest == null)
                {
                    return true;
                }

                var food = curJob.GetTarget(foodToLoad).Thing;
                if (!dest.Storeable(food))
                {
                    return true;
                }

                return dest.foodCount() >= FeedingTube.MaxFoodStored;
            });
            yield return curToil.FailOnSomeonePhysicallyInteracting(tubeToFill)
                .FailOnDespawnedNullOrForbidden(tubeToFill);
            var toil = curToil;
            curToil = new Toil
            {
                initAction = () =>
                {
                    var actor = toil.actor;
                    var curJob = actor.jobs.curJob;
                    var dest = curJob.GetTarget(tubeToFill).Thing as FeedingTube;
                    if (dest == null)
                    {
                        return;
                    }

                    var max = FeedingTube.MaxFoodStored - dest.foodCount();
                    var food = curJob.GetTarget(foodToLoad).Thing;
                    if (max > food.stackCount)
                    {
                        dest.LoadFood(food);
                    }
                    else
                    {
                        Log.Message(
                            $"Having to split off: Max{FeedingTube.MaxFoodStored} Cur{dest.foodCount()} Math{max}");
                        dest.LoadFood(food.SplitOff(max));
                    }
                }
            };
            yield return curToil.FailOnSomeonePhysicallyInteracting(tubeToFill)
                .FailOnDespawnedNullOrForbidden(tubeToFill);
        }
    }
}