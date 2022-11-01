using RimWorld;
using Verse;

namespace FeedingTube
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class FeedingTubeMod : Mod
    {
        internal static readonly NeedDef ThirstDef;

        static FeedingTubeMod()
        {
            ThirstDef = DefDatabase<NeedDef>.GetNamedSilentFail("DBHThirst");
            if (ThirstDef != null)
                Log.Message("[FeedingTube]: Dubs Bad Hygiene is loaded, tube will fill thirst need when feeding");
        }

        public FeedingTubeMod(ModContentPack content) : base(content)
        {
            Log.Message("[FeedingTube]: loaded");
        }
        
        internal static JobDef FillTubeJob => DefDatabase<JobDef>.GetNamed("nukulartechniker_fill_tube");
    }
}