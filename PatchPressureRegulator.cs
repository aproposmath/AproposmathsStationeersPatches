using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

using Assets.Scripts.Atmospherics;

[HarmonyPatch]
public static class PatchPressureRegulator
{
    static readonly MethodInfo VolumeGetter =
        AccessTools.PropertyGetter(typeof(Atmosphere), nameof(Atmosphere.Volume));

    static readonly MethodInfo GetGasVolumeMethod =
        AccessTools.Method(typeof(Atmosphere), nameof(Atmosphere.GetGasVolume));
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(
            typeof(AtmosphereHelper),
            nameof(AtmosphereHelper.MoveRegulatedGas));

        yield return AccessTools.Method(
            typeof(AtmosphereHelper),
            nameof(AtmosphereHelper.MaxMolesPerTick));
    }

    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(VolumeGetter))
                instruction.operand = GetGasVolumeMethod;
            yield return instruction;
        }
    }
}