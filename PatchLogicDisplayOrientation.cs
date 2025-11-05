namespace AproposmathsStationeersPatches
{
    using Assets.Scripts.Objects.Electrical;
    using Assets.Scripts;
    using HarmonyLib;
    using UnityEngine;

    // Flips Logic Display digits when mounted upside down
    static class PatchLogicDisplayOrientation
    {
        [HarmonyPatch(typeof(LogicDisplay)), HarmonyPatch(nameof(LogicDisplay.SetDisplay)), HarmonyPostfix]
        static void LogicDisplay_SetDisplay_Postfix(LogicDisplay __instance)
        {
            var yRotated = __instance.Transform.localRotation * new Vector3(0, 1, 0);
            if (yRotated.y < -0.5f)
            {
                // this formula was deduced by concicely analyzing the mathematics of the position calculation and unity coordinate systems (i.e. hours of trial and error)
                __instance.DigitTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                var x = __instance.DigitTransform.localPosition.x;
                var num2 = (__instance._maxPixels - x / GameManager.DigitPixel) * 2 + 1.0f;
                var xNew = (float)Mathf.FloorToInt((float)(num2) * 0.5f) * GameManager.DigitPixel;
                __instance.DigitTransform.localPosition = new Vector3(xNew, 0, 0);
            }

        }
    }
}
