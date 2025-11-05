using Assets.Scripts.Networks;
using Assets.Scripts.Objects.Motherboards;
using HarmonyLib;

namespace AproposmathsStationeersPatches
{
    // Patches LogicType StackSize in IC Code
    [HarmonyPatch]
    static class PatchStackSize
    {
        [HarmonyPatch(
            typeof(CableNetwork),
            nameof(CableNetwork.GetLogicValue),
            new[] { typeof(LogicType) }
        )]
        static bool Prefix(ref LogicType logicType, CableNetwork __instance, ref double __result)
        {
            if (logicType == LogicType.StackSize)
            {
                __result = __instance.GetStackSize();
                return false;
            }
            return true;
        }

        [HarmonyPatch(
            typeof(CableNetwork),
            nameof(CableNetwork.CanLogicRead),
            new[] { typeof(LogicType) }
        )]
        static bool Prefix(ref LogicType logicType, CableNetwork __instance, ref bool __result)
        {
            if (logicType == LogicType.StackSize)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
