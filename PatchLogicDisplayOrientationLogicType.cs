namespace AproposmathsStationeersPatches;

using HarmonyLib;
using UnityEngine;

using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Serialization;
using System.Collections.Generic;

static class PatchLogicDisplayOrientationLogicType
{
    private static readonly Dictionary<int, int> displayRotationValues = [];

    [HarmonyPatch(typeof(XmlSaveLoad)), HarmonyPatch(nameof(XmlSaveLoad.LoadWorld)), HarmonyPrefix]
    private static void OnLoad()
    {
        displayRotationValues.Clear();
    }

    [HarmonyPatch(typeof(LogicDisplay)), HarmonyPatch(nameof(LogicDisplay.CanLogicRead)), HarmonyPrefix]
    private static bool CanLogicRead(LogicType logicType, ref bool __result)
    {
        if (logicType == LogicType.Orientation)
        {
            __result = true;
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(LogicDisplay)), HarmonyPatch(nameof(LogicDisplay.CanLogicWrite)), HarmonyPrefix]
    private static bool CanLogicWrite(LogicType logicType, ref bool __result)
    {
        if (logicType == LogicType.Orientation)
        {
            __result = true;
            return false;
        }
        return true;
    }

    private static double GetRotationValue(LogicDisplay display)
    {
        if (displayRotationValues.TryGetValue(display.GetInstanceID(), out int value))
            return value;
        return 0;
    }

    private static void SetRotationValue(LogicDisplay display, double value)
    {
        if (display.ScreenUp == null)
            return;

        var oldValue = GetRotationValue(display);
        if (value == oldValue)
            return;

        if (value == 0)
        {
            float num = Vector3.Dot(display.transform.up, Vector3.up);
            float num2 = Vector3.Dot(-display.transform.up, Vector3.up);
            float num3 = Vector3.Dot(display.transform.right, Vector3.up);
            float num4 = Vector3.Dot(-display.transform.right, Vector3.up);
            float num5 = Mathf.Max(num, num2, num3, num4);
            if (num3 != num5 && num4 != num5 && num2 == num5 && oldValue == 1)
                display.ScreenUp.transform.Rotate(Vector3.forward, 180f, Space.Self);

            displayRotationValues.Remove(display.GetInstanceID());
            return;
        }

        if ((Vector3.Dot(display.ScreenUp.transform.up, display.transform.up) > 0.0 ? 1.0 : -1.0) != value)
            display.ScreenUp.transform.Rotate(Vector3.forward, 180f, Space.Self);
        displayRotationValues[display.GetInstanceID()] = (int)value;
    }

    [HarmonyPatch(typeof(LogicDisplay)), HarmonyPatch(nameof(LogicDisplay.SetLogicValue)), HarmonyPrefix]
    private static bool SetLogicValue(LogicDisplay __instance, LogicType logicType, double value)
    {
        if (logicType == LogicType.Orientation)
        {
            SetRotationValue(__instance, value);
            return false;
        }
        return true;

    }

    [HarmonyPatch(typeof(LogicDisplay)), HarmonyPatch(nameof(LogicDisplay.GetLogicValue)), HarmonyPrefix]
    private static bool GetLogicValue(LogicDisplay __instance, LogicType logicType, ref double __result)
    {
        if (logicType == LogicType.Orientation)
        {
            __result = GetRotationValue(__instance);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(LogicUnitBase)), HarmonyPatch(nameof(LogicUnitBase.SerializeSave)), HarmonyPostfix]
    private static void PostfixSerializeSave(Structure __instance, ref ThingSaveData __result)
    {
        if (!CommunityPatchesPlugin.SaveLogicDisplayOrientation.Value)
            return;
        if (__instance is not LogicDisplay display)
            return;
        if (__result is not LogicBaseSaveData saveData)
            return;
        saveData.MothershipReferenceId = (long)GetRotationValue(display);
    }

    [HarmonyPatch(typeof(LogicDisplay)), HarmonyPatch(nameof(LogicDisplay.DeserializeSave)), HarmonyPostfix]
    private static void PrefixDeserializeSave(LogicDisplay __instance, ref ThingSaveData savedData)
    {
        if (savedData is not StructureSaveData structureSaveData)
            return;
        if (structureSaveData.MothershipReferenceId == 0)
            return;
        if (CommunityPatchesPlugin.SaveLogicDisplayOrientation.Value)
            SetRotationValue(__instance, structureSaveData.MothershipReferenceId);
        structureSaveData.MothershipReferenceId = 0;
    }
}