using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    public class HardcoreSettingsDef : Def
    {
        public float ammoCraftCostMultiplier = 5f;
        public float arrowCraftCostMultiplier = 1f;
        public float boltCraftCostMultiplier = 2f;
        public float deepResourceMultiplier = 0.143f; // ~1/7
        public List<string> ammoCraftExcludedMaterials;
        public List<string> ammoCraftExcludedRecipes;
        public int traderSilverMinimum = 3500;
        // Множители цены продажи игроком. Сначала точечные по defName, потом
        // список правил по категориям / статам (первое подходящее).
        public Dictionary<string, float> sellPriceOverrides;
        public List<PriceRule> sellPriceRules;
        // То же самое для цены покупки у торговца. Списки независимы:
        // товар может быть дешевле только при продаже, только при покупке
        // или в обе стороны.
        public Dictionary<string, float> buyPriceOverrides;
        public List<PriceRule> buyPriceRules;
        // Животные с меткой CompFreeAnimal продаются с этими множителями:
        // пришедшие событием и прирученные вручную. 1 = без изменений.
        // Рождённые в колонии метки не получают и продаются по полной цене.
        public float freeAnimalSellMultiplier = 1f;
        public float tamedAnimalSellMultiplier = 1f;
        // Стальной дождь: пауза между письмом и первыми осколками, в тиках
        // (60 тиков = 1 секунда на обычной скорости). 0 = без задержки.
        public int razorRainStartDelayTicks = 0;
        // Время полёта метеорита Core_SK (SK.Events.MeteorIncoming) до удара, в тиках.
        // Паковое значение 220~300; пустой диапазон оставляет как есть.
        public IntRange meteorTicksToImpact = default(IntRange);
        // Множитель восстановления нефтяных залежей Rimefeller поверх его настроек.
        // 0 = залежи не восстанавливаются, 1 = как в моде.
        public float oilFieldRegenMultiplier = 1f;
        // Гейты событий (техуровень для событий, форм жуткого присоединившегося и
        // гостей) живут в отдельном моде HSK More Balance: Quests and Events.
        // Фактическое значение берётся из Defs/Misc/HardcoreSettings.xml; здесь только фолбэк.
        public float treeRegrowthChance = 0.25f;
        // Срок жизни деревьев = growDays * это значение (ваниль 8; 0 = не менять)
        public float treeLifespanDaysPerGrowDays = 0f;
        // Гости Hospitality: множитель выданного им серебра по техуровню фракции (нет строки — без изменений)
        public Dictionary<TechLevel, float> guestSilverMultiplierByTech;
        // Платный вызов каравана с консоли связи у нейтральной фракции.
        // 0 серебра = строка не добавляется.
        public int commsTraderSilverCost = 0;
        public int commsTraderGoodwillCost = 15;
        // Заряды общие на все фракции; каждый восстанавливается за cooldownTicks
        public int commsTraderMaxCharges = 2;
        public int commsTraderCooldownTicks = 3600000; // год
        public int commsTraderArrivalDelayTicks = 120000; // как в ванильном запросе — 2 дня
        // Фракции выше этого техуровня строку не получают (Undefined = без ограничения)
        public TechLevel commsTraderMaxTechLevel = TechLevel.Undefined;
        public List<string> commsTraderExcludedFactions;
        // Разбор оружия: ствол целее weaponPartSalvageMinHitPct отдаёт
        // weaponPartSalvageFraction своих деталей, потрёпанный — ничего.
        // Доля 0 = детали не выпадают никогда. Что не уцелело, даёт прутки.
        public float weaponPartSalvageMinHitPct = 0.5f;
        public float weaponPartSalvageFraction = 0f;
        public int weaponPartSmeltSteel = 5;
        // Доля прочих продуктов разбора (металл из costList). Патроны, которые
        // дописывает Combat Extended из магазина, не режутся. 1 = как в паке.
        public float weaponSmeltOtherFraction = 1f;

        private static HardcoreSettingsDef cachedInstance;

        public static HardcoreSettingsDef Instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = DefDatabase<HardcoreSettingsDef>.GetNamedSilentFail("HSK_HardcoreSettings");
                return cachedInstance;
            }
        }
    }
}
