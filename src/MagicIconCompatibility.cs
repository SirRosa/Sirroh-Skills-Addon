using System;
using System.Collections;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    // The Overhaul redraws vanilla skill icons every ten seconds. Correct only the
    // two reversed sprites after its refresh; never touch levels, IDs or saves.
    internal static class MagicIconCompatibility
    {
        private static IDictionary overhaulSprites;
        private static bool installed;

        internal static void Install(Harmony harmony)
        {
            if (installed || harmony == null || Chainloader.PluginInfos == null ||
                !Chainloader.PluginInfos.TryGetValue(Plugin.OverhaulGuid, out var plugin) ||
                plugin?.Instance == null) return;
            try
            {
                Assembly assembly = plugin.Instance.GetType().Assembly;
                Type overrides = assembly.GetType("Sirro.PracaFizyczna.SkillIconOverrides", false);
                MethodInfo tick = AccessTools.Method(overrides, "Tick");
                overhaulSprites = AccessTools.Field(overrides, "Sprites")?.GetValue(null) as IDictionary;
                if (tick == null || overhaulSprites == null)
                {
                    Plugin.Instance.Warning("Magic icon fix unavailable: Overhaul icon system not found.");
                    return;
                }
                harmony.Patch(tick, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(MagicIconCompatibility), nameof(AfterOverhaulRefresh))));
                installed = true;
            }
            catch (Exception e)
            {
                overhaulSprites = null;
                Plugin.Instance.Warning("Magic icon fix unavailable: " + e.GetType().Name);
            }
        }

        private static void AfterOverhaulRefresh()
        {
            var skills = Player.m_localPlayer?.GetSkills()?.m_skills;
            if (skills == null || overhaulSprites == null) return;
            var originalBlood = overhaulSprites["BloodMagic"] as Sprite;
            var originalElemental = overhaulSprites["ElementalMagic"] as Sprite;
            if (originalBlood == null || originalElemental == null) return;
            Skills.SkillDef blood = null, elemental = null;
            foreach (var skill in skills)
            {
                if (skill == null) continue;
                if (skill.m_skill == Skills.SkillType.BloodMagic) blood = skill;
                else if (skill.m_skill == Skills.SkillType.ElementalMagic) elemental = skill;
            }
            // The original Tick is an early-return on most frames; only swap if
            // it just re-assigned the original sprites. Never swap repeatedly.
            if (blood != null && elemental != null &&
                ReferenceEquals(blood.m_icon, originalBlood) &&
                ReferenceEquals(elemental.m_icon, originalElemental))
            {
                blood.m_icon = originalElemental;
                elemental.m_icon = originalBlood;
            }
        }
    }
}
