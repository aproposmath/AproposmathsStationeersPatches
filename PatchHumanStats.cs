using System.Text;
using HarmonyLib;

using Assets.Scripts.Objects.Entities;

namespace AproposmathsStationeersPatches
{
    // Show more player stats in the tooltips
    static class PatchHumanStats
    {
        static string FormatState(float state)
        {
            state = state * 100; // Convert from 0-1 to percentage
            string color = state > 0.8 ? "green" : (state > 0.25 ? "yellow" : "red");
            string formattedRate = state.ToString("F1") + "%";
            return $"<color={color}>{formattedRate}</color>";
        }
        static string FormatRate(float rate)
        {
            rate *= 2 * 60 * 100; // Convert from per tick to per minute (in percent)
            string sign = rate > 0 ? "+" : "";
            string color = rate > 0 ? "green" : "yellow";
            string formattedRate = rate.ToString("F1") + "%/min";
            return $"<color={color}>{sign}{formattedRate}</color>";
        }


        [HarmonyPatch(typeof(Human)), HarmonyPatch(nameof(Human.AppendMoodTooltip)), HarmonyPostfix]
        static void Human_AppendMoodTooltip_Postfix(Human __instance, ref StringBuilder sb)
        {
            sb.AppendLine("Mood: " + FormatState(__instance.Mood));
            sb.AppendLine("Mood Rate: " + FormatRate(__instance.CalculateMoodChange()));
        }

        [HarmonyPatch(typeof(Human)), HarmonyPatch(nameof(Human.AppendHygieneTooltip)), HarmonyPostfix]
        static void Human_AppendHygieneTooltip_Postfix(Human __instance, ref StringBuilder sb)
        {
            sb.AppendLine("Hygiene: " + FormatState(__instance.Hygiene));
            sb.AppendLine("Hygiene Rate: " + FormatRate(__instance.CalculateHygieneChange()));
        }
    }
}
