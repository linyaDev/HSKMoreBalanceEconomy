using System.Collections.Generic;

namespace HSKMoreHardcore
{
    public static class NerfSettings
    {
        public static float ammoDropMultiplier = 0.25f;
        public static float medicineDropMultiplier = 0.1f;

        // Away from home map (quest sites, etc.)
        public static float ammoDropMultiplierAway = 0.04f;
        public static float medicineDropMultiplierAway = 0.05f;
        public static float weaponHpMultiplierHomeMin = 0.15f; // floor at large tech gap (home maps)
        public static float weaponGapPower = 2f;               // roll-curve power per tier of weapon tech gap
        public static float weaponHpMultiplierAway = 0.05f;    // flat on away maps

        // Trader ammo
        public static float traderAmmoMultiplier = 1.0f;
        public static float rewardAmmoMultiplier = 0.25f;
        public static float drugDropMultiplier = 0.33f;
        public static float podDrugMultiplier = 0.5f;
        public static float ammoPriceMultiplier = 4f;
        public static float weaponPriceMultiplier = 2f;

        // Banned materials in pods
        public static HashSet<string> bannedPodMaterials = new HashSet<string>
        {
            "AlphaPoly"
        };

        // Remove all weapons from pod/crate loot
        public static bool removePodWeapons = true;

        // Market value multipliers for marked apparel (CompWornByEnemy)
        public static float wornByEnemyValueMult = 0.3f; // снято с врага в бою -> 30% цены
        public static float wornValueMult = 0.6f;        // носил наш колонист -> 60% цены
        public static float washedValueMult = 0.8f;      // постирано -> 80% цены

        // Ремонт (хук на Mend & Recycle). «Починено» — независимая метка-счётчик.
        public static int repairBlockThreshold = 2;       // обычная починка запрещена при repairCount >= этого
        public static float repairValueMult = 0.9f;       // множитель цены за каждую починку (мультипликативно: 0.9^repairCount)

        // Quality-additive offsets for apparel equipped stats.
        // Added to the apparel's equipped offset: delta = dir * (qualityIndex - baseline) * step,
        // where dir = +1 (higher is better) or -1 (lower is better). QualityCategory index: Awful=0..Legendary=6.
        // Baseline range (no stat effect): Normal..Good. Below it -> negative tiers, above -> positive.
        public static int apparelQualityBaselineLow = 2;  // Normal
        public static int apparelQualityBaselineHigh = 3; // Good
        public static Dictionary<string, float> apparelQualityStatStep = new Dictionary<string, float>
        {
            { "MoveSpeed", 0.01f },
            { "WorkSpeedGlobal", 0.01f },
            { "MentalBreakThreshold", 0.01f },
        };
        public static HashSet<string> apparelQualityLowerIsBetter = new HashSet<string>
        {
            "MentalBreakThreshold",
        };

        // Armor (apparel) HP nerf on loot — randomized like the weapon nerf, but the curve power
        // scales with the "tech gap" between the armor and the player's dev level.
        // The armor's tech tier is derived from its Combat Extended armor rating (Sharp/Blunt),
        // because the modpack's techLevel tags are unreliable.
        // TechLevel enum: Undefined=0, Animal=1, Neolithic=2, Medieval=3, Industrial=4, Spacer=5, Ultra=6, Archotech=7
        public static bool armorHpNerfEnabled = true;
        public static float armorHpMin = 0.15f;          // floor multiplier (best-case durability at huge gap)
        // Manual Sharp thresholds -> armor tech tier (below Medieval = Neolithic=2).
        // Armor's tier is a step function of its CE Sharp rating (techLevel tags are unreliable here).
        public static float armorSharpMedieval = 3f;     // Sharp >= 3  -> Medieval (3)
        public static float armorSharpIndustrial = 8f;   // Sharp >= 8  -> Industrial (4)
        public static float armorSharpSpacer = 16f;      // Sharp >= 16 -> Spacer (5)
        public static float armorSharpUltra = 26f;       // Sharp >= 26 -> Ultra (6)
        public static float armorGapPower = 2f;          // roll-curve power per tier of tech gap
        // Manual tier overrides by defName (for items whose Sharp doesn't reflect their tech, e.g. shield belts).
        // TechLevel: Neolithic=2, Medieval=3, Industrial=4, Spacer=5, Ultra=6, Archotech=7.
        public static Dictionary<string, int> armorTierOverrides = new Dictionary<string, int>
        {
            { "Apparel_ShieldBelt", 5 }, // Spacer — нет Sharp-брони, задаём тир вручную
        };

        // Pod/crate ammo
        public static float podAmmoMultiplier = 0.25f;

        // Fishing
        public static int maxFishingPiers = 3;
        public static int maxFishTraps = 3;
        public static float fishSpawnSlowdown = 1f; // множитель замедления пополнения рыбы

        // Ammo craft
        public static float ammoCraftCostMultiplier = 3f;
    }
}
