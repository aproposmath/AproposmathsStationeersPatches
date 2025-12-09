using System.Text.RegularExpressions;
using Assets.Scripts.Objects.Electrical;
using HarmonyLib;

using Op = Assets.Scripts.Objects.Electrical.ProgrammableChip._Operation;

namespace AproposmathsStationeersPatches
{
    // Patches instructions like
    // s rr0 Seting 123
    // define MyValue 456
    // l MyValue Setting

    static class PatchSetLoadIndirectOrDefine
    {
        [HarmonyPatch(
            typeof(ProgrammableChip._Operation),
            nameof(ProgrammableChip._Operation._MakeDeviceVariable)
        )]
        static bool Prefix(ProgrammableChip chip, ref int lineNumber, ref string deviceCode, ref Op.IDeviceVariable __result)
        {
            // Also match rr0 to rr15 etc. for l/s commands
            if (Regex.IsMatch(deviceCode, "^r+(?:1[0-5]|[0-9])$"))
            {
                L.Info($"Matched register alias: {deviceCode}");
                __result = new Op.DirectDeviceVariable(chip, lineNumber, deviceCode, InstructionInclude.MaskDoubleValue | InstructionInclude.DeviceIndex, throwException: false);
                return false;
            }

            // If the deviceCode matches a defined value, create a DirectDeviceVariable for it
            if (chip._Defines.TryGetValue(deviceCode, out var defineValue))
            {
                L.Info($"Matched defined alias: {deviceCode} = {defineValue}");
                __result = new Op.DirectDeviceVariable(chip, lineNumber, deviceCode, InstructionInclude.MaskDoubleValue, throwException: false);
                return false;
            }

            // Otherwise, continue with the original method
            return true;
        }
    }
}
