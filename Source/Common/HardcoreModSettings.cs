using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    // Игровые настройки мода (Options -> Mod settings -> HSK More Balance).
    // Здесь только переключатели поведения; числовые константы баланса живут
    // в Defs/Misc/HardcoreSettings.xml.
    public class HardcoreModSettings : ModSettings
    {
        // Виджет прогресса техуровня по Ignorance Is Bliss (TechProgressWidget).
        // По умолчанию выключен — включается в настройках мода.
        public bool showTechProgress = false;
        public float techWidgetX = -1f;
        public float techWidgetY = -1f;

        // Серебро над гостями Hospitality (GuestSilverOverlay). Включено по
        // умолчанию, выключается здесь.
        public bool showGuestSilver = true;

        // Порог зума для метки серебра: пикселей на клетку, ниже — метка
        // скрыта. 0 = показывать при любом зуме.
        public float guestSilverZoom = 24f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showTechProgress, "showTechProgress", false);
            Scribe_Values.Look(ref techWidgetX, "techWidgetX", -1f);
            Scribe_Values.Look(ref techWidgetY, "techWidgetY", -1f);
            Scribe_Values.Look(ref showGuestSilver, "showGuestSilver", true);
            Scribe_Values.Look(ref guestSilverZoom, "guestSilverZoom", 24f);
        }
    }

    public class HSKMoreHardcoreMod : Mod
    {
        public static HardcoreModSettings Settings { get; private set; }

        public HSKMoreHardcoreMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<HardcoreModSettings>();
        }

        public override string SettingsCategory() => "HSK More Balance";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);
            list.CheckboxLabeled("HMH_ShowTechWidget".Translate(),
                ref Settings.showTechProgress,
                "HMH_ShowTechWidgetTip".Translate());

            // --- Гости ---
            list.GapLine();
            Text.Font = GameFont.Medium;
            list.Label("HMH_GuestsSection".Translate());
            Text.Font = GameFont.Small;
            list.CheckboxLabeled("HMH_ShowGuestSilver".Translate(),
                ref Settings.showGuestSilver,
                "HMH_ShowGuestSilverTip".Translate());
            if (Settings.showGuestSilver)
            {
                list.Label("HMH_GuestSilverZoom".Translate(Settings.guestSilverZoom.ToString("0")),
                    tooltip: "HMH_GuestSilverZoomTip".Translate());
                Settings.guestSilverZoom = Mathf.Round(list.Slider(Settings.guestSilverZoom, 0f, 60f));
            }
            list.End();
        }
    }
}
