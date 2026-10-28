using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    // Stat changes belong to the eaten food instance, never to the shared prefab.
    // Optional effects are patched only if the exact 1.0 method exists at runtime.
    internal static class GarmazerstwoEffects
    {
        private sealed class FoodRecord { internal Garmazerstwo.Food Data; }
        private sealed class State
        {
            internal readonly Dictionary<string, float> Buffs = new Dictionary<string, float>(StringComparer.Ordinal);
            internal float HpRegen, StaminaRegen, EitrRegen, VanillaHpRegen, VanillaEitr;
            internal float NextRefresh, NextRegen;
        }
        private static readonly ConditionalWeakTable<Player, State> States = new ConditionalWeakTable<Player, State>();
        private static readonly ConditionalWeakTable<object, FoodRecord> ActiveFood =
            new ConditionalWeakTable<object, FoodRecord>();
        private const string SaveKey = "sirroh.skillsaddon.garmazerstwo.activefood.v1";
        private static readonly FieldInfo Foods = AccessTools.Field(typeof(Player), "m_foods");
        private static readonly FieldInfo FoodItem = AccessTools.Field(typeof(Player.Food), "m_item");
        private static readonly FieldInfo FoodHealth = AccessTools.Field(typeof(Player.Food), "m_health");
        private static readonly FieldInfo FoodStamina = AccessTools.Field(typeof(Player.Food), "m_stamina");
        private static readonly FieldInfo FoodEitr = AccessTools.Field(typeof(Player.Food), "m_eitr");
        private static readonly FieldInfo FoodTime = AccessTools.Field(typeof(Player.Food), "m_time");
        private static readonly MethodInfo AddStamina = AccessTools.Method(typeof(Player), "AddStamina", new[] { typeof(float) });
        private static readonly MethodInfo AddEitr = AccessTools.Method(typeof(Player), "AddEitr", new[] { typeof(float) });
        private static readonly MethodInfo Heal = AccessTools.Method(typeof(Character), "Heal",
            new[] { typeof(float), typeof(bool) });
        private static ItemDrop.ItemData aboutToEat;
        private static bool warnedFood;

        internal static float Scale(string name)
        {
            switch (name)
            {
                case "defence": return .5f;
                case "speed": return .24f;
                case "stamina_cost": return .4f;
                case "jump": return .3f;
                case "swim": return .4f;
                case "eitr_cost": return .4f;
                case "magic_damage": return .7f;
                case "eitr_regen": return .6f;
                case "hp_regen": return .6f;
                case "max_eitr": return .4f;
                case "taming": return .6f;
                case "experience": return .3f;
                default: return 1f;
            }
        }

        internal static void Install(Harmony harmony)
        {
            TryPatch(harmony, typeof(Player), "GetJogSpeedFactor", nameof(SpeedFactor), false);
            TryPatch(harmony, typeof(Player), "GetRunSpeedFactor", nameof(SpeedFactor), false);
            TryPatch(harmony, typeof(Player), "GetSwimSpeedFactor", nameof(SwimFactor), false);
            TryPatch(harmony, typeof(Player), "UseStamina", nameof(StaminaCost), true);
            TryPatch(harmony, typeof(Player), "UseEitr", nameof(EitrCost), true);
            TryPatch(harmony, typeof(Player), "Jump", nameof(JumpBonus), false);
            TryPatch(harmony, typeof(Tameable), "DecreaseRemainingTime", nameof(TamingBonus), true);
        }

        private static void TryPatch(Harmony harmony, Type owner, string name, string callback, bool prefix)
        {
            var method = AccessTools.Method(owner, name);
            if (method == null) return;
            try
            {
                var hook = new HarmonyMethod(AccessTools.Method(typeof(GarmazerstwoEffects), callback));
                if (prefix) harmony.Patch(method, prefix: hook);
                else harmony.Patch(method, postfix: hook);
            }
            catch (Exception e) { Plugin.Instance.Warning("Garmażerstwo: niezgodny cel " + name + ": " + e.GetType().Name); }
        }

        private static IEnumerable FoodEntries(Player player)
        {
            return player == null ? null : Foods?.GetValue(player) as IEnumerable;
        }

        private static ItemDrop.ItemData Item(object food)
        {
            return food == null ? null : FoodItem?.GetValue(food) as ItemDrop.ItemData;
        }

        private static Garmazerstwo.Food Data(object food)
        {
            if (food == null) return null;
            if (ActiveFood.TryGetValue(food, out var record)) return record.Data;
            return Garmazerstwo.Read(Item(food));
        }

        private static void Attach(object food, Garmazerstwo.Food data)
        {
            if (food == null || data == null) return;
            ActiveFood.GetOrCreateValue(food).Data = data;
        }

        // Vanilla reconstructs active foods from prefab names at login and discards item custom data.
        // Keep separate per-character records so buffs and cooking levels survive a relog.
        private static void SaveFood(Player player)
        {
            if (player?.m_customData == null) return;
            var parts = new System.Collections.Generic.List<string>();
            foreach (var entry in FoodEntries(player) ?? new object[0])
            {
                var item = Item(entry);
                var data = Data(entry);
                if (item?.m_shared == null || data == null) continue;
                parts.Add(Uri.EscapeDataString(item.m_shared.m_name) + "=" +
                          Uri.EscapeDataString(Garmazerstwo.Serialize(data)));
            }
            if (parts.Count > 0)
                player.m_customData[SaveKey] = string.Join(";", parts);
            else player.m_customData.Remove(SaveKey);
        }

        private static void LoadFood(Player player)
        {
            if (player?.m_customData == null ||
                !player.m_customData.TryGetValue(SaveKey, out string raw) ||
                string.IsNullOrEmpty(raw) || raw.Length > 18000) return;
            var map = new Dictionary<string, Garmazerstwo.Food>(StringComparer.Ordinal);
            foreach (string pair in raw.Split(';'))
            {
                int at = pair.IndexOf('=');
                if (at <= 0) continue;
                try
                {
                    string key = Uri.UnescapeDataString(pair.Substring(0, at));
                    var data = Garmazerstwo.Parse(Uri.UnescapeDataString(pair.Substring(at + 1)));
                    if (data != null) map[key] = data;
                }
                catch (Exception) { }
            }
            foreach (var entry in FoodEntries(player) ?? new object[0])
            {
                var item = Item(entry);
                if (item?.m_shared != null && map.TryGetValue(item.m_shared.m_name, out var data))
                    Attach(entry, data);
            }
            if (States.TryGetValue(player, out var state)) state.NextRefresh = 0f;
        }

        [HarmonyPatch(typeof(Player), "Save")]
        private static class FoodSavePatch
        {
            private static void Prefix(Player __instance) => SaveFood(__instance);
        }

        [HarmonyPatch(typeof(Player), "Load")]
        private static class FoodLoadPatch
        {
            private static void Postfix(Player __instance) => LoadFood(__instance);
        }

        private static void ApplyFoodValues(Player player)
        {
            if (FoodHealth == null || FoodStamina == null || FoodEitr == null ||
                FoodTime == null) return;
            foreach (var entry in FoodEntries(player) ?? new object[0])
            {
                var item = Item(entry);
                var data = Data(entry);
                if (item?.m_shared == null || data == null) continue;
                float remaining = Convert.ToSingle(FoodTime.GetValue(entry));
                float lifetime = item.m_shared.m_foodBurnTime;
                if (lifetime <= 0f) continue;
                float multiplier = Mathf.Pow(Mathf.Clamp01(remaining / lifetime), .3f);
                float hp = item.m_shared.m_food * (1f + data.CookPercent) + data.Hp;
                float stamina = item.m_shared.m_foodStamina * (1f + data.CookPercent) + data.Stamina;
                float eitr = item.m_shared.m_foodEitr * (1f + data.CookPercent) + data.Eitr;
                if (data.Buff == "max_eitr") eitr *= 1f + data.BuffValue;
                FoodHealth.SetValue(entry, hp * multiplier);
                FoodStamina.SetValue(entry, stamina * multiplier);
                FoodEitr.SetValue(entry, eitr * multiplier);
            }
        }

        [HarmonyPatch(typeof(Player), "GetTotalFoodValue")]
        private static class FoodTotalPatch
        {
            private static void Prefix(Player __instance)
            {
                if (ReferenceEquals(__instance, Player.m_localPlayer))
                    ApplyFoodValues(__instance);
            }
        }

        private static float Buff(Player player, string key)
        {
            if (player == null || Garmazerstwo.Enabled?.Value != true) return 0f;
            State state = States.GetOrCreateValue(player);
            if (Time.time >= state.NextRefresh) Refresh(player, state);
            return state.Buffs.TryGetValue(key, out float value) ? value : 0f;
        }

        private static void Refresh(Player player, State state)
        {
            state.NextRefresh = Time.time + .4f;
            state.Buffs.Clear();
            state.HpRegen = state.StaminaRegen = state.EitrRegen = 0f;
            state.VanillaHpRegen = state.VanillaEitr = 0f;
            var foods = FoodEntries(player);
            if (foods == null) return;
            foreach (var food in foods)
            {
                var item = Item(food);
                var data = Data(food);
                if (data == null) continue;
                if (!string.IsNullOrEmpty(data.Buff))
                {
                    if (!state.Buffs.TryGetValue(data.Buff, out float previous) ||
                        previous < data.BuffValue) state.Buffs[data.Buff] = data.BuffValue;
                }
                state.HpRegen += data.HpRegen;
                state.StaminaRegen += data.StaminaRegen;
                state.EitrRegen += data.EitrRegen;
                state.VanillaHpRegen += item.m_shared.m_foodRegen;
                state.VanillaEitr += item.m_shared.m_foodEitr;
            }
        }

        private static void SpeedFactor(Player __instance, ref float __result)
        {
            if (ReferenceEquals(__instance, Player.m_localPlayer))
                __result *= 1f + Buff(__instance, "speed");
        }

        private static void SwimFactor(Player __instance, ref float __result)
        {
            if (ReferenceEquals(__instance, Player.m_localPlayer))
                __result *= 1f + Buff(__instance, "swim");
        }

        private static void StaminaCost(Player __instance, ref float __0)
        {
            if (ReferenceEquals(__instance, Player.m_localPlayer))
                __0 *= 1f - Buff(__instance, "stamina_cost");
        }

        private static void EitrCost(Player __instance, ref float __0)
        {
            if (ReferenceEquals(__instance, Player.m_localPlayer))
                __0 *= 1f - Buff(__instance, "eitr_cost");
        }

        // Jump height scales only the initial upward velocity, never flight/falling velocity.
        private static void JumpBonus(Player __instance)
        {
            if (!ReferenceEquals(__instance, Player.m_localPlayer)) return;
            float value = Buff(__instance, "jump");
            if (value <= 0f) return;
            var bodyField = AccessTools.Field(typeof(Character), "m_body");
            var body = bodyField?.GetValue(__instance) as Rigidbody ??
                       __instance.GetComponent<Rigidbody>();
            if (body == null || body.linearVelocity.y <= 0f) return;
            Vector3 velocity = body.linearVelocity;
            velocity.y *= Mathf.Sqrt(1f + value); // Height ~ squared jump velocity.
            body.linearVelocity = velocity;
        }

        // Vanilla calls this only when the creature is fed, calm and owned by this client.
        // Never rewrite the tame ZDO or alter already-tamed creatures.
        private static void TamingBonus(Tameable __instance, ref float __0)
        {
            var player = Player.m_localPlayer;
            if (player == null || __instance == null || __0 <= 0f ||
                Vector3.Distance(player.transform.position, __instance.transform.position) > 60f)
                return;
            float value = Buff(player, "taming");
            if (value > 0f) __0 *= 1f + value;
        }

        // EatFood only returns true if the game actually accepted a new meal.
        [HarmonyPatch(typeof(Player), "EatFood")]
        private static class EatFood
        {
            private static void Prefix(Player __instance, ItemDrop.ItemData __0)
            {
                aboutToEat = ReferenceEquals(__instance, Player.m_localPlayer) ? __0 : null;
            }
            private static void Postfix(Player __instance, bool __result)
            {
                ItemDrop.ItemData eaten = aboutToEat;
                aboutToEat = null;
                if (!__result || eaten == null || Garmazerstwo.Enabled?.Value != true) return;
                var data = Garmazerstwo.Read(eaten);
                if (data == null) return;
                var entries = FoodEntries(__instance);
                if (entries == null || FoodHealth == null || FoodStamina == null || FoodEitr == null)
                {
                    if (!warnedFood) {
                        Plugin.Instance.Warning("Garmażerstwo: nie znaleziono struktury aktywnych posiłków; premie statystyk wyłączone.");
                        warnedFood = true;
                    }
                    return;
                }
                object chosen = null;
                foreach (var entry in entries)
                {
                    var current = Item(entry);
                    if (current == null || current.m_shared == null) continue;
                    if (ReferenceEquals(current, eaten)) { chosen = entry; break; }
                    if (current.m_shared.m_name == eaten.m_shared.m_name) chosen = entry;
                }
                if (chosen != null)
                {
                    Attach(chosen, data);
                    ApplyFoodValues(__instance);
                    SaveFood(__instance);
                }
                States.GetOrCreateValue(__instance).NextRefresh = 0f;
            }
        }

        // Additional regeneration from all five inherited spices.
        [HarmonyPatch(typeof(Player), "Update")]
        private static class RegenPulse
        {
            private static void Postfix(Player __instance)
            {
                if (!ReferenceEquals(__instance, Player.m_localPlayer) || Garmazerstwo.Enabled?.Value != true)
                    return;
                State state = States.GetOrCreateValue(__instance);
                if (Time.time >= state.NextRefresh) Refresh(__instance, state);
                if (Time.time < state.NextRegen) return;
                state.NextRegen = Time.time + 1f;
                try
                {
                    float extraHealth = state.HpRegen;
                    if (state.Buffs.TryGetValue("hp_regen", out float hpBonus))
                        extraHealth += state.VanillaHpRegen * hpBonus;
                    if (extraHealth > 0f && Heal != null)
                        Heal.Invoke(__instance, new object[] { extraHealth / 10f, true });
                    if (state.StaminaRegen > 0f && AddStamina != null)
                        AddStamina.Invoke(__instance, new object[] { state.StaminaRegen });
                    float extraEitr = state.EitrRegen;
                    if (state.Buffs.TryGetValue("eitr_regen", out float eitrBonus))
                        extraEitr += state.VanillaEitr * .033f * eitrBonus;
                    if (extraEitr > 0f && AddEitr != null)
                        AddEitr.Invoke(__instance, new object[] { extraEitr });
                }
                catch (Exception) { /* Never break vanilla Player.Update on a missing optional API. */ }
            }
        }

        [HarmonyPatch(typeof(Character), "Damage", new Type[] { typeof(HitData) })]
        private static class DamageAndDefence
        {
            private static void Prefix(Character __instance, HitData hit)
            {
                if (hit == null || Garmazerstwo.Enabled?.Value != true) return;
                var attacker = hit.GetAttacker() as Player;
                if (ReferenceEquals(attacker, Player.m_localPlayer))
                {
                    float physical = Buff(attacker, "physical_damage");
                    float magic = Buff(attacker, "magic_damage");
                    hit.m_damage.m_blunt *= 1f + physical;
                    hit.m_damage.m_slash *= 1f + physical;
                    hit.m_damage.m_pierce *= 1f + physical;
                    hit.m_damage.m_fire *= 1f + magic;
                    hit.m_damage.m_frost *= 1f + magic;
                    hit.m_damage.m_lightning *= 1f + magic;
                    hit.m_damage.m_poison *= 1f + magic;
                    hit.m_damage.m_spirit *= 1f + magic;
                }
                if (ReferenceEquals(__instance, Player.m_localPlayer))
                    hit.m_damage.Modify(1f - Buff((Player)__instance, "defence"));
            }
        }

        [HarmonyPatch(typeof(Player), "RaiseSkill")]
        private static class ExperienceBonus
        {
            private static void Prefix(Player __instance, Skills.SkillType __0, ref float __1)
            {
                if (!ReferenceEquals(__instance, Player.m_localPlayer) ||
                    __0 == Garmazerstwo.SkillType) return;
                __1 *= 1f + Buff(__instance, "experience");
            }
        }

        [HarmonyPatch(typeof(Skills), "LowerAllSkills")]
        private static class KeepSkills
        {
            private static bool Prefix()
            {
                return Player.m_localPlayer == null ||
                       Buff(Player.m_localPlayer, "no_skill_loss") <= 0f;
            }
        }
    }
}