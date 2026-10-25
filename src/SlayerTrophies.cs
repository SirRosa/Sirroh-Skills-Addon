using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    internal static class SlayerTrophies
    {
        private const string SaveKey = "sirroh.skillsaddon.slayerspecies.v1";
        // The original CustomDataWrapper persisted this exact double-underscore key.
        private const string LegacySaveKey = "SlayerSkills_1847__C";
        private const string AwardRpc = "sirroh.skillsaddon.slayer.award.v1";
        private const string DotOwnerKey = "sirroh.skillsaddon.slayer.dot.owner.v1";
        private const string DotLevelKey = "sirroh.skillsaddon.slayer.dot.level.v1";
        private static readonly Dictionary<string, float> Points = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> CreatureToTrophy = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> DisplayToTrophy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static ConfigEntry<bool> enabled, physical, elemental, other, useCreatureLevel, tamesKill, tamesBuff, loseOnDeath, serverAuthoritative;
        private static ConfigEntry<int> pointsCap, damageCap, bossPoints;
        private static ConfigEntry<bool> allowCheats;
        private static ConfigEntry<float> radius;
        private static ConfigEntry<AwardMode> awardMode;
        private static Player activePlayer;
        private static ZDOID publishedTo = ZDOID.None;
        private static Context deathContext, ragdollContext;

        private enum AwardMode { Random, All, Distribute, KillingBlow }

        private struct Context
        {
            internal long PeerId;
            internal int Level;
        }

        internal static void Configure(ConfigFile config)
        {
            enabled = config.Bind("Pogromca Stworzeń", "Enabled", true, "Progres gatunków po wypadnięciu trofeum.");
            pointsCap = config.Bind("Pogromca Stworzeń", "SpeciesCap", 100,
                new ConfigDescription("Limit punktów na każdy gatunek.", new AcceptableValueRange<int>(1, 1000)));
            damageCap = config.Bind("Pogromca Stworzeń", "DamageAtCapPercent", 100,
                new ConfigDescription("Maksymalny bonus do obrażeń przeciw gatunkowi.", new AcceptableValueRange<int>(0, 200)));
            physical = config.Bind("Pogromca Stworzeń", "PhysicalDamage", true, "Premia do obrażeń fizycznych.");
            elemental = config.Bind("Pogromca Stworzeń", "ElementalDamage", false, "Premia do obrażeń ogniowych/mrozu/błyskawic.");
            other = config.Bind("Pogromca Stworzeń", "OtherDamage", false, "Premia do trucizny i ducha.");
            useCreatureLevel = config.Bind("Pogromca Stworzeń", "ScaleByStars", false, "Więcej punktów za wyższy poziom (gwiazdki CLLC).");
            tamesKill = config.Bind("Pogromca Stworzeń", "TamedKillsCount", true, "Zalicza zabójstwa własnych oswojonych stworzeń.");
            tamesBuff = config.Bind("Pogromca Stworzeń", "TamedCreaturesGetBonus", false, "Chowańce korzystają z premii właściciela przeciw gatunkowi.");
            loseOnDeath = config.Bind("Pogromca Stworzeń", "LossOnDeath", true, "Utrata części nieutrwalonych punktów po śmierci.");
            allowCheats = config.Bind("Pogromca Stworzeń", "AllowCheats", false, "Sterowanie progresją gatunków poleceniami raiseskill/resetskill.");
            radius = config.Bind("Pogromca Stworzeń", "TrophyDistance", 30f,
                new ConfigDescription("Promień rozdziału punktów w trybie drużynowym.", new AcceptableValueRange<float>(1, 200)));
            bossPoints = config.Bind("Pogromca Stworzeń", "BossBonusPoints", 3,
                new ConfigDescription("Dodatkowe punkty za trofeum bossa.", new AcceptableValueRange<int>(0, 100)));
            awardMode = config.Bind("Pogromca Stworzeń", "PointsAssignment", AwardMode.Random,
                "Random/All/Distribute/KillingBlow. Domyślnie ostatni cios.");
            serverAuthoritative = config.Bind("Pogromca Stworzeń", "OnlyHostMayAwardPoints", false,
                "Odrzuca nieautoryzowane klient-klient RPC z punktami. Właścicielem łupów powinien być host.");
        }

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegisterRpc
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                ZRoutedRpc.instance?.Register<string, float>(AwardRpc, ReceivePoints);
            }
        }

        [HarmonyPatch(typeof(Player), "Start")]
        private static class PlayerStart
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                if (!ReferenceEquals(__instance, Player.m_localPlayer)) return;
                activePlayer = __instance;
                Points.Clear();
                if (!__instance.m_customData.TryGetValue(SaveKey, out var contents))
                    __instance.m_customData.TryGetValue(LegacySaveKey, out contents);
                Parse(contents);
                PublishAll();
            }
        }

        [HarmonyPatch(typeof(Player), "Update")]
        private static class InitialNetworkPublication
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                if (!ReferenceEquals(activePlayer, __instance)) return;
                ZDOID current = __instance.GetZDOID();
                if (current == ZDOID.None || current == publishedTo) return;
                var view = ValheimAccess.View(__instance);
                if (view == null || !view.IsOwner() || view.GetZDO() == null) return;
                PublishAll();
                publishedTo = current;
            }
        }

        [HarmonyPatch(typeof(Player), "Save")]
        private static class PlayerSave
        {
            [HarmonyPrefix]
            private static void Prefix(Player __instance)
            {
                if (ReferenceEquals(__instance, activePlayer)) Save();
            }
        }

        [HarmonyPatch(typeof(Player), "OnDestroy")]
        private static class PlayerDestroy
        {
            [HarmonyPostfix]
            private static void Postfix(Player __instance)
            {
                if (ReferenceEquals(__instance, activePlayer)) Clear();
            }
        }

        [HarmonyPatch(typeof(Skills), "LowerAllSkills")]
        private static class SkillLoss
        {
            [HarmonyPostfix]
            private static void Postfix(Skills __instance, float factor)
            {
                if (!enabled.Value || !loseOnDeath.Value || activePlayer == null ||
                    !ReferenceEquals(activePlayer.GetSkills(), __instance)) return;
                foreach (var token in Points.Keys.ToList())
                {
                    if (Points[token] >= pointsCap.Value) continue;
                    Points[token] = Mathf.Clamp(Points[token] * (1f - factor), 0f, pointsCap.Value);
                    Publish(token);
                }
                Save();
            }
        }

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static class SceneAwake
        {
            [HarmonyPostfix]
            private static void Postfix() => BuildMap();
        }

        private static void BuildMap()
        {
            CreatureToTrophy.Clear();
            DisplayToTrophy.Clear();
            var scene = ZNetScene.instance;
            if (scene?.m_prefabs == null) return;
            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null || prefab.GetComponent<Character>() == null) continue;
                var drops = prefab.GetComponent<CharacterDrop>();
                if (drops?.m_drops == null) continue;
                foreach (var drop in drops.m_drops)
                {
                    var item = drop.m_prefab == null ? null : drop.m_prefab.GetComponent<ItemDrop>();
                    if (item?.m_itemData?.m_shared == null || (int)item.m_itemData.m_shared.m_itemType != 13) continue;
                    var token = item.m_itemData.m_shared.m_name;
                    CreatureToTrophy[prefab.name] = token;
                    var label = Localization.instance == null ? token : Localization.instance.Localize(token);
                    DisplayToTrophy[NormalizeLabel(label)] = token;
                    break;
                }
            }
        }

        private static string NormalizeLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            var text = label.Trim();
            if (text.EndsWith(" trophy", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(0, text.Length - 7);
            return text.Trim();
        }

        private static string TrophyFor(Character creature)
        {
            if (creature == null) return null;
            var name = creature.gameObject.name.Replace("(Clone)", "").Trim();
            if (CreatureToTrophy.TryGetValue(name, out var key)) return key;
            // Custom/CLLC prefabs can appear after initial map construction.
            var drops = creature.GetComponent<CharacterDrop>();
            if (drops?.m_drops != null)
                foreach (var drop in drops.m_drops)
                {
                    var item = drop.m_prefab == null ? null : drop.m_prefab.GetComponent<ItemDrop>();
                    if (item?.m_itemData?.m_shared != null && (int)item.m_itemData.m_shared.m_itemType == 13)
                    {
                        CreatureToTrophy[name] = item.m_itemData.m_shared.m_name;
                        return item.m_itemData.m_shared.m_name;
                    }
                }
            return null;
        }

        private static string NetworkKey(string trophy)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char ch in trophy) { hash ^= ch; hash *= 16777619; }
                return "sirroh.skillsaddon.trophy." + hash.ToString("x8", CultureInfo.InvariantCulture);
            }
        }

        private static void Publish(string token)
        {
            var view = ValheimAccess.View(activePlayer);
            if (view == null || !view.IsOwner() || view.GetZDO() == null) return;
            if (Points.TryGetValue(token, out var value))
                view.GetZDO().Set(NetworkKey(token), value);
        }

        private static void PublishAll()
        {
            foreach (var token in Points.Keys) Publish(token);
        }

        private static void Save()
        {
            if (activePlayer == null) return;
            var parts = Points.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => p.Key + ":" + p.Value.ToString("R", CultureInfo.InvariantCulture));
            activePlayer.m_customData[SaveKey] = string.Join(",", parts);
        }

        private static void Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            foreach (string pair in raw.Split(','))
            {
                int idx = pair.LastIndexOf(':');
                if (idx <= 0 || idx >= pair.Length - 1) continue;
                var name = pair.Substring(0, idx);
                if (float.TryParse(pair.Substring(idx + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var level) &&
                    !float.IsNaN(level) && !float.IsInfinity(level))
                    Points[name] = Mathf.Clamp(level, 0f, pointsCap.Value);
            }
        }

        internal static void ApplySpeciesBonus(Character victim, Character attacker, HitData hit)
        {
            if (!enabled.Value || victim == null || victim.IsPlayer() || attacker == null || hit == null) return;
            string key = TrophyFor(victim);
            if (key == null) return;
            Player owner = attacker as Player;
            if (owner == null && tamesBuff.Value && attacker.IsTamed()) owner = GetTameOwner(attacker);
            if (owner == null) return;
            float amount = 0f;
            if (ReferenceEquals(owner, activePlayer)) Points.TryGetValue(key, out amount);
            else
            {
                var zdo = ValheimAccess.View(owner)?.GetZDO();
                if (zdo != null) amount = zdo.GetFloat(NetworkKey(key), 0f);
            }
            float boost = Mathf.Clamp01(amount / pointsCap.Value) * damageCap.Value / 100f;
            if (boost <= 0f) return;
            if (physical.Value)
            {
                hit.m_damage.m_blunt *= 1f + boost;
                hit.m_damage.m_slash *= 1f + boost;
                hit.m_damage.m_pierce *= 1f + boost;
            }
            if (elemental.Value)
            {
                hit.m_damage.m_fire *= 1f + boost;
                hit.m_damage.m_frost *= 1f + boost;
                hit.m_damage.m_lightning *= 1f + boost;
            }
            if (other.Value)
            {
                hit.m_damage.m_poison *= 1f + boost;
                hit.m_damage.m_spirit *= 1f + boost;
            }
        }

        private static Player GetTameOwner(Character attacker)
        {
            if (attacker == null || !attacker.IsTamed()) return null;
            // Sirroh familar owner key uses player identity, not ZDO network owner.
            var id = ValheimAccess.View(attacker)?.GetZDO()?.GetLong("sirroh.tresura.owner.v1", 0L) ?? 0L;
            foreach (var player in Player.GetAllPlayers())
                if (player != null && (id != 0L && player.GetPlayerID() == id)) return player;
            var ai = attacker.GetComponent<MonsterAI>();
            return ai?.GetFollowTarget()?.GetComponent<Player>();
        }

        private static long AttackerPeer(Character target)
        {
            var hit = ValheimAccess.LastHit(target);
            Character attacker = hit == null ? null : hit.GetAttacker();
            if (attacker == null) return ValheimAccess.View(target)?.GetZDO()?.GetLong(DotOwnerKey, 0L) ?? 0L;
            var player = attacker as Player;
            if (player == null && tamesKill.Value) player = GetTameOwner(attacker);
            return player?.GetOwner() ?? 0L;
        }

        private static Context Capture(Character target)
        {
            if (target == null || target.IsPlayer() || target.IsTamed()) return default;
            return new Context
            {
                PeerId = AttackerPeer(target),
                Level = (useCreatureLevel.Value ? Math.Max(1, target.GetLevel()) : 1) +
                        (target.IsBoss() ? bossPoints.Value : 0)
            };
        }

        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        private static class DotAttribution
        {
            [HarmonyPrefix]
            private static void Prefix(Character __instance, HitData hit)
            {
                if (__instance == null || hit == null || __instance.IsPlayer() ||
                    ValheimAccess.View(__instance) == null || !ValheimAccess.View(__instance).IsOwner() || !hit.HaveAttacker()) return;
                if (hit.m_damage.m_fire + hit.m_damage.m_poison + hit.m_damage.m_spirit <= 0f) return;
                var player = hit.GetAttacker() as Player;
                if (player == null) return;
                var zdo = ValheimAccess.View(__instance).GetZDO();
                if (zdo == null) return;
                zdo.Set(DotOwnerKey, player.GetOwner());
                zdo.Set(DotLevelKey, __instance.GetLevel());
            }
        }

        [HarmonyPatch(typeof(CharacterDrop), "OnDeath")]
        private static class Death
        {
            [HarmonyPrefix]
            private static void Prefix(CharacterDrop __instance) => deathContext = Capture(__instance?.GetComponent<Character>());
            [HarmonyFinalizer]
            private static void Finalizer() => deathContext = default;
        }

        [HarmonyPatch(typeof(Ragdoll), "SaveLootList")]
        private static class RagSave
        {
            [HarmonyPrefix]
            private static void Prefix(Ragdoll __instance, CharacterDrop characterDrop)
            {
                var state = Capture(characterDrop?.GetComponent<Character>());
                var zdo = ValheimAccess.View(__instance)?.GetZDO();
                if (zdo != null && state.PeerId != 0)
                {
                    zdo.Set(DotOwnerKey, state.PeerId);
                    zdo.Set(DotLevelKey, state.Level);
                }
            }
        }

        [HarmonyPatch(typeof(Ragdoll), "SpawnLoot")]
        private static class RagSpawn
        {
            [HarmonyPrefix]
            private static void Prefix(Ragdoll __instance)
            {
                var zdo = ValheimAccess.View(__instance)?.GetZDO();
                ragdollContext = zdo == null ? default : new Context
                {
                    PeerId = zdo.GetLong(DotOwnerKey, 0L),
                    Level = zdo.GetInt(DotLevelKey, 1)
                };
            }
            [HarmonyFinalizer]
            private static void Finalizer() => ragdollContext = default;
        }

        [HarmonyPatch(typeof(CharacterDrop), "DropItems")]
        private static class DroppedTrophy
        {
            [HarmonyPostfix]
            private static void Postfix(List<KeyValuePair<GameObject, int>> drops, Vector3 centerPos)
            {
                if (!enabled.Value || drops == null) return;
                var state = deathContext.PeerId != 0 ? deathContext : ragdollContext;
                if (state.PeerId == 0 || state.Level <= 0) return;
                foreach (var pair in drops)
                {
                    var item = pair.Key == null ? null : pair.Key.GetComponent<ItemDrop>();
                    if (item?.m_itemData?.m_shared == null || (int)item.m_itemData.m_shared.m_itemType != 13) continue;
                    Award(item.m_itemData.m_shared.m_name, state.Level, state.PeerId, centerPos);
                    break; // Exactly one award per creature, not per trophy stack.
                }
            }
        }

        private static void Award(string trophy, float value, long killer, Vector3 center)
        {
            if (string.IsNullOrEmpty(trophy) || value <= 0f || ZRoutedRpc.instance == null) return;
            if (serverAuthoritative.Value && (ZNet.instance == null || !ZNet.instance.IsServer())) return;
            var targets = new List<Player>();
            foreach (var player in Player.GetAllPlayers())
                if (player != null && (player.transform.position - center).sqrMagnitude <= radius.Value * radius.Value)
                    targets.Add(player);
            if (awardMode.Value == AwardMode.KillingBlow || targets.Count == 0)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(killer, AwardRpc, trophy, value);
                return;
            }
            if (awardMode.Value == AwardMode.Random)
            {
                var winner = targets[UnityEngine.Random.Range(0, targets.Count)];
                ZRoutedRpc.instance.InvokeRoutedRPC(winner.GetOwner(), AwardRpc, trophy, value);
                return;
            }
            if (awardMode.Value == AwardMode.Distribute) value /= targets.Count;
            foreach (var player in targets)
                ZRoutedRpc.instance.InvokeRoutedRPC(player.GetOwner(), AwardRpc, trophy, value);
        }

        private static void ReceivePoints(long sender, string trophy, float points)
        {
            if (!enabled.Value || activePlayer == null || string.IsNullOrEmpty(trophy) || trophy.Length > 200 ||
                points <= 0f || points > 200f || float.IsNaN(points) || float.IsInfinity(points)) return;
            if (serverAuthoritative.Value && sender != ValheimAccess.ServerPeerID()) return;
            Points.TryGetValue(trophy, out var old);
            float next = Mathf.Min(pointsCap.Value, old + points);
            if (next <= old) return;
            Points[trophy] = next;
            Save();
            Publish(trophy);
            if (Mathf.FloorToInt(next) > Mathf.FloorToInt(old))
                activePlayer.Message(MessageHud.MessageType.TopLeft,
                    "Pogromca: " + NormalizeLabel(Localization.instance?.Localize(trophy) ?? trophy) +
                    " " + Mathf.FloorToInt(next) + "/" + pointsCap.Value);
        }

        [HarmonyPatch(typeof(Skills), "CheatRaiseSkill")]
        private static class SpeciesRaiseCommand
        {
            [HarmonyPrefix]
            private static bool Prefix(string name, float value)
            {
                if (!allowCheats.Value || activePlayer == null || string.IsNullOrEmpty(name)) return true;
                if (string.Equals(name, "all", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var token in Points.Keys.ToList()) SetPoints(token, Points[token] + value);
                    return true; // Let Valheim raise its ordinary skills, too.
                }
                if (!DisplayToTrophy.TryGetValue(NormalizeLabel(name.Replace('_', ' ')), out var trophy)) return true;
                Points.TryGetValue(trophy, out var current);
                SetPoints(trophy, current + value);
                return false;
            }
        }

        [HarmonyPatch(typeof(Skills), "CheatResetSkill")]
        private static class SpeciesResetCommand
        {
            [HarmonyPrefix]
            private static bool Prefix(string name)
            {
                if (!allowCheats.Value || activePlayer == null || string.IsNullOrEmpty(name)) return true;
                if (string.Equals(name, "all", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var token in Points.Keys.ToList()) SetPoints(token, 0f);
                    return true;
                }
                if (!DisplayToTrophy.TryGetValue(NormalizeLabel(name.Replace('_', ' ')), out var trophy)) return true;
                SetPoints(trophy, 0f);
                return false;
            }
        }

        private static void SetPoints(string trophy, float points)
        {
            if (string.IsNullOrEmpty(trophy) || float.IsNaN(points) || float.IsInfinity(points)) return;
            Points[trophy] = Mathf.Clamp(points, 0f, pointsCap.Value);
            Publish(trophy);
            Save();
        }

        [HarmonyPatch(typeof(InventoryGui), "UpdateTrophyList")]
        private static class TrophyLabels
        {
            [HarmonyPostfix]
            private static void Postfix(List<GameObject> ___m_trophyList)
            {
                if (!enabled.Value || ___m_trophyList == null) return;
                foreach (var trophy in ___m_trophyList)
                {
                    if (trophy == null) continue;
                    var title = trophy.transform.Find("name");
                    var description = trophy.transform.Find("description");
                    if (title == null || description == null) continue;
                    var name = title.GetComponent<TMP_Text>();
                    var text = description.GetComponent<TMP_Text>();
                    if (name == null || text == null) continue;
                    if (!DisplayToTrophy.TryGetValue(NormalizeLabel(name.text), out var key)) continue;
                    if (!Points.TryGetValue(key, out var value)) continue;
                    string prefix = "Pogromca " + Mathf.FloorToInt(value) + "/" + pointsCap.Value;
                    string detail = text.text;
                    while (detail.StartsWith("Pogromca ", StringComparison.Ordinal))
                    {
                        int next = detail.IndexOf('\n');
                        if (next < 0) break;
                        detail = detail.Substring(next + 1);
                    }
                    text.text = prefix + "\n" + detail;
                }
            }
        }

        internal static void Clear()
        {
            activePlayer = null;
            publishedTo = ZDOID.None;
            Points.Clear();
            CreatureToTrophy.Clear();
            DisplayToTrophy.Clear();
            deathContext = default;
            ragdollContext = default;
        }
    }
}
