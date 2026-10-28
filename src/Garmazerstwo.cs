using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    // Versioned, per-item food records. No SharedData modification and no global prefab stat writes.
    internal static class Garmazerstwo
    {
        internal const string Key = "sirroh.skillsaddon.garmazerstwo.food.v1";
        internal const float SpiceMaxMultiplier = 2f;
        internal const string MasterSuffix = " Mistrza Kuchni";
        private const float MasterTolerance = .0001f;
        internal static Skills.SkillType SkillType;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> XpCook, XpSpice, CookMax, StreakMax;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static readonly System.Random Random = new System.Random();
        internal static bool CookingCraft;
        internal static int CookingAmount;
        private static int cookStreak;
        private static FieldInfo externalStreak;
        private static bool externalStreakChecked;
        private static float lastCookTime;
        private static MethodInfo getTooltip;

        // v3: aggregated stats without quality/stars or an artificial seasoning limit.
        // v1/v2 meals are read without dropping previously earned bonuses.
        internal sealed class Food
        {
            internal int Level;
            internal float CookPercent;
            internal string Buff = "";
            internal float BuffValue;
            internal float Hp, Stamina, Eitr, HpRegen, StaminaRegen, EitrRegen;
        }

        internal static void Configure(ConfigFile cfg)
        {
            Enabled = cfg.Bind("Garmażerstwo", "Enabled", true, "Umiejętność gotowania oraz przyprawiania.");
            XpCook = cfg.Bind("Garmażerstwo", "XPPerCook", .5f,
                new ConfigDescription("Doświadczenie za ugotowany posiłek.", new AcceptableValueRange<float>(0f, 20f)));
            XpSpice = cfg.Bind("Garmażerstwo", "XPPerSpice", 5f,
                new ConfigDescription("Doświadczenie za jedno udane przyprawienie.", new AcceptableValueRange<float>(0f, 100f)));
            CookMax = cfg.Bind("Garmażerstwo", "FoodBonusAt100_v2", 100f,
                new ConfigDescription("Procentowy bonus gotowania na poziomie 100. Nowy klucz zapewnia +100% również w już istniejących konfiguracjach.", new AcceptableValueRange<float>(0f, 100f)));
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
            // Compact, locale-invariant and deterministic for stack identity checks.
            return string.Join("|", new[] { "v3",
                data.Level.ToString(C), F(data.CookPercent),
                Uri.EscapeDataString(data.Buff ?? ""), F(data.BuffValue),
                F(data.Hp), F(data.Stamina), F(data.Eitr),
                F(data.HpRegen), F(data.StaminaRegen), F(data.EitrRegen) });
        }

        internal static Food Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length > 4096) return null;
            try
            {
                string[] parts = raw.Split('|');
                if (parts.Length < 5 ||
                    !int.TryParse(parts[1], NumberStyles.Integer, C, out int level)) return null;
                var data = new Food {
                    Level = Mathf.Clamp(level, 0, 100),
                    CookPercent = Mathf.Clamp(ReadFloat(parts[2]), 0f, 1f),
                    Buff = Uri.UnescapeDataString(parts[3]),
                    BuffValue = Mathf.Clamp(ReadFloat(parts[4]), 0f, 1f)
                };
                if (parts[0] == "v3" && parts.Length == 11)
                {
                    data.Hp = Mathf.Max(0f, ReadFloat(parts[5]));
                    data.Stamina = Mathf.Max(0f, ReadFloat(parts[6]));
                    data.Eitr = Mathf.Max(0f, ReadFloat(parts[7]));
                    data.HpRegen = Mathf.Max(0f, ReadFloat(parts[8]));
                    data.StaminaRegen = Mathf.Max(0f, ReadFloat(parts[9]));
                    data.EitrRegen = Mathf.Max(0f, ReadFloat(parts[10]));
                    return data;
                }
                // Historical v2: ignore star count, keep aggregate food stats.
                if (parts[0] == "v2")
                {
                    if (parts.Length != 12 ||
                        !int.TryParse(parts[5], NumberStyles.Integer, C, out int oldStars) ||
                        oldStars < 0 || oldStars > 5) return null;
                    data.Hp = Mathf.Max(0f, ReadFloat(parts[6]));
                    data.Stamina = Mathf.Max(0f, ReadFloat(parts[7]));
                    data.Eitr = Mathf.Max(0f, ReadFloat(parts[8]));
                    data.HpRegen = Mathf.Max(0f, ReadFloat(parts[9]));
                    data.StaminaRegen = Mathf.Max(0f, ReadFloat(parts[10]));
                    data.EitrRegen = Mathf.Max(0f, ReadFloat(parts[11]));
                    return data;
                }
                if (parts[0] != "v1" || parts.Length > 10) return null;
                // No old bonuses are discarded. They become six cumulative totals.
                for (int i = 5; i < parts.Length; ++i)
                {
                    string[] values = parts[i].Split(',');
                    if (values.Length != 7) return null;
                    data.Hp += Mathf.Max(0f, ReadFloat(values[1]));
                    data.Stamina += Mathf.Max(0f, ReadFloat(values[2]));
                    data.Eitr += Mathf.Max(0f, ReadFloat(values[3]));
                    data.HpRegen += Mathf.Max(0f, ReadFloat(values[4]));
                    data.StaminaRegen += Mathf.Max(0f, ReadFloat(values[5]));
                    data.EitrRegen += Mathf.Max(0f, ReadFloat(values[6]));
                }
                return data;
            }
            catch (Exception) { return null; }
        }

        internal static Food Read(ItemDrop.ItemData item)
        {
            if (item?.m_customData == null || !item.m_customData.TryGetValue(Key, out string value)) return null;
            var record = Parse(value);
            if (record == null) return null;
            ClampSeasoning(record, item);
            // Existing legacy overpowered dishes are capped on read, without
            // destructively rewriting the inventory during loading.
            NormalizeMaster(record, item);
            return record;
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
            if (hasA != hasB) return false;
            if (!hasA || av == bv) return true;
            // Completed meals have canonical seasoning stats independent of
            // which ingredients were spent. Only compare them as identical if
            // their separate cooking level and special effects also match.
            if (IsMaster(a) && IsMaster(b))
            {
                // Do not silently merge custom items whose *other mod* data
                // differs even if their Sirroh mastery profile is identical.
                var otherA = a.m_customData.Where(p => p.Key != Key)
                    .OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
                var otherB = b.m_customData.Where(p => p.Key != Key)
                    .OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
                if (otherA.Length != otherB.Length) return false;
                for (int i = 0; i < otherA.Length; ++i)
                    if (otherA[i].Key != otherB[i].Key ||
                        otherA[i].Value != otherB[i].Value) return false;
                Food aRecord = Read(a), bRecord = Read(b);
                return aRecord != null && bRecord != null &&
                       Serialize(aRecord) == Serialize(bRecord);
            }
            return false;
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

        private static int EffectiveCookingStreak()
        {
            if (!externalStreakChecked)
            {
                externalStreakChecked = true;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var module = assembly.GetType("ImpactfulSkills.patches.Cooking", false);
                    externalStreak = module == null ? null : AccessTools.Field(module, "CookingStreak");
                    if (externalStreak != null) break;
                }
            }
            if (externalStreak != null)
            {
                try { return Mathf.Max(0, Convert.ToInt32(externalStreak.GetValue(null))); }
                catch (Exception) { }
            }
            return cookStreak;
        }

        internal static void OnCook(ItemDrop.ItemData item, int count, bool awardXp = true)
        {
            if (Enabled?.Value != true || !IsFood(item) || Player.m_localPlayer == null ||
                Read(item) != null || count <= 0) return;
            float level = SkillLevel(Player.m_localPlayer);
            if (Time.time - lastCookTime > 120f) cookStreak = 0;
            cookStreak = Mathf.Min(100, cookStreak + count);
            lastCookTime = Time.time;
            var data = new Food {
                Level = Mathf.RoundToInt(level),
                CookPercent = CookPercentage(level)
            };
            data.Buff = RollBuff(item, out data.BuffValue);
            Write(item, data);
            if (awardXp) AwardCookingXp(count);
        }

        internal static float CookPercentage(float level)
        {
            level = Mathf.Clamp(level, 0f, 100f);
            float streak = Mathf.Clamp01((EffectiveCookingStreak() - 1f) / 10f);
            return Mathf.Clamp01(level / 100f * (CookMax.Value + StreakMax.Value * streak) / 100f);
        }

        // +100% cooking and +200% seasoning are separate: max total +300%.
        internal static float SpiceLimit(float vanilla) =>
            Mathf.Max(0f, vanilla) * SpiceMaxMultiplier;

        internal static void ClampSeasoning(Food data, ItemDrop.ItemData item)
        {
            if (data == null || item?.m_shared == null) return;
            var d = item.m_shared;
            data.Hp = Mathf.Clamp(data.Hp, 0f, SpiceLimit(d.m_food));
            data.Stamina = Mathf.Clamp(data.Stamina, 0f, SpiceLimit(d.m_foodStamina));
            data.Eitr = Mathf.Clamp(data.Eitr, 0f, SpiceLimit(d.m_foodEitr));
            data.HpRegen = Mathf.Clamp(data.HpRegen, 0f, Mathf.Max(0f, d.m_foodRegen));
        }

        internal static bool IsMaster(Food data, ItemDrop.ItemData item)
        {
            if (data == null || !IsFood(item)) return false;
            var d = item.m_shared;
            return (d.m_food <= 0f || data.Hp >= SpiceLimit(d.m_food) - MasterTolerance) &&
                   (d.m_foodStamina <= 0f || data.Stamina >= SpiceLimit(d.m_foodStamina) - MasterTolerance) &&
                   (d.m_foodEitr <= 0f || data.Eitr >= SpiceLimit(d.m_foodEitr) - MasterTolerance);
        }

        internal static bool IsMaster(ItemDrop.ItemData item) =>
            IsMaster(Read(item), item);

        internal static void NormalizeMaster(Food data, ItemDrop.ItemData item)
        {
            ClampSeasoning(data, item);
            if (!IsMaster(data, item)) return;
            var d = item.m_shared;
            // Normalise ALL seasoning contributions of mastered copies of
            // the same recipe: differing spices cannot split their stacks.
            // Cooking skill level / unique cooking Buff stay independent.
            data.Hp = SpiceLimit(d.m_food);
            data.Stamina = SpiceLimit(d.m_foodStamina);
            data.Eitr = SpiceLimit(d.m_foodEitr);
            // Bounded, deterministic secondary effects for the finished meal.
            // This is necessary for two mastered recipes to share item identity.
            data.HpRegen = Mathf.Max(0f, d.m_foodRegen) * .2f;
            data.StaminaRegen = Mathf.Min(1f, Mathf.Max(0f, d.m_foodStamina) * .02f);
            data.EitrRegen = Mathf.Min(1f, Mathf.Max(0f, d.m_foodEitr) * .02f);
        }

        internal static void Effective(ItemDrop.ItemData item, out float hp,
            out float stamina, out float eitr, out float regenHp, out float regenStamina,
            out float regenEitr)
        {
            var d = item.m_shared;
            var record = Read(item);
            float multiplier = 1f + Mathf.Clamp01(record?.CookPercent ?? 0f);
            hp = d.m_food * multiplier +
                Mathf.Clamp(record?.Hp ?? 0f, 0f, SpiceLimit(d.m_food));
            stamina = d.m_foodStamina * multiplier +
                Mathf.Clamp(record?.Stamina ?? 0f, 0f, SpiceLimit(d.m_foodStamina));
            eitr = d.m_foodEitr * multiplier +
                Mathf.Clamp(record?.Eitr ?? 0f, 0f, SpiceLimit(d.m_foodEitr));
            regenHp = d.m_foodRegen + Mathf.Clamp(record?.HpRegen ?? 0f,
                0f, Mathf.Max(0f, d.m_foodRegen));
            regenStamina = record?.StaminaRegen ?? 0f;
            regenEitr = record?.EitrRegen ?? 0f;
        }

        internal static bool AddSpice(Food data, ItemDrop.ItemData source,
            ItemDrop.ItemData target, float skillLevel)
        {
            if (data == null || !IsFood(source) || !IsFood(target)) return false;
            ClampSeasoning(data, target);
            if (IsMaster(data, target)) return false;
            Effective(source, out float hp, out float stamina, out float eitr,
                out _, out float staminaRegen, out float eitrRegen);
            float percent = .05f + .05f * Mathf.Clamp01(skillLevel / 100f);
            float bonusHp = hp > 0f ? Mathf.Max(1f, Mathf.Round(hp * percent)) : 0f;
            float bonusStamina = stamina > 0f ? Mathf.Max(1f, Mathf.Round(stamina * percent)) : 0f;
            float bonusEitr = eitr > 0f ? Mathf.Max(1f, Mathf.Round(eitr * percent)) : 0f;

            float nextHp = Mathf.Min(SpiceLimit(target.m_shared.m_food), data.Hp + bonusHp);
            float nextStamina = Mathf.Min(SpiceLimit(target.m_shared.m_foodStamina),
                data.Stamina + bonusStamina);
            float nextEitr = Mathf.Min(SpiceLimit(target.m_shared.m_foodEitr),
                data.Eitr + bonusEitr);
            if (nextHp <= data.Hp + MasterTolerance &&
                nextStamina <= data.Stamina + MasterTolerance &&
                nextEitr <= data.Eitr + MasterTolerance) return false;
            data.Hp = nextHp;
            data.Stamina = nextStamina;
            data.Eitr = nextEitr;

            // HP regeneration: 20% of the ingredient's vanilla base, and
            // never over 2x the target's vanilla regen including base.
            float sourceBaseRegen = Mathf.Max(0f, source.m_shared.m_foodRegen);
            float targetBaseRegen = Mathf.Max(0f, target.m_shared.m_foodRegen);
            data.HpRegen = Mathf.Clamp(data.HpRegen + sourceBaseRegen * .20f,
                0f, targetBaseRegen);
            if (stamina > 0f)
                data.StaminaRegen += Mathf.Max(1f, Mathf.Round(
                    staminaRegen * percent + bonusStamina * .1f));
            if (eitr > 0f)
                data.EitrRegen += Mathf.Max(1f, Mathf.Round(
                    eitrRegen * percent + bonusEitr * .1f));
            data.Level = Mathf.RoundToInt(Mathf.Clamp(skillLevel, 0f, 100f));
            data.CookPercent = CookPercentage(skillLevel);
            if (string.IsNullOrEmpty(data.Buff) && skillLevel >= 75f)
                data.Buff = RollBuff(target, out data.BuffValue);
            NormalizeMaster(data, target);
            return true;
        }

        internal static Food Default(ItemDrop.ItemData target)
        {
            var record = Read(target) ?? new Food { Level = 0, CookPercent = 0f };
            ClampSeasoning(record, target);
            return record;
        }

        internal static void AwardCookingXp(int count)
        {
            if (count > 0 && Player.m_localPlayer != null)
                Player.m_localPlayer.RaiseSkill(SkillType, XpCook.Value * count);
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

        // Only update existing Valheim lines. No verbose source history or
        // duplicate "food totals" below the original tooltip.
        private static void Tooltip(ItemDrop.ItemData item, ref string __result)
        {
            if (item == null || string.IsNullOrEmpty(__result)) return;
            Food data = Read(item);
            if (data == null) return;
            Effective(item, out float hp, out float stamina, out float eitr,
                out float heal, out float stRegen, out float eRegen);
            string[] lines = __result.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                // ItemData.GetTooltip returns localization *tokens*, not the
                // Polish/English labels rendered by Localization.Localize later.
                // Keep localized names too for other mods that localize early.
                if (IsStatLine(lines[i], "$item_food_health", "Życie", "Zdrowie", "Health"))
                    lines[i] = ReplaceFirstValue(lines[i], hp);
                else if (IsStatLine(lines[i], "$item_food_stamina", "Kondycja", "Stamina"))
                    lines[i] = ReplaceFirstValue(lines[i], stamina);
                else if (IsStatLine(lines[i], "$item_food_eitr", "Eitr"))
                    lines[i] = ReplaceFirstValue(lines[i], eitr);
                else if (IsStatLine(lines[i], "$item_food_regen", "Leczenie", "Healing"))
                    lines[i] = ReplaceFirstValue(lines[i], heal);
            }
            if (IsMaster(data, item) && lines.Length > 0 &&
                !lines[0].Contains(MasterSuffix))
                lines[0] += MasterSuffix;
            __result = string.Join("\n", lines);
            // One concise line for the updated level; stars are on the item icon.
            __result += "\n<color=#D6B36B>Garmażerstwo: " + data.Level + "</color>";
            if (!string.IsNullOrEmpty(data.Buff) && BuffNames.TryGetValue(data.Buff, out var name))
                __result += "\n<color=#87CEEB>" + name + ": +" +
                    Mathf.RoundToInt(data.BuffValue * 100f) + "%</color>";
        }

        private static bool IsStatLine(string raw, params string[] names)
        {
            string plain = Regex.Replace(raw, "<[^>]+>", "").TrimStart();
            foreach (string name in names)
                if (plain.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string ReplaceFirstValue(string line, float value)
        {
            int colon = line.IndexOf(':');
            if (colon < 0) return line;
            Match match = Regex.Match(line.Substring(colon + 1),
                @"^(?:\s|<[^>]+>)*(-?\d+(?:[.,]\d+)?)");
            if (!match.Success) return line;
            Group number = match.Groups[1];
            int start = colon + 1 + number.Index;
            string formatted = Mathf.Approximately(value, Mathf.Round(value))
                ? Mathf.RoundToInt(value).ToString(C)
                : value.ToString("0.#", C);
            if (number.Value.Contains(",")) formatted = formatted.Replace(".", ",");
            return line.Substring(0, start) + formatted +
                   line.Substring(start + number.Length);
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