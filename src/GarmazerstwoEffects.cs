using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    // Stat changes belong to the eaten food instance, never to the shared prefab.
    // Optional effects are patched only if the exact 1.0 method exists at runtime.
    internal static class GarmazerstwoEffects
    {
        private sealed class FoodMarker { }
        private sealed class State
        {
            internal readonly Dictionary<string, float> Buffs = new Dictionary<string, float>(StringComparer.Ordinal);
            internal float HpRegen, StaminaRegen, EitrRegen;
            internal float NextRefresh, NextRegen;
        }
        private static readonly ConditionalWeakTable<Player, State> States = new ConditionalWeakTable<Player, State>();
        private static readonly ConditionalWeakTable<object, FoodMarker> EnhancedFood =
            new ConditionalWeakTable<object, FoodMarker>();
        private static readonly FieldInfo Foods = AccessTools.Field(typeof(Player), "m_foods");
        private static readonly FieldInfo FoodItem = AccessTools.Field(typeof(Player.Food), "m_item");
        private static readonly FieldInfo FoodHealth = AccessTools.Field(typeof(Player.Food), "m_health");
        private static readonly FieldInfo FoodStamina = AccessTools.Field(typeof(Player.Food), "m_stamina");
        private static readonly FieldInfo FoodEitr = AccessTools.Field(typeof(Player.Food), "m_eitr");
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
            var foods = FoodEntries(player);
            if (foods == null) return;
            foreach (var food in foods)
            {
                var item = Item(food);
                var data = Garmazerstwo.Read(item);
                if (data == null) continue;
                if (!string.IsNullOrEmpty(data.Buff))
                {
                    if (!state.Buffs.TryGetValue(data.Buff, out float previous) ||
                        previous < data.BuffValue) state.Buffs[data.Buff] = data.BuffValue;
                }
                state.HpRegen += data.HpRegen;
                state.StaminaRegen += data.StaminaRegen;
                state.EitrRegen += data.EitrRegen;
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

        // EatFood only returns true if the game actually accepted a new meal.
        [HarmonyPatch(typeof(Player), "EatFood")]
        private static class EatFood
        {
            private static void Prefix(Player __instance, ItemDrop.ItemData item)
            {
                aboutToEat = ReferenceEquals(__instance, Player.m_localPlayer) ? item : null;
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
                foreach (var entry in entries)
                {
                    if (!ReferenceEquals(Item(entry), eaten) || EnhancedFood.TryGetValue(entry, out _)) continue;
                    var shared = eaten.m_shared;
                    FoodHealth.SetValue(entry, shared.m_food * (1f + data.CookPercent) + data.Hp);
                    FoodStamina.SetValue(entry, shared.m_foodStamina * (1f + data.CookPercent) + data.Stamina);
                    FoodEitr.SetValue(entry, (shared.m_foodEitr * (1f + data.CookPercent) + data.Eitr) *
                        (1f + (data.Buff == "max_eitr" ? data.BuffValue : 0f)));
                    EnhancedFood.Add(entry, new FoodMarker());
                    break;
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
                    if (state.HpRegen > 0f && Heal != null)
                        Heal.Invoke(__instance, new object[] { state.HpRegen / 10f, true });
                    if (state.StaminaRegen > 0f && AddStamina != null)
                        AddStamina.Invoke(__instance, new object[] { state.StaminaRegen });
                    if (state.EitrRegen > 0f && AddEitr != null)
                        AddEitr.Invoke(__instance, new object[] { state.EitrRegen });
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
            private static void Prefix(Player __instance, Skills.SkillType skill, ref float factor)
            {
                if (!ReferenceEquals(__instance, Player.m_localPlayer) ||
                    skill == Garmazerstwo.SkillType) return;
                factor *= 1f + Buff(__instance, "experience");
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