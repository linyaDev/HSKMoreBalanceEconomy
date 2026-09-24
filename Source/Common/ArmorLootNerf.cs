using RimWorld;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    // Нерф прочности брони в луте: рандомный, как у оружия, но крутизна кривой растёт с «разрывом технологий»
    // между бронёй и нашим уровнем развития.
    //
    // Уровень развития игрока берём из Faction.def.techLevel (его двигает мод Tech Advancing).
    // Значения enum TechLevel: Undefined=0, Animal=1, Neolithic=2, Medieval=3, Industrial=4, Spacer=5, Ultra=6, Archotech=7.
    //
    // ВАЖНО: техуровень самой брони НЕ берём из def.techLevel/тегов (в модпаке они битые),
    // а выводим из её защиты по шкале Combat Extended (Sharp/Blunt).
    public static class ArmorLootNerf
    {
        // Полный разбор расчёта (для логов)
        public struct Calc
        {
            public float sharp;       // CE Sharp брони
            public int armorTier;     // техуровень брони, выведенный из Sharp по порогам
            public int dev;           // наш уровень развития
            public int gap;           // armorTier - dev
            public float power;       // степень кривой рандома (0 если нерфа нет)
            public float roll;        // Rand.Value (NaN если нерфа нет)
            public float mult;        // итоговый множитель прочности
        }

        // Текущий уровень развития игрока
        public static int PlayerDevLevel()
        {
            var f = Faction.OfPlayerSilentFail;
            if (f?.def == null)
                return 0;
            return (int)f.def.techLevel;
        }

        // Техуровень брони по порогам Sharp (только Sharp). Ниже первого порога — Neolithic (2).
        public static int ArmorTierFromSharp(float sharp)
        {
            if (sharp >= NerfSettings.armorSharpUltra) return 6;       // Ultra
            if (sharp >= NerfSettings.armorSharpSpacer) return 5;      // Spacer
            if (sharp >= NerfSettings.armorSharpIndustrial) return 4;  // Industrial
            if (sharp >= NerfSettings.armorSharpMedieval) return 3;    // Medieval
            return 2;                                                  // Neolithic
        }

        public static Calc Compute(Apparel ap)
        {
            Calc c = default;
            c.sharp = ap.GetStatValue(StatDefOf.ArmorRating_Sharp);
            // Ручной override тира (напр. пояс-щит) важнее всего; иначе берём максимум из
            // армор-тира (по Sharp) и техуровня дефа (def.techLevel есть у всех вещей через наследование).
            if (NerfSettings.armorTierOverrides.TryGetValue(ap.def.defName, out int overrideTier))
                c.armorTier = overrideTier;
            else
                c.armorTier = Mathf.Max(ArmorTierFromSharp(c.sharp), (int)ap.def.techLevel);

            c.dev = PlayerDevLevel();
            c.gap = c.armorTier - c.dev;

            if (c.gap <= 0)
            {
                // Броня нашего уровня или ниже — не трогаем
                c.power = 0f;
                c.roll = float.NaN;
                c.mult = 1f;
                return c;
            }

            // Чем больше разрыв — тем круче кривая и тем вероятнее низкая прочность (как у оружия)
            c.power = NerfSettings.armorGapPower * c.gap;
            c.roll = HardcoreGameComponent.Roll(ap.thingIDNumber);
            c.mult = NerfSettings.armorHpMin + (1f - NerfSettings.armorHpMin) * Mathf.Pow(c.roll, c.power);
            return c;
        }

        public static float MultiplierFor(Apparel ap) => Compute(ap).mult;

        public static string TierName(int tier)
        {
            if (tier < 0) tier = 0;
            if (tier > 7) tier = 7;
            return ((TechLevel)tier).ToString();
        }

        private static string Breakdown(Calc c)
        {
            string head = $"Sharp={c.sharp:F1} => техур.брони={TierName(c.armorTier)}, развитие={TierName(c.dev)}, разрыв={c.gap}";
            if (c.gap <= 0)
                return head + " (<=0)";
            return head + $", power={c.power:F1}, roll={c.roll:F3}, roll^power={Mathf.Pow(c.roll, c.power):F3} => множитель={c.mult:F2}";
        }

        // Применить нерф к броне (только понижаем прочность). Возвращает true, если что-то изменили.
        public static bool Apply(Apparel ap, string tag)
        {
            if (!NerfSettings.armorHpNerfEnabled || ap == null || ap.HitPoints <= 1)
                return false;

            Calc c = Compute(ap);

            if (c.mult >= 1f)
                return false;

            int before = ap.HitPoints;
            int target = Mathf.Max(1, Mathf.FloorToInt(ap.MaxHitPoints * c.mult));
            if (target >= before)
                return false;

            ap.HitPoints = target;
            return true;
        }
    }
}
