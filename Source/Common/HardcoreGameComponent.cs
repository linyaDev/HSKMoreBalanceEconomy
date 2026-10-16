using System.Collections.Generic;
using Verse;

namespace HSKMoreHardcore
{
    public class HardcoreGameComponent : GameComponent
    {
        private Dictionary<int, int> fishSpawnTimers = new Dictionary<int, int>();

        // Стабильный бросок для лута: сид перегенерируется раз в 3 дня, бросок = f(сид, thingId).
        // Ключ — thingIDNumber экземпляра: каждый предмет получает свой бросок (разнообразие по
        // экземплярам — два одинаковых меча с разных врагов будут разной прочности). id присваивается
        // при генерации пешки и пишется в сейв, поэтому перезагрузка существующего сейва даёт тот же
        // бросок (анти-сейв-скам для лута, который уже есть на карте в момент сохранения).
        private const int StableRollPeriodTicks = 180000; // 3 дня (60000 тиков/день)
        private int stableRollSeed;
        private int stableRollPeriod = int.MinValue;

        public HardcoreGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref fishSpawnTimers, "fishSpawnTimers", LookMode.Value, LookMode.Value);
            if (fishSpawnTimers == null)
                fishSpawnTimers = new Dictionary<int, int>();

            Scribe_Values.Look(ref stableRollSeed, "stableRollSeed", 0);
            Scribe_Values.Look(ref stableRollPeriod, "stableRollPeriod", int.MinValue);
        }

        public override void FinalizeInit()
        {
            // Временная диагностика: прогресс исследований считается только
            // при живой игре, на старте приложения его ещё нет
            TechCountDebug.LogProgress();
        }

        public Dictionary<int, int> FishSpawnTimers => fishSpawnTimers;

        // Детерминированный бросок [0,1) для данного экземпляра (thingId) в текущем 3-дневном окне
        public float StableRoll(int thingId)
        {
            int period = Find.TickManager.TicksGame / StableRollPeriodTicks;
            if (period != stableRollPeriod)
            {
                stableRollPeriod = period;
                stableRollSeed = Rand.Int; // новый сид раз в 3 дня
            }

            int combined = Gen.HashCombineInt(stableRollSeed, thingId);
            Rand.PushState(combined);
            float value = Rand.Value;
            Rand.PopState();
            return value;
        }

        public static HardcoreGameComponent Get()
        {
            return Current.Game?.GetComponent<HardcoreGameComponent>();
        }

        // Стабильный бросок с фолбэком на обычный Rand, если компонент недоступен
        public static float Roll(int thingId)
        {
            var comp = Get();
            return comp != null ? comp.StableRoll(thingId) : Rand.Value;
        }
    }
}
