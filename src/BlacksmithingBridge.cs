using System;
using System.Collections.Generic;
using System.Linq;
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
        private static ConfigEntry<bool> preserveLevel;
        private static bool patchInstalled;
        private static Type itemDataType;
        private static FieldInfo activeItemsField;
        private static FieldInfo savedLevelField;
        private static FieldInfo lastQualityField;
        private static MethodInfo applyBonuses;
        private static MethodInfo eligibility;
        private static MethodInfo allowed;
        private static MethodInfo infoAccessor, nativeGet;
        private static FieldInfo infusionField;
        private static bool warned;
        private static readonly Dictionary<int, bool> eligibilityCache = new Dictionary<int, bool>();

        internal static void Initialize(ConfigFile config)
        {
            diagnostics = config.Bind("Blacksmithing Expanded - Compat", "Diagnostics", false,
                "Zgłaszaj rozbieżności wykryte na styku systemów.");
            useNativeEligibility = config.Bind("Blacksmithing Expanded - Compat", "UseEligibilityAPI", true,
                "Sprawdzaj przez oryginalne API, czy przedmiot podlega systemowi kowalstwa.");
            preserveLevel = config.Bind("Blacksmithing Expanded - Compat", "ProtectPersistedItemLevel", true,
                "Zapobiega przeliczeniu zapisanego bonusu wyposażenia przy niższym, chwilowym odczycie poziomu kowalstwa; nie modyfikuje plików zapisu.");
            detected = Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey(Guid);
            if (!detected) return;
            try
            {
                var asm = Chainloader.PluginInfos[Guid].Instance.GetType().Assembly;
                var nested = asm.GetType("BlacksmithingExpanded.BlacksmithingExpanded+ItemEligibilityCache", false);
                var main = asm.GetType("BlacksmithingExpanded.BlacksmithingExpanded", false);
                itemDataType = asm.GetType("BlacksmithingExpanded.BlacksmithingExpanded+BlacksmithingItemData", false);
                activeItemsField = AccessTools.Field(itemDataType, "activeItems");
                savedLevelField = AccessTools.Field(itemDataType, "level");
                lastQualityField = AccessTools.Field(itemDataType, "lastKnownQuality");
                applyBonuses = AccessTools.Method(main, "ApplyCraftingBonuses", new[] { typeof(ItemDrop.ItemData), typeof(int) });
                eligibility = AccessTools.Method(nested, "IsEligibleForBlacksmithingBonuses", new[] { typeof(ItemDrop.ItemData) });
                allowed = AccessTools.Method(nested, "IsItemAllowed", new[] { typeof(ItemDrop.ItemData) });
                infusionField = AccessTools.Field(itemDataType, "infusion");
                BindNativeDataReader(asm);
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

        // Resolve the ItemDataManager read-only API. No GetOrCreate, Save, or Load calls.
        private static void BindNativeDataReader(Assembly source)
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(a =>
                    a == source || a.GetName().Name.IndexOf("ItemDataManager", StringComparison.OrdinalIgnoreCase) >= 0);
                var extensions = assemblies.SelectMany(a => a.GetTypes())
                    .Where(t => t.Namespace != null && t.Namespace.StartsWith("ItemDataManager", StringComparison.Ordinal))
                    .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    .ToArray();
                infoAccessor = extensions.FirstOrDefault(m => m.Name == "Data" && !m.IsGenericMethod &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(ItemDrop.ItemData));
                if (infoAccessor == null) return;
                Type infoType = infoAccessor.ReturnType;
                nativeGet = infoType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "Get" && m.IsGenericMethodDefinition &&
                        m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 0);
                if (nativeGet == null)
                    nativeGet = extensions.FirstOrDefault(m => m.Name == "Get" && m.IsGenericMethodDefinition &&
                        m.GetGenericArguments().Length == 1 && m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == infoType);
                nativeGet = nativeGet?.MakeGenericMethod(itemDataType);
            }
            catch (Exception e)
            {
                infoAccessor = nativeGet = null;
                Plugin.Instance.Warning("Blacksmithing read-only ItemData API unavailable: " + e.GetType().Name);
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

        internal static void Install(Harmony harmony)
        {
            if (patchInstalled || !detected || harmony == null || applyBonuses == null) return;
            try
            {
                harmony.Patch(applyBonuses, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(BlacksmithingBridge), nameof(ProtectPreviousCraftLevel))));
                patchInstalled = true;
                Plugin.Instance.LogInfo("Blacksmithing guard installed on ApplyCraftingBonuses.");
            }
            catch (Exception e)
            {
                Plugin.Instance.Warning("Blacksmithing guard unavailable; original BE is untouched: " + e.Message);
            }
        }

        // Prefix runs BEFORE the original recalculation. It only adjusts the argument,
        // never rewrites the item's quality, SharedData, durability or custom-data.
        // Saved level comes from BE's own activeItems registry, not from Sirroh guesses.
        private static void ProtectPreviousCraftLevel(ItemDrop.ItemData item, ref int level)
        {
            if (!preserveLevel.Value || item?.m_shared == null || level <= 0 ||
                activeItemsField == null || savedLevelField == null || lastQualityField == null) return;
            try
            {
                var items = activeItemsField.GetValue(null) as System.Collections.IDictionary;
                if (items == null || !items.Contains(item.m_shared)) return;
                var state = items[item.m_shared];
                if (state == null || !itemDataType.IsInstanceOfType(state)) return;
                var formerLevel = Convert.ToInt32(savedLevelField.GetValue(state));
                var formerQuality = Convert.ToInt32(lastQualityField.GetValue(state));
                if (formerLevel > level && formerQuality > 0 && item.m_quality >= formerQuality)
                {
                    if (diagnostics.Value)
                        Plugin.Instance.Warning("Blacksmithing prevented item bonus downgrade from " + formerLevel +
                            " to " + level + " (" + item.m_shared.m_name + ").");
                    level = formerLevel;
                }
            }
            catch (Exception e)
            {
                if (!warned)
                {
                    warned = true;
                    Plugin.Instance.Warning("Blacksmithing level guard disabled for this operation: " + e.GetType().Name);
                }
            }
        }

        // Reads the original BE item data, including after cloning m_shared.
        // This is presentation only and cannot re-roll, delete or apply an infusion.
        internal static string ReadInfusion(ItemDrop.ItemData item)
        {
            if (!detected || item?.m_shared == null || infusionField == null) return null;
            try
            {
                object state = null;
                if (infoAccessor != null && nativeGet != null)
                {
                    object info = infoAccessor.Invoke(null, new object[] { item });
                    if (info != null)
                        state = nativeGet.IsStatic ? nativeGet.Invoke(null, new[] { info }) :
                            nativeGet.Invoke(info, Array.Empty<object>());
                }
                // Older BE variants index by SharedData; use only as a last resort.
                if (state == null && activeItemsField?.GetValue(null) is System.Collections.IDictionary registry &&
                    registry.Contains(item.m_shared))
                    state = registry[item.m_shared];
                return state != null && itemDataType.IsInstanceOfType(state) ?
                    infusionField.GetValue(state) as string : null;
            }
            catch (Exception e)
            {
                if (!warned)
                {
                    warned = true;
                    Plugin.Instance.Warning("Blacksmithing infusion tooltip read failed: " + e.GetType().Name);
                }
                return null;
            }
        }

        internal static void Report()
        {
            if (!detected)
                Plugin.Instance.LogInfo("Blacksmithing Expanded absent; optional integration not loaded.");
            else
                Plugin.Instance.LogInfo("Blacksmithing Expanded present: foreign stats, item clones and Blacksmithing skill retained; native eligibility API " +
                    (eligibility != null && allowed != null ? "available" : "unavailable") +
                    "; persisted-level guard " + (patchInstalled ? "installed" : "not installed") + ".");
        }

        internal static void Clear()
        {
            eligibilityCache.Clear();
            eligibility = null;
            allowed = null;
            detected = false;
            warned = false;
            patchInstalled = false;
            applyBonuses = null;
            itemDataType = null;
            activeItemsField = null;
            savedLevelField = null;
            lastQualityField = null;
            infoAccessor = nativeGet = null;
            infusionField = null;
        }
    }
}
