using UnityEngine;
using Verse;

namespace PromptGenerator
{
    public class AIUtilsSettings : ModSettings
    {
        public string GptToken = "";
        public override void ExposeData() => Scribe_Values.Look(ref GptToken, "GptToken", "");
    }

    public class AIUtilsMod : Mod
    {
        public static AIUtilsSettings Settings;
        private bool showToken;

        public AIUtilsMod(ModContentPack pack) : base(pack)
        {
            Settings = GetSettings<AIUtilsSettings>();
        }

        public override string SettingsCategory() => "AI_Utils";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("GPT token");

            // one row with field + Show/Hide + Copy
            Rect row = listing.GetRect(Text.LineHeight);
            Rect fieldRect  = new Rect(row.x, row.y, row.width - 130f, row.height);
            Rect showRect   = new Rect(row.x + row.width - 125f, row.y, 60f, row.height);
            Rect copyRect   = new Rect(row.x + row.width - 60f,  row.y, 60f, row.height);

            if (showToken)
            {
                Settings.GptToken = Widgets.TextField(fieldRect, Settings.GptToken ?? "");
            }
            else
            {
                string masked = new string('•', Mathf.Min((Settings.GptToken ?? "").Length, 64));
                Widgets.TextField(fieldRect, masked); // display only
                TooltipHandler.TipRegion(fieldRect, "Click 'Show' to edit");
            }

            if (Widgets.ButtonText(showRect, showToken ? "Hide" : "Show"))
                showToken = !showToken;

            if (Widgets.ButtonText(copyRect, "Copy"))
                GUIUtility.systemCopyBuffer = Settings.GptToken ?? "";

            listing.Gap();
            listing.Label("Tip: keep this secret. Use 'Show' to edit, 'Copy' to copy.");

            listing.End();
        }
    }
}