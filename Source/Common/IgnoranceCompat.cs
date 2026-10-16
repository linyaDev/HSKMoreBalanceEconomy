using System;
using System.Reflection;
using RimWorld;
using HarmonyLib;
using Verse;

namespace HSKMoreHardcore
{
    // Мягкая связка с Ignorance Is Bliss (dame.ignorance). Читаем его расчётный
    // техуровень игрока и фильтры допуска — для наших патчей событий.
    // Без ссылки на его DLL: тип ищется по имени один раз при старте, геттеры
    // и методы оборачиваются в делегаты (после инициализации вызов по цене
    // обычного метода). Если мод не установлен или его API поменялось —
    // Active=false и работают фолбэки.
    [StaticConstructorOnStartup]
    public static class IgnoranceCompat
    {
        private static readonly Func<TechLevel> getPlayerTech;
        private static readonly Func<TechLevel, bool> techIsEligible;
        private static readonly Func<Faction, bool> factionIsEligible;

        /// <summary>Ignorance Is Bliss установлен и его API успешно подцеплен.</summary>
        public static bool Active { get; }

        static IgnoranceCompat()
        {
            var t = AccessTools.TypeByName("DIgnoranceIsBliss.Core_Patches.IgnoranceBase");
            if (t == null)
                return;

            getPlayerTech = CreateDelegate<Func<TechLevel>>(AccessTools.PropertyGetter(t, "PlayerTechLevel"));
            techIsEligible = CreateDelegate<Func<TechLevel, bool>>(AccessTools.Method(t, "TechIsEligibleForIncident"));
            factionIsEligible = CreateDelegate<Func<Faction, bool>>(AccessTools.Method(t, "FactionInEligibleTechRange"));

            Active = getPlayerTech != null && techIsEligible != null && factionIsEligible != null;
            if (!Active)
                Log.Warning("[HSKMoreHardcore] IgnoranceCompat: Ignorance Is Bliss найден, но его API изменилось — связка отключена.");
        }

        /// <summary>
        /// Расчётный техуровень игрока по версии Ignorance Is Bliss (по проценту
        /// исследований и т.п., смотря что выбрано в его настройках). Без мода —
        /// ванильный уровень фракции игрока; вне игры — Undefined.
        /// </summary>
        public static TechLevel PlayerTechLevel
        {
            get
            {
                if (Current.Game == null)
                    return TechLevel.Undefined;
                if (Active)
                    return getPlayerTech();
                return Faction.OfPlayer.def.techLevel;
            }
        }

        /// <summary>
        /// Пропустил бы Ignorance Is Bliss событие/фракцию этого техуровня.
        /// Без мода ограничений нет — всегда true.
        /// </summary>
        public static bool TechIsEligible(TechLevel tech)
        {
            return !Active || Current.Game == null || techIsEligible(tech);
        }

        /// <summary>
        /// Пропустил бы Ignorance Is Bliss эту фракцию (учитывает его исключения
        /// для Империи и механоидов). Без мода — всегда true.
        /// </summary>
        public static bool FactionIsEligible(Faction faction)
        {
            if (faction == null)
                return true;
            return !Active || Current.Game == null || factionIsEligible(faction);
        }

        private static T CreateDelegate<T>(MethodInfo method) where T : Delegate
        {
            if (method == null)
                return null;
            try
            {
                return (T)method.CreateDelegate(typeof(T));
            }
            catch (ArgumentException)
            {
                // сигнатура не совпала — API мода изменилось
                return null;
            }
        }
    }
}
