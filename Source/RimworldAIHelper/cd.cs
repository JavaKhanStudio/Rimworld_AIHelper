// File: Source/AIUtils/TwitchTalkIntegration.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimworldAIHelper.MakeTalk
{
    [StaticConstructorOnStartup]
    public static class TwitchTalkBootstrap
    {
        static TwitchTalkBootstrap()
        {
            try
            {
                var harmony = new Harmony("yourname.promptgenerator.twitch");
                // Try a few likely handlers across TT versions
                // 1) Command processing
                TryPatch(harmony,
                    typeName: "TwitchToolkit.Commands.CommandProcessor",
                    methodName: "ProcessMessage",
                    paramTypes: new[] { typeof(string), typeof(string) });

                // 2) Raw chat handler
                TryPatch(harmony,
                    typeName: "TwitchToolkit.Twitch.TwitchChat",
                    methodName: "OnMessageReceived",
                    paramTypes: new[] { typeof(string), typeof(string), typeof(string) }); // user, message, channel?

                // 3) ToolkitCore path (sometimes wrapped differently)
                TryPatch(harmony,
                    typeName: "ToolkitCore.Services.ChatService",
                    methodName: "HandleMessage",
                    paramTypes: new[] { typeof(string), typeof(string) });

                // If none patched, we log once.
                if (_patchedCount == 0)
                    Log.Warning("[AI_Utils] TwitchToolkit not found or no known chat handler method to patch. Twitch command disabled.");
            }
            catch (Exception e)
            {
                Log.Warning("[AI_Utils] Twitch integration bootstrap failed: " + e);
            }
        }

        private static int _patchedCount = 0;

        private static void TryPatch(Harmony h, string typeName, string methodName, Type[] paramTypes)
        {
            try
            {
                var t = AccessTools.TypeByName(typeName);
                if (t == null) return;

                MethodInfo m;
                if (paramTypes != null)
                    m = AccessTools.Method(t, methodName, paramTypes);
                else
                    m = AccessTools.Method(t, methodName);

                if (m == null) return;

                var postfix = new HarmonyMethod(typeof(TwitchTalkPatch).GetMethod(nameof(TwitchTalkPatch.PostfixChat), BindingFlags.Static | BindingFlags.Public));
                h.Patch(m, postfix: postfix);
                _patchedCount++;
                Log.Message("[AI_Utils] Patched TwitchToolkit method: " + t.FullName + "." + methodName);
            }
            catch (Exception e)
            {
                Log.Warning("[AI_Utils] Failed to patch " + typeName + "." + methodName + ": " + e);
            }
        }
    }

    public static class TwitchTalkPatch
    {
        // This postfix runs after TT handles any chat message.
        // We'll parse the message ourselves and trigger our action on !talk.
        public static void PostfixChat(object __instance, params object[] __args)
        {
            try
            {
                // Common signatures we tried:
                // - (string user, string message)
                // - (string user, string message, string channel)
                // We'll dig out the first string = user, second string = message.

                string user = null;
                string msg  = null;

                foreach (var a in __args)
                {
                    if (a is string s)
                    {
                        if (user == null) { user = s; }
                        else if (msg == null) { msg = s; break; }
                    }
                }

                if (string.IsNullOrEmpty(msg)) return;

                // Parse command: "!talk [pawn name]"
                // Examples: "!talk", "!talk Emmie", "!talk john smith"
                var trimmed = msg.Trim();
                if (!trimmed.StartsWith("!talk", StringComparison.OrdinalIgnoreCase))
                    return;

                string arg = trimmed.Length > 5 ? trimmed.Substring(5).Trim() : string.Empty;

                // Find target pawn
                Pawn target = FindTargetPawn(arg, user);
                if (target == null)
                {
                    Messages.Message("No matching colonist found.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                // Fire and forget our existing async flow
                _ = MakePawnSpeakAsync(target);
            }
            catch (Exception e)
            {
                Log.Warning("[AI_Utils] Twitch command handler failed: " + e);
            }
        }

        // === Reuse or inline your existing async GPT call ===
        private static bool _busy;
        private static async Task MakePawnSpeakAsync(Pawn pawn)
        {
            if (_busy) return;
            _busy = true;

            try
            {
                var token = PromptGenerator.AIUtilsMod.Settings != null ? PromptGenerator.AIUtilsMod.Settings.GptToken : null;
                if (string.IsNullOrEmpty(token))
                {
                    Messages.Message("GPT token is empty. Set it in Mod Settings.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                string prompt = RimworldAIHelper.MakeTalk.PromptBuilder.BuildQuestionPrompt(pawn);

                string body  = PromptGenerator.OpenAIClient.CreateChatBody(prompt, "gpt-4o-mini");
                string reply = await Task.Run<string>(delegate { return PromptGenerator.OpenAIClient.PostChat(token, body); });

                string text = reply != null ? reply.Trim() : "";
                if (string.IsNullOrEmpty(text)) text = "(...)";

                // Mote
                ShowSpeechMote(pawn, text);
                // Play log
                Find.PlayLog?.Add(new RimworldAIHelper.MakeTalk.PlayLogEntry_GPTSay(pawn, text));
            }
            catch (Exception ex)
            {
                Log.Error("[AI_Utils] GPT call (twitch) failed: " + ex);
            }
            finally
            {
                _busy = false;
            }
        }

        private static void ShowSpeechMote(Pawn pawn, string text)
        {
            if (pawn == null || pawn.Map == null) return;

            const int maxChars = 120;
            if (text != null && text.Length > maxChars)
                text = text.Substring(0, maxChars) + "…";
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ");

            var pos = pawn.DrawPos + new Vector3(0f, 0f, 0.6f);
            MoteMaker.ThrowText(pos, pawn.Map, text, Color.white, 7f);
        }

        // Try to resolve the pawn to talk:
        // 1) if a name was typed, fuzzy match colonists by label
        // 2) else pick a random free colonist on current map
        // (Advanced: if you use TwitchToolkit colonist assignment -> reflect their mapping here.)
        private static Pawn FindTargetPawn(string nameOrEmpty, string twitchUser)
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return null;

                var pawns = map.mapPawns?.FreeColonists?.ToList() ?? new List<Pawn>();
                if (pawns.Count == 0) return null;

                if (!string.IsNullOrEmpty(nameOrEmpty))
                {
                    // case-insensitive contains on LabelShort/Name
                    string needle = nameOrEmpty.ToLowerInvariant();
                    var match = pawns.FirstOrDefault(p =>
                    {
                        var a = p.LabelShortCap.ToLowerInvariant();
                        if (a.Contains(needle)) return true;
                        var b = p.Name?.ToStringFull?.ToLowerInvariant();
                        return !string.IsNullOrEmpty(b) && b.Contains(needle);
                    });
                    if (match != null) return match;
                }

                // TODO: If you want, reflect TwitchToolkit's viewer->pawn binding here.
                // Fallback: random colonist
                return pawns.RandomElement();
            }
            catch
            {
                return null;
            }
        }
    }
}
