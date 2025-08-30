// File: Source/AIUtils/PawnTalkLog.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PromptGenerator
{
    public class PawnTalkEntry : IExposable
    {
        public string pawnLoadId;
        public string text;
        public int gameTicks;

        public PawnTalkEntry() { }
        public PawnTalkEntry(string id, string t)
        {
            pawnLoadId = id;
            text = t;
            gameTicks = Find.TickManager.TicksGame;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnLoadId, "pawnLoadId");
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref gameTicks, "gameTicks", 0);
        }
    }

    // Saved with the game; holds all entries
    public class PawnTalkLog : GameComponent
    {
        private List<PawnTalkEntry> entries = new List<PawnTalkEntry>();

        public PawnTalkLog() { }
        public PawnTalkLog(Game game) { }

        public static PawnTalkLog Inst => Current.Game.GetComponent<PawnTalkLog>();

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref entries, "aiutils_talk_entries", LookMode.Deep);
            if (entries == null) entries = new List<PawnTalkEntry>();
        }

        public void Add(Pawn p, string text)
        {
            if (p == null || string.IsNullOrEmpty(text)) return;
            var id = p.GetUniqueLoadID();           // stable across saves
            entries.Add(new PawnTalkEntry(id, text));

            // keep it bounded to avoid bloat (per save)
            const int maxEntries = 2000;
            if (entries.Count > maxEntries)
                entries.RemoveRange(0, entries.Count - maxEntries);
        }

        public IEnumerable<PawnTalkEntry> For(Pawn p, int last = 50)
        {
            var id = p.GetUniqueLoadID();
            // newest last for nicer reading
            return entries.Where(e => e.pawnLoadId == id)
                          .OrderByDescending(e => e.gameTicks)
                          .Take(last)
                          .OrderBy(e => e.gameTicks);
        }
    }
}
