using System;
using System.Reflection;
using HarmonyLib;

namespace Sirroh.SkillsAddon
{
    // Companion to the vanilla/Overhaul Cooking skill for grills and ovens.
    // XP is awarded on the same successful Cooking RaiseSkill event Overhaul uses.
    internal static class GarmazerstwoStations
    {
        [ThreadStatic] private static bool collecting;
        private static readonly MethodInfo Slot = AccessTools.Method(typeof(CookingStation), "GetSlot");
        private static readonly MethodInfo Done = AccessTools.Method(typeof(CookingStation), "IsItemDone");
        private static bool CompletedFood(CookingStation station)
        {
            if (station?.m_slots == null || Slot == null || Done == null) return false;
            for (int i = 0; i < station.m_slots.Length; ++i)
            {
                try
                {
                    Type status = Slot.GetParameters()[3].ParameterType.GetElementType();
                    object[] args = { i, "", 0f, Activator.CreateInstance(status), false };
                    Slot.Invoke(station, args);
                    string name = args[1] as string;
                    if (string.IsNullOrEmpty(name) || !(bool)Done.Invoke(station, new object[] { name }))
                        continue;
                    return station.m_overCookedItem == null || name != station.m_overCookedItem.name;
                }
                catch (Exception) { return false; }
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