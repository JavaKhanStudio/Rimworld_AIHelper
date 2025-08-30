// File: Source/AIUtils/Dialog_PawnTalkLog.cs
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PromptGenerator
{
    public class Dialog_PawnTalkLog : Window
    {
        private readonly Pawn pawn;
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(640f, 480f);

        public Dialog_PawnTalkLog(Pawn p)
        {
            pawn = p;
            doCloseButton = true;
            draggable = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(inRect.TopPartPixels(32f), $"{pawn.LabelShortCap} – AI Talk Log");
            Text.Font = GameFont.Small;

            var listRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 80f);
            var entries = PawnTalkLog.Inst.For(pawn);

            var sb = new StringBuilder();
            foreach (var e in entries)
            {
                var day = GenDate.DateFullStringAt(e.gameTicks, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile));
                sb.AppendLine($"[{day}] {e.text}");
            }
            string all = sb.ToString().TrimEnd();
            float viewW = listRect.width - 16f;
            float viewH = Text.CalcHeight(all, viewW);
            var view = new Rect(0f, 0f, viewW, Mathf.Max(viewH, listRect.height));

            scroll = GUI.BeginScrollView(listRect, scroll, view);
            Widgets.Label(view, all);
            GUI.EndScrollView();

            var copyRect = new Rect(inRect.xMax - 120f, inRect.yMax - 30f, 110f, 24f);
            if (Widgets.ButtonText(copyRect, "Copy all"))
                GUIUtility.systemCopyBuffer = all;
        }
    }
}