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
        private const string KillSaveKey = "sirroh.skillsaddon.headhunter.kills.v1";
        private const string KillRpc = "sirroh.skillsaddon.headhunter.kill.v1";
        private const string KillAwardedZdo = "sirroh.skillsaddon.headhunter.counted.v1";
        private const string AwardRpc = "sirroh.skillsaddon.slayer.award.v1";
        private const string DotOwnerKey = "sirroh.skillsaddon.slayer.dot.owner.v1";
        private const string DotLevelKey = "sirroh.skillsaddon.slayer.dot.level.v1";
        private static readonly Dictionary<string, int> KillCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<ZDOID> CountedDeaths = new HashSet<ZDOID>();
        private static readonly Queue<ZDOID> CountedDeathOrder = new Queue<ZDOID>();
        private static ConfigEntry<int> commonInterval, rareInterval;
        private static ConfigEntry<string> rarePrefabs;
        private static HashSet<string> rareExact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
        private static readonly List<string> ProgressKeys = new List<string>();
        private static int page;
        private static bool progressDirty = true;
        private static ConfigEntry<bool> showKillMessages, showInventoryProgress;
        private static Context deathContext, ragdollContext;

        private enum AwardMode { Random, All, Distribute, KillingBlow }

        private struct Context
        {
            internal long PeerId;
            internal int Level;
        }

        internal static void Configure(ConfigFile config)
        {
            enabled = config.Bind("Łowca Głów", "Enabled", true, "Progres gatunków za trofea i zabójstwa.");
            showKillMessages = config.Bind("Łowca Głów", "PokazPostepZabojstw", true,
                "Pokazuj postęp do następnego punktu po każdym zabójstwie.");
            showInventoryProgress = config.Bind("Łowca Głów", "PanelPostepuWekwipunku", true,
                "Pokazuj panel Łowcy Głów przy otwartym ekwipunku; obejmuje też stworzenia bez trofeów.");
            pointsCap = config.Bind("Łowca Głów", "SpeciesCap", 100,
                new ConfigDescription("Limit punktów na każdy gatunek.", new AcceptableValueRange<int>(1, 1000)));
            damageCap = config.Bind("Łowca Głów", "DamageAtCapPercent", 100,
                new ConfigDescription("Maksymalny bonus do obrażeń przeciw gatunkowi.", new AcceptableValueRange<int>(0, 200)));
            physical = config.Bind("Łowca Głów", "PhysicalDamage", true, "Premia do obrażeń fizycznych.");
            elemental = config.Bind("Łowca Głów", "ElementalDamage", false, "Premia do obrażeń ogniowych/mrozu/błyskawic.");
            other = config.Bind("Łowca Głów", "OtherDamage", false, "Premia do trucizny i ducha.");
            useCreatureLevel = config.Bind("Łowca Głów", "ScaleByStars", false, "Więcej punktów za wyższy poziom (gwiazdki CLLC).");
            tamesKill = config.Bind("Łowca Głów", "TamedKillsCount", true, "Zalicza zabójstwa własnych oswojonych stworzeń.");
            tamesBuff = config.Bind("Łowca Głów", "TamedCreaturesGetBonus", false, "Chowańce korzystają z premii właściciela przeciw gatunkowi.");
            loseOnDeath = config.Bind("Łowca Głów", "LossOnDeath", true, "Utrata części nieutrwalonych punktów po śmierci.");
            allowCheats = config.Bind("Łowca Głów", "AllowCheats", false, "Sterowanie progresją gatunków poleceniami raiseskill/resetskill.");
            radius = config.Bind("Łowca Głów", "TrophyDistance", 30f,
                new ConfigDescription("Promień rozdziału punktów w trybie drużynowym.", new AcceptableValueRange<float>(1, 200)));
            bossPoints = config.Bind("Łowca Głów", "BossBonusPoints", 3,
                new ConfigDescription("Dodatkowe punkty za trofeum bossa.", new AcceptableValueRange<int>(0, 100)));
            awardMode = config.Bind("Łowca Głów", "PointsAssignment", AwardMode.Random,
                "Random/All/Distribute/KillingBlow. Domyślnie ostatni cios.");
            commonInterval = config.Bind("Łowca Głów", "ZabójstwaNaPunkt", 10,
                new ConfigDescription("Liczba zabójstw jednego gatunku potrzebna do przyznania +1 punktu, niezależnie od trofeów.",
                    new AcceptableValueRange<int>(1, 100)));
            rareInterval = config.Bind("Łowca Głów", "DużeStworzeniaNaPunkt", 5,
                new ConfigDescription("Liczba zabójstw dużego/rzadkiego gatunku potrzebna do +1 punktu.",
                    new AcceptableValueRange<int>(1, 100)));
            rarePrefabs = config.Bind("Łowca Głów", "DużeStworzeniaPrefaby",
                "FallenValkyrie,Serpent,Gjall,Morgen,Bjorn,Bjorn_sleeping,Unbjorn,Troll,Abomination,StoneGolem,SeekerBrute,GoblinBrute,Lox",
                "Lista nazw prefabów (oddzielona przecinkami); każdy ma próg 5 zamiast 10. Bossowie automatycznie korzystają z progu 5.");
            ParseRarity(rarePrefabs.Value);
            rarePrefabs.SettingChanged += (_, __) => ParseRarity(rarePrefabs.Value);
            serverAuthoritative = config.Bind("Łowca Głów", "OnlyHostMayAwardPoints", false,
                "Odrzuca nieautoryzowane klient-klient RPC z punktami. Właścicielem łupów powinien być host.");
        }

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegisterRpc
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                ZRoutedRpc.instance?.Register<string, float>(AwardRpc, ReceivePoints);
                ZRoutedRpc.instance?.Register<string, string>(KillRpc, ReceiveKill);
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
                KillCounts.Clear();
                progressDirty = true;
                if (!__instance.m_customData.TryGetValue(SaveKey, out var contents))
                    __instance.m_customData.TryGetValue(LegacySaveKey, out contents);
                Parse(contents);
                if (__instance.m_customData.TryGetValue(KillSaveKey, out var killContents))
                    ParseKills(killContents);
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

        // Original Slayer Skills rebuilt its localized trophy names after
        // language changes. Keep this behavior or Polish trophy rows do not match.
        [HarmonyPatch(typeof(Localization), "SetLanguage")]
        private static class RefreshLocalizedTrophies
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
            // Monsters without trophy items still qualify for per-species kill progress.
            return "@prefab:" + name;
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
            activePlayer.m_customData[KillSaveKey] = string.Join(",",
                KillCounts.OrderBy(k => k.Key, StringComparer.Ordinal)
                    .Select(k => k.Key + ":" + k.Value.ToString(CultureInfo.InvariantCulture)));
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

        private static void ParseKills(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            foreach (string entry in raw.Split(','))
            {
                int at = entry.LastIndexOf(':');
                if (at <= 0 || at >= entry.Length - 1) continue;
                string token = entry.Substring(0, at);
                if (int.TryParse(entry.Substring(at + 1), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int count) && count >= 0)
                    KillCounts[token] = Math.Min(count, 100000000);
            }
        }

        private static void ParseRarity(string entries)
        {
            rareExact = new HashSet<string>(
                (entries ?? "").Split(',').Select(n => n.Trim()).Where(n => n.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }

        private static int RequiredKills(string prefab)
        {
            return rareExact.Contains(prefab) ? rareInterval.Value : commonInterval.Value;
        }

        // Kept separate from DropItems so every death counts even if RNG drops no trophy.
        private static void CountKill(Character character)
        {
            if (!enabled.Value || character == null || character.IsPlayer() ||
                character.IsTamed() || ZRoutedRpc.instance == null) return;
            var view = ValheimAccess.View(character);
            if (view == null || !view.IsOwner()) return;
            var zdo = view.GetZDO();
            if (zdo == null || !zdo.IsValid() || zdo.GetBool(KillAwardedZdo, false)) return;
            ZDOID id = character.GetZDOID();
            if (id == ZDOID.None || CountedDeaths.Contains(id)) return;
            long peer = AttackerPeer(character);
            if (peer == 0L) return; // No credited player; don't mark or award an unowned death.
            string species = TrophyFor(character);
            if (string.IsNullOrEmpty(species)) return;
            string prefab = character.gameObject.name.Replace("(Clone)", "").Trim();
            if (species.Length > 200 || prefab.Length > 150) return;
            zdo.Set(KillAwardedZdo, true);
            CountedDeaths.Add(id);
            CountedDeathOrder.Enqueue(id);
            while (CountedDeathOrder.Count > 2048)
                CountedDeaths.Remove(CountedDeathOrder.Dequeue());
            // Each kill goes only to its actual owner, not to every nearby player.
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, KillRpc, species, character.IsBoss() ? "@boss:" + prefab : prefab);
        }

        [HarmonyPatch(typeof(Character), "OnDeath")]
        private static class CharacterDeathCounter
        {
            [HarmonyPrefix]
            private static void Prefix(Character __instance) => CountKill(__instance);
        }

        private static void ReceiveKill(long sender, string species, string prefab)
        {
            if (!enabled.Value || activePlayer == null || string.IsNullOrEmpty(species) ||
                string.IsNullOrEmpty(prefab) || species.Length > 200 || prefab.Length > 160) return;
            if (serverAuthoritative.Value && sender != ValheimAccess.ServerPeerID()) return;
            KillCounts.TryGetValue(species, out int previous);
            if (previous >= 100000000) return;
            int next = previous + 1;
            KillCounts[species] = next;
            progressDirty = true;
            int needed = prefab.StartsWith("@boss:", StringComparison.Ordinal)
                ? rareInterval.Value : RequiredKills(prefab);
            if (next % needed == 0)
            {
                Points.TryGetValue(species, out float current);
                SetPoints(species, current + 1f);
                if (current < pointsCap.Value)
                    activePlayer.Message(MessageHud.MessageType.TopLeft,
                        "Łowca Głów: " + DisplaySpecies(species) + " +" + 1 +
                        " (" + next + " zabójstw)");
            }
            else Save(); // Persist remainder across character logout/restart.
            if (showKillMessages.Value && next % needed != 0)
                activePlayer.Message(MessageHud.MessageType.TopLeft,
                    "Łowca Głów: " + DisplaySpecies(species) +
                    " — " + (next % needed) + "/" + needed + " zabójstw" +
                    " | poziom " + Mathf.FloorToInt(Points.TryGetValue(species, out var score) ? score : 0f) +
                    "/" + pointsCap.Value);
        }

        private static string DisplaySpecies(string token)
        {
            if (token.StartsWith("@prefab:", StringComparison.Ordinal))
                return token.Substring("@prefab:".Length);
            return NormalizeLabel(Localization.instance?.Localize(token) ?? token);
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
            private static void Prefix(CharacterDrop __instance)
            {
                var creature = __instance?.GetComponent<Character>();
                deathContext = Capture(creature);
                CountKill(creature); // Fallback for creatures that bypass Character.OnDeath.
            }
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
            progressDirty = true;
            Save();
            Publish(trophy);
            if (Mathf.FloorToInt(next) > Mathf.FloorToInt(old))
                activePlayer.Message(MessageHud.MessageType.TopLeft,
                    "Łowca Głów: " + DisplaySpecies(trophy) +
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
            progressDirty = true;
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
                if (DisplayToTrophy.Count == 0 && ZNetScene.instance != null) BuildMap();
                foreach (var trophy in ___m_trophyList)
                {
                    if (trophy == null) continue;
                    var title = trophy.transform.Find("name");
                    var description = trophy.transform.Find("description");
                    if (title == null || description == null) continue;
                    var name = title.GetComponent<TMP_Text>();
                    var text = description.GetComponent<TMP_Text>();
                    if (name == null || text == null) continue;
                    // The original patch only decorated trophies after the first point.
                    // Always show 0/Cap, and show the next kill threshold on each trophy.
                    if (!DisplayToTrophy.TryGetValue(NormalizeLabel(name.text), out var key)) continue;
                    Points.TryGetValue(key, out var value);
                    KillCounts.TryGetValue(key, out int kills);
                    int threshold = RequiredKillsForTrophy(key);
                    string prefix = "Łowca Głów " + Mathf.FloorToInt(value) + "/" + pointsCap.Value +
                        " • " + (kills % threshold) + "/" + threshold + " zabójstw";
                    string detail = text.text;
                    while ((detail.StartsWith("Łowca Głów ", StringComparison.Ordinal) ||
                            detail.StartsWith("Pogromca ", StringComparison.Ordinal)))
                    {
                        int next = detail.IndexOf('\n');
                        if (next < 0) break;
                        detail = detail.Substring(next + 1);
                    }
                    text.text = prefix + "\n" + detail;
                }
            }
        }

        private static int RequiredKillsForTrophy(string token)
        {
            foreach (var entry in CreatureToTrophy)
                if (entry.Value == token && rareExact.Contains(entry.Key))
                    return Math.Max(1, rareInterval.Value);
            return Math.Max(1, commonInterval.Value);
        }

        internal static void DrawProgress()
        {
            if (!enabled.Value || !showInventoryProgress.Value ||
                activePlayer == null || !InventoryGui.IsVisible()) return;
            if (progressDirty)
            {
                ProgressKeys.Clear();
                foreach (var key in KillCounts.Keys) ProgressKeys.Add(key);
                foreach (var key in Points.Keys)
                    if (!ProgressKeys.Contains(key)) ProgressKeys.Add(key);
                ProgressKeys.Sort((a, b) =>
                {
                    KillCounts.TryGetValue(a, out int ac);
                    KillCounts.TryGetValue(b, out int bc);
                    int order = bc.CompareTo(ac);
                    return order != 0 ? order : StringComparer.Ordinal.Compare(a, b);
                });
                progressDirty = false;
            }
            int maxPage = Math.Max(0, (ProgressKeys.Count - 1) / 7);
            page = Mathf.Clamp(page, 0, maxPage);
            float width = 350f;
            var rect = new Rect(Mathf.Max(8, Screen.width - width - 14),
                Mathf.Max(8, Screen.height - 334), width, 316);
            GUI.Box(rect, "");
            GUI.Label(new Rect(rect.x + 12, rect.y + 9, 320, 26), "ŁOWCA GŁÓW  •  POSTĘP");
            if (ProgressKeys.Count == 0)
            {
                GUI.Label(new Rect(rect.x + 12, rect.y + 47, width - 24, 44),
                    "Brak zapisanych zabójstw.\nPokonaj stworzenie, aby zacząć śledzenie.");
                return;
            }
            for (int i = page * 7, row = 0; i < ProgressKeys.Count && row < 7; ++i, ++row)
            {
                string key = ProgressKeys[i];
                Points.TryGetValue(key, out float level);
                KillCounts.TryGetValue(key, out int total);
                int interval = RequiredKillsForTrophy(key);
                string title = DisplaySpecies(key);
                if (title.Length > 22) title = title.Substring(0, 19) + "...";
                GUI.Label(new Rect(rect.x + 12, rect.y + 42 + row * 33, width - 24, 31),
                    title + ": " + Mathf.FloorToInt(level) + "/" + pointsCap.Value +
                    "  |  " + (total % interval) + "/" + interval +
                    "  (" + total + " zab.)");
            }
            if (maxPage > 0)
            {
                if (GUI.Button(new Rect(rect.x + 12, rect.y + 274, 40, 28), "<")) page = Math.Max(0, page - 1);
                GUI.Label(new Rect(rect.x + 70, rect.y + 277, 135, 25), "Strona " + (page + 1) + "/" + (maxPage + 1));
                if (GUI.Button(new Rect(rect.x + width - 52, rect.y + 274, 40, 28), ">")) page = Math.Min(maxPage, page + 1);
            }
        }

        internal static void Clear()
        {
            activePlayer = null;
            publishedTo = ZDOID.None;
            Points.Clear();
            KillCounts.Clear();
            ProgressKeys.Clear();
            progressDirty = true;
            page = 0;
            CountedDeaths.Clear();
            CountedDeathOrder.Clear();
            CreatureToTrophy.Clear();
            DisplayToTrophy.Clear();
            deathContext = default;
            ragdollContext = default;
        }
    }
}
