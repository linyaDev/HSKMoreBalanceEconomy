using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    // Виджет прогресса техуровня по мнению Ignorance Is Bliss — в стиле виджетов
    // еды/богатства: перетаскиваемая плашка (ПКМ), позиция в настройках мода,
    // ЛКМ открывает исследования, тултип — разбор по уровням.
    // Прогресс считается точно по алгоритму IiB в режиме «процент исследований»:
    // счётчик НАКОПИТЕЛЬНЫЙ сверху вниз — законченные проекты уровней ВЫШЕ
    // целевого тоже идут в зачёт. Рисуемся после ResourceReadout, чтобы плашка
    // перекрывала надписи на карте, а не наоборот.
    [StaticConstructorOnStartup]
    public static class TechProgressWidget
    {
        private static readonly Color Green = new Color(0.55f, 0.78f, 0.45f);
        private static readonly Color BarBg = new Color(0.25f, 0.25f, 0.25f, 0.8f);
        private static readonly Color BgColor = new Color(0.08f, 0.08f, 0.08f, 0.7f);

        private const float Width = 175f;
        private const float Height = 36f;
        private const int RecalcInterval = 250;

        private static FieldInfo settingsField;      // SettingsHelper.LatestVersion
        private static FieldInfo percentField;       // PercentResearchNeeded
        private static FieldInfo usePercentField;    // UsePercentResearched
        private static FieldInfo useFixedField;      // UseFixedTechRange

        private static bool dragging;
        private static Vector2 dragOffset;

        private static int lastCalcTick = -99999;
        private static bool valid;
        private static string cachedLabel;
        private static string cachedTooltip;
        private static float cachedFraction;

        static TechProgressWidget()
        {
            if (!IgnoranceCompat.Active)
                return;

            var helper = AccessTools.TypeByName("DIgnoranceIsBliss.SettingsHelper");
            settingsField = helper == null ? null : AccessTools.Field(helper, "LatestVersion");
            var settingsType = settingsField?.FieldType;
            if (settingsType != null)
            {
                percentField = AccessTools.Field(settingsType, "PercentResearchNeeded");
                usePercentField = AccessTools.Field(settingsType, "UsePercentResearched");
                useFixedField = AccessTools.Field(settingsType, "UseFixedTechRange");
            }

            var readout = AccessTools.Method(typeof(ResourceReadout), nameof(ResourceReadout.ResourceReadoutOnGUI));
            if (readout == null)
            {
                Log.Warning("[HSKMoreHardcore] TechProgressWidget: ResourceReadout.ResourceReadoutOnGUI not found.");
                return;
            }

            new Harmony("linya.hskmorehardcore.techprogresswidget").Patch(readout,
                postfix: new HarmonyMethod(typeof(TechProgressWidget), nameof(DrawOverlay)));
        }

        public static void DrawOverlay()
        {
            if (Current.ProgramState != ProgramState.Playing)
                return;
            var settings = HSKMoreHardcoreMod.Settings;
            if (settings == null || !settings.showTechProgress)
                return;

            Recalc();
            if (!valid)
                return;

            float posX = settings.techWidgetX;
            float posY = settings.techWidgetY;
            if (posX < 0f)
            {
                posX = 200f;
                posY = 240f;
                settings.techWidgetX = posX;
                settings.techWidgetY = posY;
            }

            var widgetRect = new Rect(posX, posY, Width, Height);
            var evt = Event.current;
            var evtType = evt.type;

            // ПКМ — перетаскивание
            if (evtType == EventType.MouseDown && evt.button == 1 && Mouse.IsOver(widgetRect))
            {
                dragging = true;
                dragOffset = evt.mousePosition - new Vector2(posX, posY);
                evt.Use();
                return;
            }

            if (dragging)
            {
                if (evtType == EventType.MouseDrag || evtType == EventType.MouseMove)
                {
                    var newPos = evt.mousePosition - dragOffset;
                    settings.techWidgetX = Mathf.Clamp(newPos.x, 0f, UI.screenWidth - Width);
                    settings.techWidgetY = Mathf.Clamp(newPos.y, 0f, UI.screenHeight - Height);
                    return;
                }
                if (evtType == EventType.MouseUp && evt.button == 1)
                {
                    dragging = false;
                    settings.Write();
                    evt.Use();
                    return;
                }
            }

            if (evtType != EventType.Repaint)
            {
                // ЛКМ — открыть исследования
                if (evtType == EventType.MouseDown && evt.button == 0 && Mouse.IsOver(widgetRect))
                {
                    Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
                    evt.Use();
                }
                return;
            }

            // === только Repaint ===
            Widgets.DrawBoxSolid(widgetRect, BgColor);

            GUI.color = Green;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(posX, posY + 2f, Width, 20f), cachedLabel);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            // Прогресс-бар до порога следующего уровня
            var barRect = new Rect(posX + 8f, posY + 25f, Width - 16f, 6f);
            Widgets.DrawBoxSolid(barRect, BarBg);
            var fillRect = barRect;
            fillRect.width *= Mathf.Clamp01(cachedFraction);
            Widgets.DrawBoxSolid(fillRect, Green);

            if (Mouse.IsOver(widgetRect))
            {
                Widgets.DrawHighlight(widgetRect);
                TooltipHandler.TipRegion(widgetRect, cachedTooltip);
            }
        }

        private static void Recalc()
        {
            int tick = Find.TickManager.TicksGame;
            if (tick - lastCalcTick < RecalcInterval)
                return;
            lastCalcTick = tick;
            valid = false;

            object iibSettings = settingsField?.GetValue(null);
            if (iibSettings == null)
                return;
            if (useFixedField != null && (bool)useFixedField.GetValue(iibSettings))
                return; // фиксированный диапазон — уровень игрока не считается
            if (usePercentField != null && !(bool)usePercentField.GetValue(iibSettings))
                return; // другой режим расчёта IiB — виджет не про него
            float threshold = percentField != null ? (float)percentField.GetValue(iibSettings) : 0.75f;

            // Группировка проектов по уровням (как strataDic в IiB)
            var totals = new Dictionary<TechLevel, int>();
            var finished = new Dictionary<TechLevel, int>();
            foreach (var proj in DefDatabase<ResearchProjectDef>.AllDefsListForReading)
            {
                totals.TryGetValue(proj.techLevel, out int t);
                totals[proj.techLevel] = t + 1;
                if (proj.IsFinished)
                {
                    finished.TryGetValue(proj.techLevel, out int fin);
                    finished[proj.techLevel] = fin + 1;
                }
            }

            // Алгоритм IiB: сверху вниз, счётчик накопительный
            var current = (TechLevel)1;
            int cum = 0;
            var cumAt = new Dictionary<TechLevel, int>();
            for (int lvl = 7; lvl >= 1; lvl--)
            {
                var tl = (TechLevel)lvl;
                if (!totals.ContainsKey(tl))
                    continue;
                finished.TryGetValue(tl, out int fin);
                cum += fin;
                cumAt[tl] = cum;
                if (current == (TechLevel)1 && (float)cum / totals[tl] >= threshold)
                    current = tl;
            }

            // Следующий уровень: ближайший выше текущего с проектами
            var next = TechLevel.Undefined;
            for (int lvl = (int)current + 1; lvl <= 7; lvl++)
            {
                if (totals.ContainsKey((TechLevel)lvl))
                {
                    next = (TechLevel)lvl;
                    break;
                }
            }
            if (next == TechLevel.Undefined)
                return; // уже максимум

            int have = cumAt.TryGetValue(next, out int c) ? c : 0;
            int need = Mathf.CeilToInt(threshold * totals[next]);
            cachedFraction = need > 0 ? (float)have / need : 1f;
            // Подпись: ТЕКУЩИЙ уровень, цифры — прогресс до следующего
            cachedLabel = "HMH_TechWidget_Label".Translate(
                current.ToStringHuman().CapitalizeFirst(), have, need);
            valid = true;

            var sb = new StringBuilder();
            sb.AppendLine("HMH_TechWidget_TipHeader".Translate(
                current.ToStringHuman().CapitalizeFirst(),
                Mathf.RoundToInt(threshold * 100f)));
            sb.AppendLine();
            for (int lvl = 2; lvl <= 7; lvl++)
            {
                var tl = (TechLevel)lvl;
                if (!totals.ContainsKey(tl))
                    continue;
                finished.TryGetValue(tl, out int fin);
                string line = $"  {tl.ToStringHuman().CapitalizeFirst()}: {fin}/{totals[tl]}";
                if (tl == next)
                {
                    int needLvl = Mathf.CeilToInt(threshold * totals[tl]);
                    int cumLvl = cumAt.TryGetValue(tl, out int cc) ? cc : 0;
                    line += "  (" + "HMH_TechWidget_TipCum".Translate(cumLvl, needLvl) + ")";
                }
                sb.AppendLine(line);
            }
            sb.AppendLine();
            sb.Append("HMH_TechWidget_TipNote".Translate());
            cachedTooltip = sb.ToString();
        }
    }
}
