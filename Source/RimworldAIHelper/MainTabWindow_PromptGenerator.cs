using System.Threading.Tasks;
using PromptGenerator;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimworldAIHelper
{
    public class MainTabWindow_PromptGenerator : MainTabWindow
    {
        private bool showToken;
        private string editBuffer;
        private string prompt = "Only give the anwser, no preambel " +
                                "Write a one-sentence backstory for this pawn.";
        private string lastResult = "";
        private bool isBusy;

        public override Vector2 RequestedTabSize => new Vector2(700f, 420f);

        public MainTabWindow_PromptGenerator()
        {
            editBuffer = AIUtilsMod.Settings?.GptToken ?? "";
        }

        public override void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);

            list.Label("Prompt Generator");
            list.GapLine();

            // --- Token row (editable + masked) ---
            list.Label("GPT token");
            Rect row = list.GetRect(Text.LineHeight);
            Rect field = new Rect(row.x, row.y, row.width - 140f, row.height);
            Rect toggle = new Rect(row.x + row.width - 135f, row.y, 65f, row.height);
            Rect save   = new Rect(row.x + row.width - 65f,  row.y, 65f, row.height);

            if (showToken) editBuffer = Widgets.TextField(field, editBuffer ?? "");
            else           editBuffer = GUI.PasswordField(field, editBuffer ?? "", '•');

            if (Widgets.ButtonText(toggle, showToken ? "Hide" : "Show")) showToken = !showToken;
            if (Widgets.ButtonText(save, "Save"))
            {
                AIUtilsMod.Settings.GptToken = editBuffer ?? "";
                AIUtilsMod.Settings.Write();
                Messages.Message("GPT token saved.", MessageTypeDefOf.TaskCompletion, false);
            }

            list.Gap(8f);

            // --- Prompt input ---
            list.Label("Prompt");
            prompt = Widgets.TextArea(list.GetRect(60f), prompt ?? "");

            list.Gap(8f);

            GUI.enabled = !isBusy;
            if (Widgets.ButtonText(list.GetRect(30f), isBusy ? "Calling..." : "Call GPT"))
                _ = CallGptAsync();
            GUI.enabled = true;


            list.Gap(8f);
            list.Label("Result:");
            Rect resultRect = list.GetRect(160f);
            lastResult = Widgets.TextArea(resultRect, lastResult ?? "");


            list.End();
        }

        private Vector2 _tempScroll = default;

        private async Task CallGptAsync()
        {
            if (isBusy) return;
            isBusy = true;
            lastResult = "";

            string token = AIUtilsMod.Settings?.GptToken;
            string body = OpenAIClient.CreateChatBody(prompt, model: "gpt-4o-mini");

            try
            {
                // Run HTTP off-thread
                string reply = await Task.Run(() => OpenAIClient.PostChat(token, body));

                // Back on main thread: just assign the string; UI will repaint
                lastResult = reply ?? "(empty)";
            }
            catch (System.Exception ex)
            {
                Log.Error($"[AI_Utils] OpenAI call failed: {ex}");
                lastResult = "Error: " + ex.Message;
            }
            finally
            {
                isBusy = false;
            }
        }
    }
}