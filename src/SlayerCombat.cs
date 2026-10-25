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
        private const string BrawlerLevelZdo = "sirroh.skillsaddon.brawler.level.v1";
        private static ConfigEntry<bool> enabled, damageOn, staggerOn, pushOn;
        private static ConfigEntry<float> damageAt100, staggerAt100, pushAt100, hitXp;
        private static ConfigEntry<bool> brawlerEnabled;
        private static ConfigEntry<float> brawlerDamage, brawlerStagger, brawlerPush, brawlerHitXp;
        private static readonly ConditionalWeakTable<HitData, object> Processed = new ConditionalWeakTable<HitData, object>();
        private static readonly object Token = new object();
        private static float nextSync;
        private static float lastSync = -1f;
        private static float lastBrawlerSync = -1f;

        internal static void Configure(ConfigFile config)
        {
            enabled = config.Bind("Kolos", "Enabled", true, "Aktywuj umiejętność Kolos.");
            hitXp = config.Bind("Pogromca", "XPPerHit", 0.5f, new ConfigDescription("XP za poprawne trafienie bronią Kolosa.", new AcceptableValueRange<float>(0, 10)));
            damageOn = config.Bind("Pogromca", "BonusDamage", true, "Premia do obrażeń broni dwuręcznych.");
            damageAt100 = config.Bind("Pogromca", "DamageAt100Percent", 25f, new ConfigDescription("Premia procentowa przy poziomie 100.", new AcceptableValueRange<float>(0, 100)));
            staggerOn = config.Bind("Pogromca", "BonusStagger", true, "Premia do obrażeń ogłuszających.");
            staggerAt100 = config.Bind("Pogromca", "StaggerAt100Percent", 50f, new ConfigDescription("Bonus stagger przy 100.", new AcceptableValueRange<float>(0, 200)));
            pushOn = config.Bind("Pogromca", "PushbackResist", true, "Odporność na odrzut podczas ataku i bloku.");
            pushAt100 = config.Bind("Pogromca", "PushbackReductionAt100Percent", 200f, new ConfigDescription("Redukcja odrzutu na poziomie 100.", new AcceptableValueRange<float>(0, 300)));
            brawlerEnabled = config.Bind("Zawadiaka", "Enabled", true, "Rozwój i premie za używanie broni jednoręcznych.");
            brawlerHitXp = config.Bind("Zawadiaka", "XPPerHit", 0.5f,
                new ConfigDescription("XP za trafienie bronią jednoręczną.", new AcceptableValueRange<float>(0f, 10f)));
            brawlerDamage = config.Bind("Zawadiaka", "DamageAt100Percent", 18f,
                new ConfigDescription("Premia do obrażeń jednoręcznych na poziomie 100 (nie dubluje Kolosa).", new AcceptableValueRange<float>(0f, 100f)));
            brawlerStagger = config.Bind("Zawadiaka", "StaggerAt100Percent", 35f,
                new ConfigDescription("Premia do ogłuszenia broni jednoręcznych na poziomie 100.", new AcceptableValueRange<float>(0f, 200f)));
            brawlerPush = config.Bind("Zawadiaka", "PushbackReductionAt100Percent", 75f,
                new ConfigDescription("Redukcja odrzutu przy jednoręcznej broni (połowa podczas bloku).", new AcceptableValueRange<float>(0f, 100f)));
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

        internal static bool IsBrawlerWeapon(ItemDrop.ItemData weapon)
        {
            // Vanilla OneHandedWeapon = 3; exclude bare fists, tools, shields, bows and two-handers.
            return weapon?.m_shared != null && (int)weapon.m_shared.m_itemType == 3;
        }

        private static float BrawlerLevel(Player player)
        {
            if (player == null || Plugin.BrawlerType == Skills.SkillType.None) return 0f;
            if (ReferenceEquals(player, Player.m_localPlayer))
                return Mathf.Clamp01(player.GetSkills().GetSkillLevel(Plugin.BrawlerType) / 100f);
            var zdo = ValheimAccess.View(player)?.GetZDO();
            return zdo == null ? 0f : Mathf.Clamp01(zdo.GetFloat(BrawlerLevelZdo, 0f) / 100f);
        }

        private static float Level(Player player)
        {
            if (player == null || Plugin.SlayerType == Skills.SkillType.None) return 0f;
            if (ReferenceEquals(player, Player.m_localPlayer))
                return Mathf.Clamp01(player.GetSkills().GetSkillLevel(Plugin.SlayerType) / 100f);
            var zdo = ValheimAccess.View(player)?.GetZDO();
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
                var view = ValheimAccess.View(__instance);
                var zdo = view?.GetZDO();
                if (zdo == null || !view.IsOwner()) return;
                float level = __instance.GetSkills().GetSkillLevel(Plugin.SlayerType);
                if (Math.Abs(lastSync - level) >= 0.001f)
                {
                    zdo.Set(LevelZdo, level);
                    lastSync = level;
                }
                float brawler = __instance.GetSkills().GetSkillLevel(Plugin.BrawlerType);
                if (Math.Abs(lastBrawlerSync - brawler) >= 0.001f)
                {
                    zdo.Set(BrawlerLevelZdo, brawler);
                    lastBrawlerSync = brawler;
                }
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
                if (player != null && hit.m_damage.GetTotalDamage() > 0f)
                {
                    var weapon = player.GetCurrentWeapon();
                    if (enabled.Value && IsSlayerWeapon(weapon))
                    {
                        if (ReferenceEquals(player, Player.m_localPlayer) && hitXp.Value > 0f)
                            player.RaiseSkill(Plugin.SlayerType, hitXp.Value);
                        if (damageOn.Value)
                            hit.m_damage.Modify(1f + Level(player) * damageAt100.Value / 100f);
                    }
                    else if (brawlerEnabled.Value && IsBrawlerWeapon(weapon))
                    {
                        if (ReferenceEquals(player, Player.m_localPlayer) && brawlerHitXp.Value > 0f)
                            player.RaiseSkill(Plugin.BrawlerType, brawlerHitXp.Value);
                        hit.m_damage.Modify(1f + BrawlerLevel(player) * brawlerDamage.Value / 100f);
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
                if (!ReferenceEquals(__instance, Player.m_localPlayer)) return;
                var player = __instance as Player;
                if (player == null) return;
                float factor = player.InAttack() ? 1f : player.IsBlocking() ? .5f : 0f;
                if (factor <= 0f) return;
                var weapon = player.GetCurrentWeapon();
                float reduction = 0f;
                if (enabled.Value && pushOn.Value && IsSlayerWeapon(weapon))
                    reduction = Mathf.Clamp01(Level(player) * pushAt100.Value / 100f * factor);
                else if (brawlerEnabled.Value && IsBrawlerWeapon(weapon))
                    reduction = Mathf.Clamp01(BrawlerLevel(player) * brawlerPush.Value / 100f * factor);
                if (reduction <= 0f) return;
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
                if (hit == null) return;
                var player = hit.GetAttacker() as Player;
                if (player == null) return;
                var weapon = player.GetCurrentWeapon();
                if (enabled.Value && staggerOn.Value && IsSlayerWeapon(weapon))
                    damage *= 1f + Level(player) * staggerAt100.Value / 100f;
                else if (brawlerEnabled.Value && IsBrawlerWeapon(weapon))
                    damage *= 1f + BrawlerLevel(player) * brawlerStagger.Value / 100f;
            }
        }
    }
}
