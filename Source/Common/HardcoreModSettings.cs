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

        // Множитель стоимости крафта патронов. Пока ammoCraftOverride выключен,
        // действует значение из Defs/Misc/HardcoreSettings.xml.
        public bool ammoCraftOverride = false;
        public float ammoCraftMultiplier = 3f;

        // Резать ли патроны, выпадающие из инвентаря врагов
        // (EnemyLootNerf: четверть на своей карте, 4% на чужой).
        public bool nerfEnemyAmmoDrop = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showTechProgress, "showTechProgress", false);
            Scribe_Values.Look(ref techWidgetX, "techWidgetX", -1f);
            Scribe_Values.Look(ref techWidgetY, "techWidgetY", -1f);
            Scribe_Values.Look(ref showGuestSilver, "showGuestSilver", true);
            Scribe_Values.Look(ref guestSilverZoom, "guestSilverZoom", 24f);
            Scribe_Values.Look(ref ammoCraftOverride, "ammoCraftOverride", false);
            Scribe_Values.Look(ref ammoCraftMultiplier, "ammoCraftMultiplier", 3f);
            Scribe_Values.Look(ref nerfEnemyAmmoDrop, "nerfEnemyAmmoDrop", true);
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

            // --- Боеприпасы ---
            list.GapLine();
            Text.Font = GameFont.Medium;
            list.Label("HMH_AmmoSection".Translate());
            Text.Font = GameFont.Small;

            bool wasOverride = Settings.ammoCraftOverride;
            float wasMult = Settings.ammoCraftMultiplier;

            list.CheckboxLabeled(
                "HMH_AmmoCraftOverride".Translate(AmmoCraftNerf.DefaultAmmoMultiplier.ToString("0.##")),
                ref Settings.ammoCraftOverride,
                "HMH_AmmoCraftOverrideTip".Translate());

            // Только что включили — слайдер встаёт на действующее значение из
            // дефа, чтобы галочка сама по себе ничего не меняла
            if (Settings.ammoCraftOverride && !wasOverride)
                Settings.ammoCraftMultiplier = AmmoCraftNerf.DefaultAmmoMultiplier;

            if (Settings.ammoCraftOverride)
            {
                list.Label("HMH_AmmoCraftMultiplier".Translate(Settings.ammoCraftMultiplier.ToString("0.##")),
                    tooltip: "HMH_AmmoCraftMultiplierTip".Translate());
                Settings.ammoCraftMultiplier =
                    Mathf.Round(list.Slider(Settings.ammoCraftMultiplier, 0.5f, 6f) * 2f) / 2f;
            }

            // Пересчитываем рецепты сразу, как только значение поменялось:
            // AmmoCraftNerf считает от запомненных исходных количеств.
            if (wasOverride != Settings.ammoCraftOverride
                || !Mathf.Approximately(wasMult, Settings.ammoCraftMultiplier))
            {
                AmmoCraftNerf.Apply();
            }

            list.CheckboxLabeled("HMH_NerfEnemyAmmoDrop".Translate(),
                ref Settings.nerfEnemyAmmoDrop,
                "HMH_NerfEnemyAmmoDropTip".Translate());

            list.End();
        }
    }
}
