using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Sirroh.SkillsAddon
{
    // Inventory.AddItem normally stacks by prefab/quality without comparing m_customData.
    // This fence prevents unmodified, differently seasoned and differently rolled food from merging.
    internal static class GarmazerstwoInventory
    {
        [ThreadStatic] private static ItemDrop.ItemData incoming;
        [ThreadStatic] private static bool awarded;
        [ThreadStatic] private static int cookedCount;
        [ThreadStatic] private static ItemDrop.ItemData previous;

        [HarmonyPatch]
        private static class AddItemFence
        {
            private static IEnumerable<MethodBase> TargetMethods() =>
                typeof(Inventory).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.Name == "AddItem" && m.ReturnType == typeof(bool) &&
                    m.GetParameters().Length >= 1 &&
                    m.GetParameters()[0].ParameterType == typeof(ItemDrop.ItemData));
            private static void Prefix(ItemDrop.ItemData item)
            {
                previous = incoming;
                incoming = item;
                awarded = false;
                cookedCount = 0;
                if (Garmazerstwo.CookingCraft && Garmazerstwo.IsFood(item) &&
                    Garmazerstwo.Read(item) == null)
                {
                    cookedCount = item.m_stack;
                    Garmazerstwo.OnCook(item, cookedCount, false);
                    awarded = Garmazerstwo.Read(item) != null;
                }
            }
            private static void Postfix(bool __result)
            {
                if (__result && awarded && cookedCount > 0)
                    Garmazerstwo.AwardCookingXp(cookedCount);
                incoming = previous;
                previous = null;
                cookedCount = 0;
                awarded = false;
            }
            private static Exception Finalizer(Exception __exception)
            {
                incoming = previous;
                previous = null;
                cookedCount = 0;
                awarded = false;
                return __exception;
            }
        }

        // Global, narrowly scoped safeguard: vanilla drag/drop and QOL sorting use
        // IsSameType, which otherwise ignores m_customData and destroys per-meal rolls.
        [HarmonyPatch(typeof(ItemDrop.ItemData), "IsSameType", new[] { typeof(ItemDrop.ItemData) })]
        private static class GuardFoodIdentity
        {
            private static void Postfix(ItemDrop.ItemData __instance, ItemDrop.ItemData __0,
                ref bool __result)
            {
                if (__result && Garmazerstwo.IsFood(__instance) && Garmazerstwo.IsFood(__0) &&
                    !Garmazerstwo.SameData(__instance, __0))
                    __result = false;
            }
        }

        [HarmonyPatch]
        private static class NoMixedStacks
        {
            private static IEnumerable<MethodBase> TargetMethods() =>
                typeof(Inventory).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "FindFreeStackItem" &&
                                m.ReturnType == typeof(ItemDrop.ItemData));
            private static void Postfix(ref ItemDrop.ItemData __result)
            {
                if (__result == null) return;
                // Crafting through the AddItem(string...) overload creates a temporary item
                // internally. Never merge into a pre-existing stack before its roll is saved.
                if (incoming == null && Garmazerstwo.CookingCraft && Garmazerstwo.IsFood(__result))
                {
                    __result = null;
                    return;
                }
                if (incoming == null || !Garmazerstwo.IsFood(incoming)) return;
                if (!Garmazerstwo.SameData(incoming, __result))
                    __result = null;
            }
        }
    }
}