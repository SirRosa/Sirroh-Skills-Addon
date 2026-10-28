using System;
using HarmonyLib;

namespace Sirroh.SkillsAddon
{
    // Companion to the vanilla/Overhaul Cooking skill for grills and ovens.
    // XP is awarded on the same successful Cooking RaiseSkill event Overhaul uses.
    internal static class GarmazerstwoStations
    {
        [ThreadStatic] private static bool collecting;
        private static bool CompletedFood(CookingStation station)
        {
            if (station?.m_slots == null) return false;
            for (int i = 0; i < station.m_slots.Length; ++i)
            {
                station.GetSlot(i, out string name, out float _, out CookingStation.Status _, out bool _);
                if (string.IsNullOrEmpty(name) || !station.IsItemDone(name)) continue;
                return station.m_overCookedItem == null || name != station.m_overCookedItem.name;
            }
            return false;
        }

        [HarmonyPatch(typeof(CookingStation), "OnInteract")]
        private static class StationCollectionScope
        {
            [HarmonyPrefix]
            private static void Prefix(CookingStation __instance)
            {
                collecting = Garmazerstwo.Enabled?.Value == true && CompletedFood(__instance);
            }
            [HarmonyPostfix]
            private static void Postfix() { collecting = false; }
            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception)
            {
                collecting = false;
                return __exception;
            }
        }

        // Priority.First: consume the station context before any other mod might
        // reset its own cooking streak tracking on the same vanilla skill grant.
        [HarmonyPatch(typeof(Player), "RaiseSkill")]
        private static class StationCookingXp
        {
            [HarmonyPrefix, HarmonyPriority(Priority.First)]
            private static void Prefix(Player __instance, Skills.SkillType __0)
            {
                if (!collecting || __0 != Skills.SkillType.Cooking ||
                    !ReferenceEquals(__instance, Player.m_localPlayer)) return;
                collecting = false; // One award per collected meal, not per repeated XP hook.
                if (Garmazerstwo.SkillType != Skills.SkillType.None)
                    __instance.RaiseSkill(Garmazerstwo.SkillType, Garmazerstwo.XpCook.Value);
            }
        }
    }
}