using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    // ZenUI 1.9.3 builds its simplified food icons separately from vanilla tooltips.
    // It reads shared prefab values (m_food, m_foodStamina, m_foodEitr), ignoring
    // per-instance m_customData from Garmazerstwo. Correct only its returned text:
    // do not mutate m_shared, qualities, statuses or inventory items.
    internal static class ZenUiCompatibility
    {
        private const string ZenGuid = "ZenDragon.ZenUI";
        private static readonly Regex IconBlock = new Regex(
            @"<nobr>(?:(?!</nobr>).)*</nobr>",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly Regex IconValue = new Regex(
            @"(?<=<size=-2>)-?\d+(?:[.,]\d+)?(?=</size>)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static bool installed;
        private static bool disabled;
        private static float nextAttempt;

        internal static void TryInstall(Harmony harmony)
        {
            if (installed || disabled || harmony == null || Time.unscaledTime < nextAttempt) return;
            nextAttempt = Time.unscaledTime + 2f;
            if (Chainloader.PluginInfos == null ||
                !Chainloader.PluginInfos.TryGetValue(ZenGuid, out var plugin) ||
                plugin?.Instance == null) return;
            try
            {
                Assembly assembly = plugin.Instance.GetType().Assembly;
                Type summary = assembly.GetType("ZenUI.Section.TooltipSummary", false);
                MethodInfo target = summary?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(method => method.Name == "GetTooltipSummary" &&
                        method.ReturnType == typeof(string) &&
                        method.GetParameters().Length == 3 &&
                        method.GetParameters()[1].ParameterType == typeof(int) &&
                        method.GetParameters()[2].ParameterType == typeof(bool));
                if (target == null)
                {
                    disabled = true; // Fail closed on an incompatible ZenUI version.
                    Plugin.Instance.Warning("ZenUI: nie znaleziono oczekiwanego GetTooltipSummary; integracja nieaktywna.");
                    return;
                }
                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(ZenUiCompatibility), nameof(PatchSummary))));
                installed = true;
                Plugin.Instance.LogInfo("Garmażerstwo: wykryto ZenUI; korekta uproszczonych statystyk aktywna.");
            }
            catch (Exception e)
            {
                disabled = true;
                Plugin.Instance.Warning("ZenUI: nie udało się uruchomić izolowanej korekty tooltipów: " +
                                        e.GetType().Name + " " + e.Message);
            }
        }

        internal static void Reset()
        {
            installed = false;
            disabled = false;
            nextAttempt = 0f;
        }

        // Do not reference Zen.ModLib or ZenUI assemblies at compile time.
        // Zen's ItemData may be wrapped by Zen.ModLib; shared and custom data
        // are resolved from the object actually passed to GetTooltipSummary.
        private static bool FoodValues(object source, out float hp, out float stamina, out float eitr)
        {
            hp = stamina = eitr = 0f;
            if (source == null || Garmazerstwo.Enabled?.Value != true) return false;
            if (source is ItemDrop.ItemData direct)
            {
                if (!Garmazerstwo.IsFood(direct) || Garmazerstwo.Read(direct) == null) return false;
                Garmazerstwo.Effective(direct, out hp, out stamina, out eitr,
                    out _, out _, out _);
                return true;
            }
            // Compatible with Zen.ModLib's ItemData wrapper without linking its DLL.
            Type type = source.GetType();
            object shared = AccessTools.Field(type, "m_shared")?.GetValue(source);
            object custom = AccessTools.Field(type, "m_customData")?.GetValue(source);
            if (shared == null || !(custom is IDictionary<string, string> saved) ||
                !saved.TryGetValue(Garmazerstwo.Key, out string raw)) return false;
            var data = Garmazerstwo.Parse(raw);
            if (data == null) return false;
            Type sharedType = shared.GetType();
            float foodHp = ReadShared(sharedType, shared, "m_food");
            float foodStamina = ReadShared(sharedType, shared, "m_foodStamina");
            float foodEitr = ReadShared(sharedType, shared, "m_foodEitr");
            if (foodHp <= 0f && foodStamina <= 0f && foodEitr <= 0f) return false;
            hp = foodHp * (1f + Mathf.Clamp01(data.CookPercent)) +
                Mathf.Clamp(data.Hp, 0f, Garmazerstwo.SpiceLimit(foodHp));
            stamina = foodStamina * (1f + Mathf.Clamp01(data.CookPercent)) +
                Mathf.Clamp(data.Stamina, 0f, Garmazerstwo.SpiceLimit(foodStamina));
            eitr = foodEitr * (1f + Mathf.Clamp01(data.CookPercent)) +
                Mathf.Clamp(data.Eitr, 0f, Garmazerstwo.SpiceLimit(foodEitr));
            return true;
        }

        private static float ReadShared(Type type, object instance, string name)
        {
            FieldInfo field = AccessTools.Field(type, name);
            return field == null ? 0f : Convert.ToSingle(field.GetValue(instance), CultureInfo.InvariantCulture);
        }

        private static string Format(float value) => value.ToString("0.#", CultureInfo.CurrentCulture);

        // This is a postfix on ZenUI.Section.TooltipSummary.GetTooltipSummary.
        // Zen generates <nobr>...💖 23...</nobr> from m_shared; replace only the
        // three food-value icon blocks with values already stored by our skill.
        // All other ZenUI information and formatting remain untouched.
        private static void PatchSummary(object __0, ref string __result)
        {
            if (string.IsNullOrEmpty(__result)) return;
            try
            {
                if (!FoodValues(__0, out float hp, out float stamina, out float eitr)) return;
                bool foundHp = false, foundStamina = false, foundEitr = false;
                string output = IconBlock.Replace(__result, match =>
                {
                    string block = match.Value;
                    float value;
                    if (block.Contains("💖")) { foundHp = true; value = hp; }
                    else if (block.Contains("💪")) { foundStamina = true; value = stamina; }
                    else if (block.Contains("✨")) { foundEitr = true; value = eitr; }
                    else return block;
                    return IconValue.Replace(block, Format(value), 1);
                });

                // Zen omits food icons whose *base* stat is zero. In a seasoned
                // meal that gains Eitr/HP/stamina, show that new stat as well.
                // Only add icons in a real Zen food summary (never in abstract mode).
                if (foundHp || foundStamina || foundEitr)
                {
                    string added = "";
                    if (hp > 0f && !foundHp) added += MakeIcon("💖", hp, "#ff8080ff");
                    if (stamina > 0f && !foundStamina) added += MakeIcon("💪", stamina, "#ffff80ff");
                    if (eitr > 0f && !foundEitr) added += MakeIcon("✨", eitr, "#9090ffff");
                    if (added.Length > 0)
                    {
                        var all = IconBlock.Matches(output);
                        Match foodBlock = null;
                        foreach (Match block in all)
                            if (block.Value.Contains("💖") || block.Value.Contains("💪") || block.Value.Contains("✨"))
                                foodBlock = block;
                        if (foodBlock != null)
                            output = output.Insert(foodBlock.Index + foodBlock.Length, added);
                    }
                }
                __result = output;
            }
            catch (Exception e)
            {
                // Rendering must never break a tooltip or disable other skills.
                if (!warned)
                {
                    warned = true;
                    Plugin.Instance?.Warning("ZenUI: nie udało się odczytać statystyk przyprawionego posiłku: " +
                                             e.GetType().Name);
                }
            }
        }

        private static bool warned;
        private static string MakeIcon(string emoji, float amount, string color)
        {
            return "<nobr><size=+4><color=" + color + ">" + emoji +
                   "</color></size><space=2px><color=#ffffff><size=-2>" + Format(amount) +
                   "</size></color></nobr>";
        }
    }
}