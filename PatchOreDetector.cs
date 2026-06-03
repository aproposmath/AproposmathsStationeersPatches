using HarmonyLib;
using UnityEngine;

using Objects.Items;
using TerrainSystem;
using Assets.Scripts;

namespace AproposmathsStationeersPatches
{
    // Show angle distance in lights instead of actual distance
    // the beeps are unchanged
    static class PatchOreDetector
    {
        [HarmonyPatch(typeof(OreDetector)), HarmonyPatch(nameof(OreDetector.UpdateSignalStrength)), HarmonyPrefix]
        private static bool PrefixUpdateSignalStrength(OreDetector __instance)
        {
            var human = __instance.RootParentHuman;
            if (human == null) return true;
            var range = __instance._range;
            Vein nearestVeinOfType = Vein.GetNearestVeinOfType(__instance.transform.position, range, __instance.TrackedMinableType);
            if (nearestVeinOfType == null)
            {
                __instance.ResetSignalStrength();
                __instance._audioPitch = OreDetector.MinAudioPitch;
                return false;
            }
            var pos = Vein.GetClosestMinablePosition(__instance.transform.position, nearestVeinOfType);
            float num = Vector3.Distance(__instance.transform.position, pos);

            if (num > range)
            {
                __instance.ResetSignalStrength();
                __instance._audioPitch = OreDetector.MinAudioPitch;
            }
            else
            {
                __instance._audioPitch = Mathf.Lerp(OreDetector.MinAudioPitch, OreDetector.MaxAudioPitch, Mathf.Max(range - num, 0f) / range);

                var forward = CameraController.CurrentCamera.transform.forward;
                var position = human.HeadBone.position;
                Vector3 toVein = (pos - position).normalized;
                float angle = Vector3.Angle(forward, toVein);
                float angDist = Mathf.Clamp(angle / 90.0f, 0.0f, 1.0f);
                float newNum = range * angDist;
                __instance.UpdateMaterials(newNum);
            }
            return false; // Skip original method
        }
    }
}
