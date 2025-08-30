using RimWorld;
using RimworldAIHelper;
using Verse;
using UnityEngine;

namespace PromptGenerator
{
    public class MainTabWindow_PromptGenerator : MainTabWindow
    {
        private bool showToken;
        private string editBuffer;

        public MainTabWindow_PromptGenerator()
        {
            editBuffer = AIUtilsMod.Settings?.GptToken ?? "";
        }

        public override Vector2 RequestedTabSize => new Vector2(700f, 420f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Prompt Generator");
            listing.GapLine();

            // --- Token row ---
            listing.Label("GPT token");

            Rect row = listing.GetRect(Text.LineHeight);
            Rect fieldRect = new Rect(row.x, row.y, row.width - 140f, row.height);
            Rect toggleRect = new Rect(row.x + row.width - 135f, row.y, 65f, row.height);
            Rect saveRect   = new Rect(row.x + row.width - 65f,  row.y, 65f, row.height);

            // Editable either way:
            if (showToken)
            {
                editBuffer = Widgets.TextField(fieldRect, editBuffer ?? "");
            }
            else
            {
                // Masked but editable
                // Requires UnityEngine.IMGUIModule (you added it) for GUI.PasswordField
                editBuffer = GUI.PasswordField(fieldRect, editBuffer ?? "", '•');
            }

            if (Widgets.ButtonText(toggleRect, showToken ? "Hide" : "Show"))
                showToken = !showToken;

            if (Widgets.ButtonText(saveRect, "Save"))
            {
                AIUtilsMod.Settings.GptToken = editBuffer ?? "";
                // Persist right away so you don’t need to open Mod Settings
                AIUtilsMod.Settings.Write();
                Messages.Message("GPT token saved.", MessageTypeDefOf.TaskCompletion, false);
            }

            listing.Gap(12f);
            listing.Label("Tip: You can type even when hidden (masked). Click Save to persist.");

            listing.End();
        }
    }
}

