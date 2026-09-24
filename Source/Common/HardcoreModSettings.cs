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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showTechProgress, "showTechProgress", false);
            Scribe_Values.Look(ref techWidgetX, "techWidgetX", -1f);
            Scribe_Values.Look(ref techWidgetY, "techWidgetY", -1f);
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
            list.End();
        }
    }
}
