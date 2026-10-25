using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    /// <summary>
    /// Optional, assembly-independent compatibility with Blacksmithing Expanded 1.2.4.
    /// No changes to item custom-data, SharedData, recipes, crafting level, skill ID,
    /// per-item infusion, durability, armor, shield/block, qualities or XP ownership.
    /// </summary>
    internal static class BlacksmithingBridge
    {
        private const string Guid = "org.bepinex.plugins.blacksmithingexpanded";
        private static bool detected;
        private static ConfigEntry<bool> diagnostics;
        private static ConfigEntry<bool> useNativeEligibility;
        private static MethodInfo eligibility;
        private static MethodInfo allowed;
        private static bool warned;
        private static readonly Dictionary<int, bool> eligibilityCache = new Dictionary<int, bool>();

        internal static void Initialize(ConfigFile config)
        {
            diagnostics = config.Bind("Blacksmithing Expanded - Compat", "Diagnostics", false,
                "Zgłaszaj rozbieżności wykryte na styku systemów.");
            useNativeEligibility = config.Bind("Blacksmithing Expanded - Compat", "UseEligibilityAPI", true,
                "Sprawdzaj przez oryginalne API, czy przedmiot podlega systemowi kowalstwa.");
            detected = Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey(Guid);
            if (!detected) return;
            try
            {
                var asm = Chainloader.PluginInfos[Guid].Instance.GetType().Assembly;
                var nested = asm.GetType("BlacksmithingExpanded.BlacksmithingExpanded+ItemEligibilityCache", false);
                eligibility = AccessTools.Method(nested, "IsEligibleForBlacksmithingBonuses", new[] { typeof(ItemDrop.ItemData) });
                allowed = AccessTools.Method(nested, "IsItemAllowed", new[] { typeof(ItemDrop.ItemData) });
                if (eligibility == null || allowed == null)
                    Plugin.Instance.Warning("Blacksmithing: API dopuszczalności zmienione; pozostawiam mod samodzielny.");
            }
            catch (Exception e)
            {
                Plugin.Instance.Warning("Blacksmithing bridge (soft-disable): " + e.GetType().Name);
                eligibility = null;
                allowed = null;
            }
        }

        // Only read native eligibility; NEVER store external ItemData or SharedData
        // references across an upgrade/clone. Classifier is not a stat authority.
        internal static bool NativeEligible(ItemDrop.ItemData item)
        {
            if (!detected || !useNativeEligibility.Value || item == null) return false;
            if (eligibility == null || allowed == null) return false;
            try
            {
                return (bool)eligibility.Invoke(null, new object[] { item }) &&
                       (bool)allowed.Invoke(null, new object[] { item });
            }
            catch (Exception e)
            {
                if (!warned)
                {
                    Plugin.Instance.Warning("Blacksmithing compatibility API failed; disabling bridge: " + e.GetType().Name);
                    warned = true;
                }
                eligibility = null;
                allowed = null;
                return false;
            }
        }

        internal static void Report()
        {
            if (!detected)
                Plugin.Instance.Info("Blacksmithing Expanded absent; optional integration not loaded.");
            else
                Plugin.Instance.Info("Blacksmithing Expanded present: foreign stats, item clones and Blacksmithing skill retained; native eligibility API " +
                    (eligibility != null && allowed != null ? "available" : "unavailable") + ".");
        }

        internal static void Clear()
        {
            eligibilityCache.Clear();
            eligibility = null;
            allowed = null;
            detected = false;
            warned = false;
        }
    }
}
