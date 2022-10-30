using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace FeedingTube
{
    internal class WorkGiver_FillTube : WorkGiver_Scanner
    {
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return ShouldSkipStatic(pawn);
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public static bool ShouldSkipStatic(Pawn pawn)
        {
            if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
            {
                return true;
            }

            if (pawn.WorkTagIsDisabled(WorkTags.ManualDumb))
            {
                return true;
            }

            if (pawn.WorkTagIsDisabled(WorkTags.Hauling))
            {
                return true;
            }

            return pawn.Faction != Faction.OfPlayer && !pawn.RaceProps.Humanlike;
        }

        public static Job GenerateFillJob(Pawn pawn, Thing thing)
        {
            if (ShouldSkipStatic(pawn))
            {
                return null;
            }

            if (!(thing is FeedingTube tube))
            {
                return null;
            }

            if (!pawn.CanReserve(tube))
            {
                return null;
            }

            bool Validator(Thing x)
            {
                return !x.IsForbidden(pawn) && pawn.CanReserve(x) && tube.Storeable(x) &&
                       tube.FoodCount() < FeedingTube.MaxFoodStored;
            }

            var food = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.FoodSourceNotPlantOrTree),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                Validator
            );

            return food is null ? null : new Job(FillTube_JobDefOf.FillTube, thing, food);
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.listerThings.AllThings.Where(t =>
                t is FeedingTube tube && tube.FoodCount() < FeedingTube.MaxFoodStored);
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            return GenerateFillJob(pawn, thing);
        }
    }
}