using HarmonyLib;
using UnityEngine;
using Verse;

namespace HSKMoreHardcore
{
    [StaticConstructorOnStartup]
    public static class DeepResourceNerf
    {
        private static float mult;

        static DeepResourceNerf()
        {
            var settings = HardcoreSettingsDef.Instance;
            mult = settings?.deepResourceMultiplier ?? 0.143f;

            if (Mathf.Approximately(mult, 1f))
                return;

            var harmony = new Harmony("linya.hskmorehardcore.deepresource");

            var setAt = AccessTools.Method(typeof(DeepResourceGrid), "SetAt");
            if (setAt != null)
            {
                harmony.Patch(setAt,
                    prefix: new HarmonyMethod(typeof(DeepResourceNerf), nameof(SetAtPrefix)));
                Log.Message($"[HSKMoreHardcore] DeepResourceNerf: x{mult} patch on SetAt applied.");
            }
        }

        public static void SetAtPrefix(ref int count)
        {
            if (count > 0)
            {
                count = Mathf.Max(1, Mathf.RoundToInt(count * mult));
            }
        }
    }
}
