// File: Source/AIUtils/PawnMakeTalk.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using PromptGenerator; // AIUtilsMod.Settings, OpenAIClient

namespace RimworldAIHelper.MakeTalk
{
    [StaticConstructorOnStartup]
    public static class AddButtonInPawn
    {
        static AddButtonInPawn()
        {
            var harmony = new Harmony("yourname.promptgenerator");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch("GetGizmos")]
    public static class Pawn_GetGizmos_Patch
    {
        private static string buildTheQuestionPrompt(Pawn pawn)
        {
            return PromptBuilder.BuildQuestionPrompt(pawn);
        }
        
        
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;

            if (__instance?.Faction == Faction.OfPlayer && __instance.RaceProps != null && __instance.RaceProps.Humanlike)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Make Talk",
                    defaultDesc  = "Ask GPT to make this colonist say something.",
                    icon         = TexCommand.PauseCaravan, // replace with your own
                    action       = () => _ = MakePawnSpeakAsync(__instance)
                };
            }
        }

        private static bool _busy;

        private static async Task MakePawnSpeakAsync(Pawn pawn)
        {
            if (_busy) return;
            _busy = true;

            try
            {
                // 1) Token check
                var token = AIUtilsMod.Settings != null ? AIUtilsMod.Settings.GptToken : null;
                if (string.IsNullOrEmpty(token))
                {
                    Messages.Message("GPT token is empty. Set it in Mod Settings.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                // 2) Build prompt
                string prompt = buildTheQuestionPrompt(pawn);
                
                // 3) Call API off-thread
                string body  = OpenAIClient.CreateChatBody(prompt, "gpt-4o-mini");
                string reply = await Task.Run<string>(delegate { return OpenAIClient.PostChat(token, body); });
                string text  = reply != null ? reply.Trim() : "";
                if (string.IsNullOrEmpty(text)) text = "(...)";

                // 4) Speech-like mote (longer)
                ShowSpeechMote(pawn, text);

                // 5) Add to the pawn's built-in Log (Play log)
                TryAddPlayLogEntry(pawn, text);

                // 6) Optional: save to file
                try
                {
                    string dir = Path.Combine(GenFilePaths.SaveDataFolderPath, "ColonistPrompts");
                    Directory.CreateDirectory(dir);
                    string file = Path.Combine(dir, pawn.LabelShortCap + "_say.txt");
                    File.WriteAllText(file, text);
                }
                catch (Exception ioEx)
                {
                    Log.Warning("[AI_Utils] Could not write prompt file: " + ioEx);
                }
            }
            catch (Exception ex)
            {
                Log.Error("[AI_Utils] GPT call failed: " + ex);
                Messages.Message("GPT call failed. Check logs.", MessageTypeDefOf.RejectInput, false);
            }
            finally
            {
                _busy = false;
            }
        }

        

        private static void ShowSpeechMote(Pawn pawn, string text)
        {
            if (pawn == null || pawn.Map == null) return;

            // keep motes readable and short
            const int maxChars = 120;
            if (text != null && text.Length > maxChars)
                text = text.Substring(0, maxChars) + "…";

            text = (text ?? "").Replace("\r", " ").Replace("\n", " ");

            var pos = pawn.DrawPos + new Vector3(0f, 0f, 0.6f);

            // timeMultiplier: higher -> longer on screen
            MoteMaker.ThrowText(pos, pawn.Map, text, Color.white, 15f);
        }

        private static void TryAddPlayLogEntry(Pawn pawn, string text)
        {
            try
            {
                if (pawn == null) return;
                if (Find.PlayLog == null) return;

                // Create and add our custom entry; it will show on the pawn's Log tab.
                var entry = new PlayLogEntry_GPTSay(pawn, text);
                Find.PlayLog.Add(entry);
            }
            catch (Exception e)
            {
                Log.Warning("[AI_Utils] Couldn't add to pawn play log: " + e);
            }
        }
    }

    /// <summary>
    /// Minimal custom Play Log entry that targets a single pawn and prints "Name: text".
    /// Shows up on the pawn's Log tab because the pawn is returned as a concern.
    /// </summary>
    public class PlayLogEntry_GPTSay : LogEntry
    {
        private Pawn pawn;
        private string line;

        public PlayLogEntry_GPTSay() { } // scribe

        public PlayLogEntry_GPTSay(Pawn p, string t)
        {
            pawn = p;
            line = t ??  string.Empty;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref line, "line", string.Empty);
        }

        // NOTE: signatures below match RW 1.4–1.6
        public override IEnumerable<Thing> GetConcerns()
        {
            if (pawn != null) yield return pawn;
        }

        public override bool Concerns(Thing t) => t == pawn;


        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
        {
            string who = pawn != null ? pawn.LabelShortCap : "Someone";
            return who + ": " + (line ?? "");
        }

        public override string GetTipString()
        {
            try
            {
                string who = (pawn != null) ? pawn.LabelShortCap : "Someone";
                string text = string.IsNullOrEmpty(line) ? "(...)" : line;
                return $"{who}\n\n{text}";
            }
            catch
            {
                return string.Empty;
            }
        }

       
        public override bool CanBeClickedFromPOV(Thing pov) => false;

        // Optional: if you want an icon; returning null is usually OK, but be defensive:
        public override Texture2D IconFromPOV(Thing pov) => null;
    }
}
