using System;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    internal static class SlayerCombat
    {
        private const string LevelZdo = "sirroh.skillsaddon.slayer.level.v1";
        private static ConfigEntry<bool> enabled, damageOn, staggerOn, pushOn;
        private static ConfigEntry<float> damageAt100, staggerAt100, pushAt100, hitXp;
        private static readonly ConditionalWeakTable<HitData, object> Processed = new ConditionalWeakTable<HitData, object>();
        private static readonly object Token = new object();
        private static float nextSync;
        private static float lastSync = -1f;

        internal static void Configure(ConfigFile config)
        {
            enabled = config.Bind("Pogromca", "Enabled", true, "Aktywuj umiejętność Pogromca.");
            hitXp = config.Bind("Pogromca", "XPPerHit", 0.5f, new ConfigDescription("XP za poprawne trafienie bronią Pogromcy.", new AcceptableValueRange<float>(0, 10)));
            damageOn = config.Bind("Pogromca", "BonusDamage", true, "Premia do obrażeń broni dwuręcznych.");
            damageAt100 = config.Bind("Pogromca", "DamageAt100Percent", 25f, new ConfigDescription("Premia procentowa przy poziomie 100.", new AcceptableValueRange<float>(0, 100)));
            staggerOn = config.Bind("Pogromca", "BonusStagger", true, "Premia do obrażeń ogłuszających.");
            staggerAt100 = config.Bind("Pogromca", "StaggerAt100Percent", 50f, new ConfigDescription("Bonus stagger przy 100.", new AcceptableValueRange<float>(0, 200)));
            pushOn = config.Bind("Pogromca", "PushbackResist", true, "Odporność na odrzut podczas ataku i bloku.");
            pushAt100 = config.Bind("Pogromca", "PushbackReductionAt100Percent", 100f, new ConfigDescription("Redukcja odrzutu na poziomie 100.", new AcceptableValueRange<float>(0, 100)));
        }

        internal static bool IsSlayerWeapon(ItemDrop.ItemData weapon)
        {
            if (weapon?.m_shared == null) return false;
            // The original Slayer accepts item type 14: two-handed weapons.
            // It does not change item SharedData (important for Blacksmithing).
            if ((int)weapon.m_shared.m_itemType != 14) return false;
            int type = (int)weapon.m_shared.m_skillType;
            return type == 1 || type == 3 || type == 4 || type == 7;
        }

        private static float Level(Player player)
        {
            if (player == null || Plugin.SlayerType == Skills.SkillType.None) return 0f;
            if (ReferenceEquals(player, Player.m_localPlayer))
                return Mathf.Clamp01(player.GetSkills().GetSkillLevel(Plugin.SlayerType) / 100f);
            var zdo = player.m_nview?.GetZDO();
            return zdo == null ? 0f : Mathf.Clamp01(zdo.GetFloat(LevelZdo, 0f) / 100f);
        }

        [HarmonyPatch(typeof(Player), "Update")]
        private static class PublishSkillLevel
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                if (!ReferenceEquals(__instance, Player.m_localPlayer) || Time.time < nextSync) return;
                nextSync = Time.time + 2f;
                var zdo = __instance.m_nview?.GetZDO();
                if (zdo == null || !__instance.m_nview.IsOwner()) return;
                float level = __instance.GetSkills().GetSkillLevel(Plugin.SlayerType);
                if (Math.Abs(lastSync - level) < 0.001f) return;
                zdo.Set(LevelZdo, level);
                lastSync = level;
            }
        }

        [HarmonyPatch(typeof(Character), "Damage", new Type[] { typeof(HitData) })]
        private static class IncomingHit
        {
            [HarmonyPrefix]
            private static void Prefix(Character __instance, HitData hit)
            {
                if (hit == null || __instance == null || __instance.IsPlayer()) return;
                if (Processed.TryGetValue(hit, out _)) return;
                Processed.Add(hit, Token);
                var attacker = hit.GetAttacker();
                var player = attacker as Player;
                if (player != null && enabled.Value)
                {
                    var weapon = player.GetCurrentWeapon();
                    if (IsSlayerWeapon(weapon) && hit.m_damage.GetTotalDamage() > 0f)
                    {
                        if (ReferenceEquals(player, Player.m_localPlayer) && hitXp.Value > 0f)
                            player.RaiseSkill(Plugin.SlayerType, hitXp.Value);
                        if (damageOn.Value)
                            hit.m_damage.Modify(1f + Level(player) * damageAt100.Value / 100f);
                    }
                }
                // Species bonus is separate from the weapon buff and is never
                // written into ItemData.m_shared: BE remains owner of item stats.
                SlayerTrophies.ApplySpeciesBonus(__instance, attacker, hit);
            }
        }

        [HarmonyPatch(typeof(Character), "ApplyPushback", new Type[] { typeof(Vector3), typeof(float) })]
        private static class Pushback
        {
            [HarmonyPrefix]
            private static void Prefix(Character __instance, ref Vector3 dir, ref float pushForce)
            {
                if (!enabled.Value || !pushOn.Value || !ReferenceEquals(__instance, Player.m_localPlayer)) return;
                var player = __instance as Player;
                if (player == null) return;
                float factor = player.InAttack() ? 1f : player.IsBlocking() ? .5f : 0f;
                if (factor <= 0f) return;
                float reduction = Mathf.Clamp01(Level(player) * pushAt100.Value / 100f * factor);
                pushForce *= 1f - reduction;
                if (pushForce <= 0f) { pushForce = 0f; dir = Vector3.zero; }
            }
        }

        [HarmonyPatch(typeof(Character), "AddStaggerDamage")]
        private static class Stagger
        {
            [HarmonyPrefix]
            private static void Prefix(ref float damage, HitData hit)
            {
                if (!enabled.Value || !staggerOn.Value || hit == null) return;
                var player = hit.GetAttacker() as Player;
                if (player == null || !IsSlayerWeapon(player.GetCurrentWeapon())) return;
                damage *= 1f + Level(player) * staggerAt100.Value / 100f;
            }
        }
    }
}
