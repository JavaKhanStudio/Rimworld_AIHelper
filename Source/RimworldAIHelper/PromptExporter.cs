using RimWorld;
using Verse;
using System.Text;

namespace RimworldAIHelper
{
    [StaticConstructorOnStartup]
    public static class PromptExporter
    {
        static PromptExporter()
        {
            // Run once when the game loads
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Log.Message("Prompt Generator Loaded!");
            });
        }

        public static string MakePrompt(Pawn pawn)
        {
            StringBuilder sb = new StringBuilder();

            // Gender & skin
            sb.Append($"{pawn.gender} human colonist with ");
            sb.Append($"{pawn.story.SkinColor.ToString()} skin, ");

            // Hair
            sb.Append($"{pawn.story.hairDef.defName} hair, ");

            // Apparel
            if (pawn.apparel != null)
            {
                foreach (Apparel app in pawn.apparel.WornApparel)
                {
                    sb.Append($"wearing {app.def.label}, ");
                }
            }

            // Bionics
            foreach (Hediff h in pawn.health.hediffSet.hediffs)
            {
                if (h.def.countsAsAddedPartOrImplant)
                    sb.Append($"with {h.Label}, ");
            }

            return sb.ToString().TrimEnd(',', ' ');
        }
    }
}