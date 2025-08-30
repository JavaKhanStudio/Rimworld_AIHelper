using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace RimworldAIHelper.MakeTalk
{
    public static class PromptBuilder
    {
        public static string BuildQuestionPrompt(Pawn pawn, int maxChars = 900)
        {
            if (pawn == null) return "Only say the answer and nothing else. Say something.";

            var sb = new StringBuilder(512);

            // Hard rule for the model
            sb.Append("Only output the spoken line, first-person, one short sentence. No narration. ");

            // Identity
            sb.Append("You are ");
            sb.Append(pawn.LabelShortCap);
            if (pawn.Faction != null)
                sb.Append(" of ").Append(pawn.Faction.Name);
            sb.Append(". ");

            // Personality/backstory
            var personality = SummarizePersonality(pawn);
            if (!string.IsNullOrEmpty(personality))
                sb.Append("Personality: ").Append(personality).Append(". ");

            // Passions/skills (top 2)
            var passions = TopPassions(pawn, 2);
            if (!string.IsNullOrEmpty(passions))
                sb.Append("Passions: ").Append(passions).Append(". ");

            // Health snapshot
            var health = SummarizeHealth(pawn);
            if (!string.IsNullOrEmpty(health))
                sb.Append("Health: ").Append(health).Append(". ");

            // Mood/needs
            var mood = SummarizeMood(pawn);
            if (!string.IsNullOrEmpty(mood))
                sb.Append("Mood: ").Append(mood).Append(". ");

            // Last 3 events from the play log
            var events3 = LastLogLines(pawn, 3);
            if (!string.IsNullOrEmpty(events3))
                sb.Append("Recent events: ").Append(events3).Append(". ");

            // Final instruction
            sb.Append("Make \"").Append(pawn.LabelShortCap).Append("\" say something appropriate.");

            // Clean up & limit size
            var text = Sanitize(sb.ToString());
            if (text.Length > maxChars) text = text.Substring(0, maxChars) + "…";
            return text;
        }

        // --- Helpers ---

        private static string SummarizePersonality(Pawn p)
        {
            try
            {
                var bits = new List<string>();

                // Backstories
                var child = p.story?.Childhood?.title;
                var adult = p.story?.Adulthood?.title;
                if (!string.IsNullOrEmpty(child)) bits.Add("childhood: " + child);
                if (!string.IsNullOrEmpty(adult)) bits.Add("adulthood: " + adult);

                // Traits (top 2 by degree magnitude)
                var traits = p.story?.traits?.allTraits;
                if (traits != null && traits.Count > 0)
                {
                    var top = traits
                        .OrderByDescending(t => Math.Abs(t.Degree))
                        .Take(2)
                        .Select(t => t.LabelCap);
                    var tstr = string.Join(", ", top);
                    if (!string.IsNullOrEmpty(tstr)) bits.Add("traits: " + tstr);
                }

                return string.Join("; ", bits);
            }
            catch { return null; }
        }

        private static string TopPassions(Pawn p, int take)
        {
            try
            {
                if (p.skills == null) return null;
                var top = p.skills.skills
                    .Where(s => s.passion > Passion.None)
                    .OrderByDescending(s => s.Level)
                    .ThenByDescending(s => s.passion)
                    .Take(Mathf.Max(1, take))
                    .Select(s => s.def.label + (s.passion == Passion.Major ? " (major)" : " (minor)"));

                var sTop = string.Join(", ", top);
                return string.IsNullOrEmpty(sTop) ? null : sTop;
            }
            catch { return null; }
        }

        private static string SummarizeHealth(Pawn p)
        {
            try
            {
                if (p.health == null) return null;
                var bits = new List<string>();

                // Pain
                var pain = p.health.hediffSet?.PainTotal ?? 0f;
                if (pain > 0.01f)
                    bits.Add("pain " + ToPct(pain));

                // Bleed rate
                var bleed = p.health.hediffSet?.BleedRateTotal ?? 0f;
                if (bleed > 0f)
                    bits.Add("bleeding");

                // Notable hediffs: implants/injuries (max 3)
                var hediffs = p.health.hediffSet?.hediffs;
                if (hediffs != null)
                {
                    var notable = hediffs
                        .Where(h => h.Visible && (h.IsPermanent() || h.Bleeding || h.def.countsAsAddedPartOrImplant))
                        .Select(h => h.LabelCap)
                        .Distinct()
                        .Take(3)
                        .ToList();
                    if (notable.Count > 0)
                        bits.Add(string.Join(", ", notable));
                }

                // Tired/hungry (quick read from needs)
                var tired = p.needs?.rest?.CurCategory;
                var hungry = p.needs?.food?.CurCategory;
                if (tired != null && tired != RimWorld.RestCategory.Rested) bits.Add("tired");
                if (hungry != null && hungry != RimWorld.HungerCategory.Fed) bits.Add("hungry");

                var sum = string.Join("; ", bits);
                return string.IsNullOrEmpty(sum) ? "stable" : sum;
            }
            catch { return null; }
        }

        private static string SummarizeMood(Pawn p)
        {
            try
            {
                var mood = p.needs?.mood;
                if (mood == null) return null;
                var pct = mood.CurInstantLevelPercentage; // 0..1
                string moodWord =
                    pct >= 0.8f ? "excellent" :
                    pct >= 0.6f ? "good" :
                    pct >= 0.4f ? "neutral" :
                    pct >= 0.2f ? "low" : "terrible";

                // One strongest thought label, if any
                string thought = null;
                var mem = mood?.thoughts?.memories;
                if (mem != null)
                {
                    var strongest = mem.Memories
                        .OrderByDescending(m => Math.Abs(m.MoodOffset()))
                        .FirstOrDefault();
                    if (strongest != null)
                        thought = strongest.LabelCap;
                }

                return thought != null ? (moodWord + " (" + thought + ")") : moodWord;
            }
            catch { return null; }
        }

        private static string LastLogLines(Pawn p, int count)
        {
            try
            {
                var log = Find.PlayLog;
                if (log == null) return null;

                // PlayLog exposes AllEntries; filter those that concern this pawn
                var list = log.AllEntries; // safer in 1.4–1.6
                if (list == null || list.Count == 0) return null;

                // Take the last N entries that list this pawn as a concern
                var recent = list
                    .Where(e =>
                    {
                        try { return e != null && e.GetConcerns().Any(t => t == p); }
                        catch { return false; }
                    })
                    .Reverse()    // newest first
                    .Take(Mathf.Max(1, count))
                    .Select(e =>
                    {
                        try { return e.ToGameStringFromPOV(p, false); }
                        catch { return null; }
                    })
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Reverse();   // restore chronological order

                var joined = string.Join(" | ", recent);
                if (string.IsNullOrEmpty(joined)) return null;

                // Trim very long logs
                if (joined.Length > 220) joined = joined.Substring(0, 220) + "…";
                return joined;
            }
            catch { return null; }
        }

        private static string ToPct(float f) => Mathf.RoundToInt(Mathf.Clamp01(f) * 100f) + "%";

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("\r", " ").Replace("\n", " ");
            // keep quotes but normalize weird whitespace
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s.Trim();
        }
    }
}
