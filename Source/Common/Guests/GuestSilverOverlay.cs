using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Циферки серебра над гостями Hospitality: иконка серебра и остаток денег
    /// на покупки — только если у гостя больше 40 серебра. Выключается в
    /// настройках мода (Options -> Mod settings -> HSK More Balance).
    /// Рисуем в GUI-проходе карты (MapComponentOnGUI) — свой канал отрисовки,
    /// чужие патчи на пешечные оверлеи (Camera+ и т.п.) не мешают.
    /// MapComponent создаётся движком автоматически, в сейв ничего не пишет.
    /// </summary>
    public class MapComponent_GuestSilverOverlay : MapComponent
    {
        private const int ShowThreshold = 40;

        private static readonly System.Type visitLordJobType =
            AccessTools.TypeByName("Hospitality.LordJob_VisitColony");

        private static readonly Color silverColor = new Color(0.85f, 0.87f, 0.9f);

        public MapComponent_GuestSilverOverlay(Map map) : base(map)
        {
        }

        public override void MapComponentOnGUI()
        {
            if (visitLordJobType == null) // Hospitality не установлен
                return;
            if (HSKMoreHardcoreMod.Settings == null || !HSKMoreHardcoreMod.Settings.showGuestSilver)
                return;
            if (Event.current.type != EventType.Repaint)
                return;

            // Порог зума из настроек (0 = показывать всегда)
            float cellPx = Find.CameraDriver.CellSizePixels;
            if (cellPx < HSKMoreHardcoreMod.Settings.guestSilverZoom)
                return;

            CellRect viewRect = Find.CameraDriver.CurrentViewRect;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.Faction == null || pawn.Faction.IsPlayer || !pawn.RaceProps.Humanlike)
                    continue;
                if (!viewRect.Contains(pawn.Position) || map.fogGrid.IsFogged(pawn.Position))
                    continue;

                var lordJob = pawn.GetLord()?.LordJob;
                if (lordJob == null || !visitLordJobType.IsInstanceOfType(lordJob))
                    continue;

                int silver = pawn.inventory?.innerContainer?.TotalStackCountOfDef(ThingDefOf.Silver) ?? 0;
                if (silver <= ShowThreshold)
                    continue;

                DrawSilverTag(pawn, silver, cellPx);
            }
        }

        private static void DrawSilverTag(Pawn pawn, int silver, float cellPx)
        {
            // Якорь — справа от головы пешки, вплотную к спрайту; метка растёт
            // вправо и центрируется по вертикали на уровне головы
            Vector3 world = pawn.DrawPos;
            world.x += 0.35f;
            world.z += 0.5f;
            Vector2 pos = Find.Camera.WorldToScreenPoint(world) / Prefs.UIScale;
            pos.y = UI.screenHeight - pos.y;

            // Масштаб от зума: ~42 пикселя на клетку — базовый размер
            float scale = Mathf.Clamp(cellPx / 42f, 0.75f, 1.4f);
            float iconSize = 15f * scale;
            float pad = 3f * scale;

            Text.Font = scale >= 1.15f ? GameFont.Small : GameFont.Tiny;
            string text = silver.ToString();
            Vector2 textSize = Text.CalcSize(text);

            float rowHeight = Mathf.Max(iconSize, textSize.y);
            float bgHeight = rowHeight + pad * 2f;
            Rect bgRect = new Rect(pos.x, pos.y - bgHeight / 2f,
                pad + iconSize + 3f * scale + textSize.x + pad,
                bgHeight);

            // Тёмная плашка с едва заметной рамкой — читаемее ванильной серой
            Widgets.DrawBoxSolid(bgRect, new Color(0.08f, 0.08f, 0.1f, 0.65f));
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            Widgets.DrawBox(bgRect);
            GUI.color = Color.white;

            Rect iconRect = new Rect(bgRect.x + pad, bgRect.y + (bgRect.height - iconSize) / 2f, iconSize, iconSize);
            GUI.DrawTexture(iconRect, ThingDefOf.Silver.uiIcon);

            GUI.color = silverColor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(iconRect.xMax + 3f * scale, bgRect.y, textSize.x + 2f, bgRect.height), text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }
    }
}
