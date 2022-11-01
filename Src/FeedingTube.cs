using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace FeedingTube
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class FeedingTube : Building, ISlotGroupParent
    {
        public const int MaxFoodStored = 10;
        public const int MinFoodToTriggerRefill = 2;
        private readonly SlotGroup _slotGroup;
        private List<IntVec3> _cachedOccupiedCells;
        private List<Thing> _foodStored = new List<Thing>();
        private StorageSettings _settings;

        public FeedingTube()
        {
            _slotGroup = new SlotGroup(this);
        }

        public void Notify_SettingsChanged()
        {
        }

        public bool StorageTabVisible => true;

        public bool IgnoreStoredThingsBeauty => def.building.ignoreStoredThingsBeauty;

        public SlotGroup GetSlotGroup()
        {
            return _slotGroup;
        }

        public virtual void Notify_ReceivedThing(Thing newItem)
        {
            if (Faction == Faction.OfPlayer && newItem.def.storedConceptLearnOpportunity != null)
                LessonAutoActivator.TeachOpportunity(newItem.def.storedConceptLearnOpportunity,
                    OpportunityType.GoodToKnow);
        }

        public virtual void Notify_LostThing(Thing newItem)
        {
        }

        public virtual IEnumerable<IntVec3> AllSlotCells()
        {
            return GenAdj.CellsOccupiedBy(this);
        }

        public List<IntVec3> AllSlotCellsList()
        {
            return _cachedOccupiedCells ??= AllSlotCells().ToList();
        }

        public StorageSettings GetStoreSettings()
        {
            return _settings;
        }

        public StorageSettings GetParentStoreSettings()
        {
            return def.building.fixedStorageSettings;
        }

        public string SlotYielderLabel()
        {
            return LabelCap;
        }

        public bool Accepts(Thing t)
        {
            return false;
        }

        public bool Storeable(Thing t)
        {
            return _settings.AllowedToAccept(t);
        }

        public override void PostMake()
        {
            base.PostMake();
            _settings = new StorageSettings(this);
            _foodStored = new List<Thing>();

            if (def.building.defaultStorageSettings != null) _settings.CopyFrom(def.building.defaultStorageSettings);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            _cachedOccupiedCells = null;
            base.SpawnSetup(map, respawningAfterLoad);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref _settings, "settings", this);
            Scribe_Collections.Look(ref _foodStored, "foodStored", LookMode.Deep);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;
            foreach (var gizmo2 in StorageSettingsClipboard.CopyPasteGizmosFor(_settings)) yield return gizmo2;

            yield return new Command_Action
            {
                action = Empty,
                hotKey = KeyBindingDefOf.Misc1,
                defaultDesc = "Empty the tube",
                icon = ContentFinder<Texture2D>.Get("UI/Designators/Open"),
                defaultLabel = "Empty"
            };
        }

        public int FoodCount()
        {
            _foodStored ??= new List<Thing>();
            return _foodStored.Sum(t => t.stackCount);
        }

        /// <summary>
        ///     adding food to storage, first try to add to an existing stack of food, if that is not possible a new stack
        ///     of food will be added
        /// </summary>
        /// <param name="food"></param>
        public void LoadFood(Thing food)
        {
            _foodStored ??= new List<Thing>();

            Log.Message($"Received {food.stackCount} food.");
            foreach (var stackable in _foodStored.Where(t => t.CanStackWith(food)))
            {
                stackable.TryAbsorbStack(food, true);
                if (food.stackCount <= 0) return;
            }

            if (food.stackCount > 0) _foodStored.Add(food.SplitOff(food.stackCount));
        }

        public override string GetInspectString()
        {
            _foodStored ??= new List<Thing>();

            var builder = new StringBuilder();
            builder.Append(base.GetInspectString());

            if (builder.Length > 0) builder.Append("\n");

            builder.Append("stored:");
            foreach (var food in _foodStored) builder.Append($"\n{food.Label}");

            return builder.ToString();
        }

        private void Empty()
        {
            foreach (var food in _foodStored) GenPlace.TryPlaceThing(food, Position, Map, ThingPlaceMode.Near);
            _foodStored.Clear();
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Empty();
            base.Destroy(mode);
        }

        /// <summary>
        ///     creating menu entries
        /// </summary>
        /// <param name="selPawn"></param>
        /// <returns>a list of menu entries</returns>
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            // getting all entries specific to the selected pawn
            foreach (var floatMenuOption in base.GetFloatMenuOptions(selPawn)) yield return floatMenuOption;

            if (FoodCount() >= MaxFoodStored)
                // the feeding tube is full
                yield return new FloatMenuOption("Fill (full)", null);

            else if (WorkGiver_FillTube.ShouldSkipStatic(selPawn))
                // the pawn will not do it
                yield return new FloatMenuOption("Fill (unwilling)", null);

            else
                // feeding tube can be be filled, when clicked a job will be stqrted
                yield return new FloatMenuOption("Fill", () =>
                    {
                        var doFill = WorkGiver_FillTube.GenerateFillJob(selPawn, this);
                        if (doFill != null) selPawn.jobs.TryTakeOrderedJob(doFill);
                    }
                );
        }

        public override void Tick()
        {
            base.Tick();

            var bed = (Position + Rotation.FacingCell).GetFirstThing<Building_Bed>(Map);
            if (bed == null || !bed.CurOccupants.Any()) return;

            foreach (var pawn in bed.CurOccupants)
            {
                if (FoodCount() == 0) return;

                if (!FeedPatientUtility.IsHungry(pawn)) continue;
                var currentFood = _foodStored.First();
                pawn.needs.food.CurLevel += currentFood.Ingested(pawn, pawn.needs.food.NutritionWanted);

                if (currentFood.Destroyed) _foodStored.Remove(currentFood);

                // thirst is added by dubs bath hygiene
                if (FeedingTubeMod.ThirstDef == null) continue;
                var thirstNeed = pawn.needs.TryGetNeed(FeedingTubeMod.ThirstDef);
                thirstNeed.CurLevel = thirstNeed.MaxLevel;
            }
        }
    }
}