using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    // Versioned, per-item food records. No SharedData modification and no global prefab stat writes.
    internal static class Garmazerstwo
    {
        internal const string Key = "sirroh.skillsaddon.garmazerstwo.food.v1";
        internal static Skills.SkillType SkillType;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> XpCook, XpSpice, CookMax, StreakMax;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static readonly System.Random Random = new System.Random();
        internal static bool CookingCraft;
        internal static int CookingAmount;
        private static int cookStreak;
        private static float lastCookTime;
        private static MethodInfo getTooltip;

        internal sealed class Spice
        {
            internal string Source;
            internal float Hp, Stamina, Eitr, HpRegen, StaminaRegen, EitrRegen;
        }

        internal sealed class Food
        {
            internal int Level;
            internal float CookPercent;
            internal string Buff = "";
            internal float BuffValue;
            internal readonly List<Spice> Spices = new List<Spice>(5);
            internal float Hp => Spices.Sum(s => s.Hp);
            internal float Stamina => Spices.Sum(s => s.Stamina);
            internal float Eitr => Spices.Sum(s => s.Eitr);
            internal float HpRegen => Spices.Sum(s => s.HpRegen);
            internal float StaminaRegen => Spices.Sum(s => s.StaminaRegen);
            internal float EitrRegen => Spices.Sum(s => s.EitrRegen);
        }

        internal static void Configure(ConfigFile cfg)
        {
            Enabled = cfg.Bind("Garmażerstwo", "Enabled", true, "Umiejętność gotowania oraz przyprawiania.");
            XpCook = cfg.Bind("Garmażerstwo", "XPPerCook", .5f,
                new ConfigDescription("Doświadczenie za ugotowany posiłek.", new AcceptableValueRange<float>(0f, 20f)));
            XpSpice = cfg.Bind("Garmażerstwo", "XPPerSpice", 5f,
                new ConfigDescription("Doświadczenie za jedno udane przyprawienie.", new AcceptableValueRange<float>(0f, 100f)));
            CookMax = cfg.Bind("Garmażerstwo", "FoodBonusAt100", 20f,
                new ConfigDescription("Procent bazowych statystyk posiłku przy poziomie 100.", new AcceptableValueRange<float>(0f, 50f)));
            StreakMax = cfg.Bind("Garmażerstwo", "StreakFoodBonusAt100", 10f,
                new ConfigDescription("Maksymalna dodatkowa premia z serii gotowania na poziomie 100.", new AcceptableValueRange<float>(0f, 30f)));
        }

        internal static void Install(Harmony harmony)
        {
            if (harmony == null) return;
            getTooltip = AccessTools.Method(typeof(ItemDrop.ItemData), "GetTooltip",
                new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) });
            if (getTooltip != null)
                harmony.Patch(getTooltip, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(Garmazerstwo), nameof(Tooltip))) { priority = Priority.Last });
            try { GarmazerstwoSpicing.Install(harmony); }
            catch (Exception e) { Plugin.Instance.Warning("Garmażerstwo: przyprawianie nieaktywne: " + e.Message); }
            try { GarmazerstwoEffects.Install(harmony); }
            catch (Exception e) { Plugin.Instance.Warning("Garmażerstwo: efekty częściowo nieaktywne: " + e.Message); }
        }

        internal static bool IsFood(ItemDrop.ItemData item)
        {
            var d = item?.m_shared;
            return d != null && (d.m_food > 0f || d.m_foodStamina > 0f || d.m_foodEitr > 0f);
        }

        internal static float SkillLevel(Player player)
        {
            if (player == null || SkillType == Skills.SkillType.None) return 0f;
            return Mathf.Clamp(player.GetSkills().GetSkillLevel(SkillType), 0f, 100f);
        }

        internal static string Prefab(ItemDrop.ItemData item)
        {
            return item?.m_dropPrefab != null ? item.m_dropPrefab.name :
                item?.m_shared?.m_name ?? "?";
        }

        private static string F(float v) => v.ToString("R", C);
        private static float ReadFloat(string s)
        {
            return float.TryParse(s, NumberStyles.Float, C, out float f) && !float.IsNaN(f) &&
                   !float.IsInfinity(f) ? Mathf.Clamp(f, -100000f, 100000f) : 0f;
        }

        internal static string Serialize(Food data)
        {
            var s = new StringBuilder("v1|");
            s.Append(data.Level).Append('|').Append(F(data.CookPercent)).Append('|');
            s.Append(Uri.EscapeDataString(data.Buff ?? "")).Append('|').Append(F(data.BuffValue));
            foreach (var spice in data.Spices.Take(5))
            {
                s.Append('|').Append(Uri.EscapeDataString(spice.Source ?? ""));
                s.Append(',').Append(F(spice.Hp)).Append(',').Append(F(spice.Stamina))
                 .Append(',').Append(F(spice.Eitr)).Append(',').Append(F(spice.HpRegen))
                 .Append(',').Append(F(spice.StaminaRegen)).Append(',').Append(F(spice.EitrRegen));
            }
            return s.ToString();
        }

        internal static Food Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length > 4096) return null;
            try
            {
                string[] parts = raw.Split('|');
                if (parts.Length < 5 || parts.Length > 10 || parts[0] != "v1") return null;
                if (!int.TryParse(parts[1], NumberStyles.Integer, C, out int level)) return null;
                var data = new Food
                {
                    Level = Mathf.Clamp(level, 0, 100),
                    CookPercent = Mathf.Clamp(ReadFloat(parts[2]), 0f, 1f),
                    Buff = Uri.UnescapeDataString(parts[3]),
                    BuffValue = Mathf.Clamp(ReadFloat(parts[4]), 0f, 1f)
                };
                for (int i = 5; i < parts.Length; ++i)
                {
                    string[] values = parts[i].Split(',');
                    if (values.Length != 7) return null;
                    data.Spices.Add(new Spice {
                        Source = Uri.UnescapeDataString(values[0]),
                        Hp = Mathf.Max(0, ReadFloat(values[1])),
                        Stamina = Mathf.Max(0, ReadFloat(values[2])),
                        Eitr = Mathf.Max(0, ReadFloat(values[3])),
                        HpRegen = Mathf.Max(0, ReadFloat(values[4])),
                        StaminaRegen = Mathf.Max(0, ReadFloat(values[5])),
                        EitrRegen = Mathf.Max(0, ReadFloat(values[6]))
                    });
                }
                return data;
            }
            catch (Exception) { return null; }
        }

        internal static Food Read(ItemDrop.ItemData item)
        {
            if (item?.m_customData == null || !item.m_customData.TryGetValue(Key, out string value)) return null;
            return Parse(value);
        }

        internal static void Write(ItemDrop.ItemData item, Food data)
        {
            if (item == null || data == null) return;
            item.m_customData[Key] = Serialize(data);
        }

        internal static bool SameData(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            if (a == null || b == null) return false;
            string av = null, bv = null;
            var hasA = a.m_customData != null && a.m_customData.TryGetValue(Key, out av);
            var hasB = b.m_customData != null && b.m_customData.TryGetValue(Key, out bv);
            return hasA == hasB && (!hasA || av == bv);
        }

        private static string RollBuff(ItemDrop.ItemData item, out float amount)
        {
            amount = 0f;
            float level = SkillLevel(Player.m_localPlayer);
            if (level < 75f) return "";
            string[] options;
            var d = item.m_shared;
            if (d.m_foodEitr >= d.m_food && d.m_foodEitr >= d.m_foodStamina && d.m_foodEitr > 0f)
                options = new[] { "eitr_cost", "magic_damage", "eitr_regen", "max_eitr" };
            else if (d.m_food > d.m_foodStamina * 1.15f)
                options = new[] { "physical_damage", "defence", "hp_regen" };
            else if (d.m_foodStamina > d.m_food * 1.15f)
                options = new[] { "speed", "stamina_cost", "jump", "swim" };
            else
                options = new[] { "no_skill_loss", "taming", "experience" };
            string buff = options[Random.Next(options.Length)];
            double min = 1d + (level - 75f) * .96d;
            double max = 25d + (level - 75f);
            float roll = (float)(min + Random.NextDouble() * (max - min)) / 100f;
            float scale = GarmazerstwoEffects.Scale(buff);
            amount = roll * scale;
            return buff;
        }

        internal static void OnCook(ItemDrop.ItemData item, int count)
        {
            if (Enabled?.Value != true || !IsFood(item) || Player.m_localPlayer == null ||
                Read(item) != null || count <= 0) return;
            float level = SkillLevel(Player.m_localPlayer);
            if (Time.time - lastCookTime > 120f) cookStreak = 0;
            cookStreak = Mathf.Min(100, cookStreak + count);
            lastCookTime = Time.time;
            float streak = Mathf.Clamp01((cookStreak - 1f) / 10f);
            var data = new Food {
                Level = Mathf.RoundToInt(level),
                CookPercent = level / 100f * (CookMax.Value + StreakMax.Value * streak) / 100f
            };
            data.Buff = RollBuff(item, out data.BuffValue);
            Write(item, data);
            Player.m_localPlayer.RaiseSkill(SkillType, XpCook.Value * count);
        }

        internal static Spice BuildSpice(ItemDrop.ItemData source, float level)
        {
            if (!IsFood(source)) return null;
            var d = source.m_shared;
            float percent = .05f + .05f * Mathf.Clamp01(level / 100f);
            float addHp = d.m_food > 0f ? Mathf.Max(1f, Mathf.Round(d.m_food * percent)) : 0f;
            float addStamina = d.m_foodStamina > 0f ? Mathf.Max(1f, Mathf.Round(d.m_foodStamina * percent)) : 0f;
            float addEitr = d.m_foodEitr > 0f ? Mathf.Max(1f, Mathf.Round(d.m_foodEitr * percent)) : 0f;
            return new Spice {
                Source = Prefab(source),
                Hp = addHp,
                Stamina = addStamina,
                Eitr = addEitr,
                HpRegen = d.m_food > 0f ? Mathf.Max(1f, Mathf.Round(d.m_foodRegen * percent)) : 0f,
                StaminaRegen = addStamina > 0f ? Mathf.Max(1f, Mathf.Round(addStamina * .1f)) : 0f,
                EitrRegen = addEitr > 0f ? Mathf.Max(1f, Mathf.Round(addEitr * .1f)) : 0f
            };
        }

        internal static bool AddSpice(Food data, ItemDrop.ItemData source, float skillLevel)
        {
            if (data == null || data.Spices.Count >= 5) return false;
            var spice = BuildSpice(source, skillLevel);
            if (spice == null) return false;
            data.Spices.Add(spice);
            return true;
        }

        internal static Food Default(ItemDrop.ItemData target)
        {
            return Read(target) ?? new Food { Level = 0, CookPercent = 0f };
        }

        internal static void AwardSpiceXp(int count)
        {
            if (count > 0 && Player.m_localPlayer != null)
                Player.m_localPlayer.RaiseSkill(SkillType, XpSpice.Value * count);
        }

        private static readonly Dictionary<string, string> BuffNames = new Dictionary<string, string>
        {
            { "physical_damage", "Obrażenia fizyczne" }, { "defence", "Obrona" },
            { "hp_regen", "Regeneracja zdrowia" }, { "speed", "Szybkość ruchu" },
            { "stamina_cost", "Mniejsze zużycie staminy" }, { "jump", "Wysokość skoku" },
            { "swim", "Szybkość pływania" }, { "eitr_cost", "Mniejsze zużycie Eitr" },
            { "magic_damage", "Obrażenia magiczne" }, { "eitr_regen", "Regeneracja Eitr" },
            { "max_eitr", "Maksymalne Eitr" }, { "no_skill_loss", "Ochrona umiejętności" },
            { "taming", "Szybsze oswajanie" }, { "experience", "Zdobywane PD" }
        };

        private static void Tooltip(ItemDrop.ItemData item, ref string __result)
        {
            if (item == null || string.IsNullOrEmpty(__result)) return;
            Food data = Read(item);
            if (data == null) return;
            __result += "\n<color=#D6B36B>Ugotowano na poziomie " + data.Level +
                        " Garmażerstwa</color>";
            if (data.Spices.Count > 0)
            {
                __result += "\n<color=#D6B36B>Przyprawienie: " + data.Spices.Count + "/5 ★</color>";
                foreach (var spice in data.Spices)
                {
                    __result += "\n<color=#A9C59B>★ " + spice.Source +
                        " (HP +" + F(spice.Hp) + ", stamina +" + F(spice.Stamina) +
                        ", Eitr +" + F(spice.Eitr) + ")</color>";
                }
                __result += "\n<color=#A9C59B>Regeneracja: HP +" + F(data.HpRegen) +
                            ", stamina +" + F(data.StaminaRegen) +
                            ", Eitr +" + F(data.EitrRegen) + "</color>";
            }
            if (!string.IsNullOrEmpty(data.Buff) && BuffNames.TryGetValue(data.Buff, out var name))
                __result += "\n<color=#87CEEB>" + name + ": +" +
                            Mathf.RoundToInt(data.BuffValue * 100f) + "%</color>";
        }

        // Run only around successful production, never when eating an existing food.
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static class CraftScope
        {
            private static readonly FieldInfo CraftRecipe = AccessTools.Field(typeof(InventoryGui), "m_craftRecipe");
            private static readonly FieldInfo MultiCrafting = AccessTools.Field(typeof(InventoryGui), "m_multiCrafting");
            private static readonly FieldInfo MultiCraftAmount = AccessTools.Field(typeof(InventoryGui), "m_multiCraftAmount");
            private static void Prefix(InventoryGui __instance)
            {
                Recipe recipe = __instance == null ? null : CraftRecipe?.GetValue(__instance) as Recipe;
                CookingCraft = Enabled?.Value == true && IsFood(recipe?.m_item?.m_itemData);
                CookingAmount = __instance != null && MultiCrafting?.GetValue(__instance) is bool multi && multi &&
                    MultiCraftAmount?.GetValue(__instance) is int count ? Mathf.Max(1, count) : 1;
            }
            private static void Postfix() { CookingCraft = false; CookingAmount = 0; }
            private static Exception Finalizer(Exception __exception)
            {
                CookingCraft = false; CookingAmount = 0; return __exception;
            }
        }

        // TargetMethods discovers only AddItem overloads returning the resulting item.
        // A newly spawned food stack is enhanced; existing already stacked food is never overwritten.
        [HarmonyPatch]
        private static class AwardCraft
        {
            private static IEnumerable<MethodBase> TargetMethods() =>
                typeof(Inventory).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => m.Name == "AddItem" && m.ReturnType == typeof(ItemDrop.ItemData));
            private static void Postfix(ItemDrop.ItemData __result)
            {
                if (!CookingCraft || __result == null) return;
                if (__result.m_stack != CookingAmount) return; // Fail closed on merged/preexisting stacks.
                OnCook(__result, CookingAmount);
            }
        }
    }
}