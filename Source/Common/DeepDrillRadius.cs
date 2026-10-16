using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HSKMoreHardcore
{
    /// <summary>
    /// Буровая установка (ванильный DeepDrill) достаёт жилу в радиусе на клетку
    /// больше: 37 ближайших клеток вместо 21 (дистанция ~3.2 против ~2.2).
    /// Число зашито литералом в цикле DeepDrillUtility.GetNextResource, поэтому
    /// транспайлер. Кольцо у постройки правится XML-патчем в паре с этим кодом:
    /// Patches/Core/DeepDrillRadius.xml (specialDisplayRadius 2.6 -> 3.6).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DeepDrillRadius
    {
        public const int VanillaCells = 21;
        public const int NewCells = 37;

        static DeepDrillRadius()
        {
            var target = AccessTools.Method(typeof(DeepDrillUtility), nameof(DeepDrillUtility.GetNextResource),
                new[]
                {
                    typeof(IntVec3), typeof(Map),
                    typeof(ThingDef).MakeByRefType(), typeof(int).MakeByRefType(), typeof(IntVec3).MakeByRefType()
                });
            if (target == null)
            {
                Log.Error("[HSKMoreHardcore] DeepDrillRadius: DeepDrillUtility.GetNextResource not found.");
                return;
            }

            new Harmony("linya.hskmorehardcore.deepdrillradius").Patch(target,
                transpiler: new HarmonyMethod(typeof(DeepDrillRadius), nameof(Transpiler)));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool patched = false;
            foreach (var ci in instructions)
            {
                if (ci.opcode == OpCodes.Ldc_I4_S && ci.OperandIs(VanillaCells))
                {
                    ci.operand = (sbyte)NewCells;
                    patched = true;
                }
                yield return ci;
            }

            if (patched)
                Log.Message($"[HSKMoreHardcore] DeepDrillRadius: буровая сканирует {NewCells} клеток вместо {VanillaCells}.");
            else
                Log.Warning("[HSKMoreHardcore] DeepDrillRadius: константа 21 не найдена — радиус не изменён.");
        }
    }
}
