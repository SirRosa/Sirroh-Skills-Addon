#!/usr/bin/env python3
"""Static safety contracts for the 0.4.0 experimental Garmażerstwo branch."""
import json
import re
from pathlib import Path

root = Path("src")
food = (root / "Garmazerstwo.cs").read_text(encoding="utf-8")
spices = (root / "GarmazerstwoSpicing.cs").read_text(encoding="utf-8")
inventory = (root / "GarmazerstwoInventory.cs").read_text(encoding="utf-8")
effects = (root / "GarmazerstwoEffects.cs").read_text(encoding="utf-8")
stations = (root / "GarmazerstwoStations.cs").read_text(encoding="utf-8")
icons = (root / "SuppliedSkillIcons.cs").read_text(encoding="utf-8")
plugin = (root / "Plugin.cs").read_text(encoding="utf-8")
polish = (root / "BlacksmithingPolish.cs").read_text(encoding="utf-8")
csproj = Path("SirrohSkillsAddon.csproj").read_text(encoding="utf-8")
workflow = Path(".github/workflows/build.yml").read_text(encoding="utf-8")
manifest = json.loads(Path("manifest.json").read_text(encoding="utf-8"))

assert manifest["version_number"] == "0.4.9"
assert 'internal const string Version = "0.4.9"' in plugin
assert 'Identifier = Guid + ".garmazerstwo"' in plugin
assert 'Identifier = "neithar.Slayer"' in plugin
assert 'Identifier = Guid + ".zawadiaka"' in plugin
assert "Garmazerstwo.Install(harmony)" in plugin
assert "ZenUiCompatibility.TryInstall(harmony)" in plugin
assert "BlacksmithingPolish.Install(harmony)" in plugin
assert 'if (data == null || !IsFood(source) || !IsFood(target)) return false;' in food
assert 'data.Stars' not in food + spices
assert 'sourceBaseRegen * .20f' in food
assert 'target.m_shared.m_foodRegen' in food
assert 'regenHp = d.m_foodRegen + Mathf.Clamp(' in food
assert 'state.HpRegen += Mathf.Clamp(data.HpRegen,' in effects
assert 'extraHealth = Mathf.Clamp(extraHealth, 0f, state.VanillaHpRegen);' in effects
assert 'data.Hp = nextHp;' in food
assert 'data.Stamina += bonusStamina;' in food
assert 'data.Eitr += bonusEitr;' in food
assert 'Effective(source, out float hp, out float stamina, out float eitr,' in food
assert 'data.Level = Mathf.RoundToInt(Mathf.Clamp(skillLevel, 0f, 100f));' in food
assert 'data.CookPercent = CookPercentage(skillLevel);' in food
assert 'if (parts[0] != "v1" || parts.Length > 10) return null;' in food
assert 'if (parts[0] == "v3" && parts.Length == 11)' in food
assert 'if (parts[0] == "v2")' in food
assert 'return string.Join("|", new[] { "v3",' in food
assert 'data.Spices' not in food + spices
assert 'int count = Mathf.Min(dst.m_stack, Mathf.Min(src.m_stack, dragCount));' in spices
assert 'TryFindFreeSlot(dest, out slot)' in spices
assert 'b.Clone()' in spices
assert 'AddItemAt.Invoke(dest' in spices
assert 'b.m_stack -= n;' in spices
assert 'dest.RemoveItem(placed);' in spices
assert 'Fingerprint(a) != expectedSourceData' in spices
assert 'Stars' not in spices
assert not (root / "GarmazerstwoStars.cs").exists()
assert 'Garmazerstwo.AddSpice(record, a, b,' in spices
assert 'Garmazerstwo.Serialize(record)' in spices
assert 'float percent = .05f + .05f * Mathf.Clamp01(skillLevel / 100f);' in food
assert 'Mathf.Max(1f, Mathf.Round(' in food
assert 'lines[i] = ReplaceFirstValue(lines[i], hp)' in food
for key in ("$item_food_health", "$item_food_stamina", "$item_food_eitr", "$item_food_regen"):
    assert key in food, "Missing raw Valheim 1.0.21 tooltip token " + key
assert 'private static bool IsStatLine' in food
assert 'private static string ReplaceFirstValue' in food
assert 'string[] lines = __result.Split' in food
assert 'm_shared.m_food =' not in food
assert 'lines[i] = ReplaceFirstValue(lines[i], stamina)' in food
assert 'lines[i] = ReplaceFirstValue(lines[i], eitr)' in food
assert 'Ugotowano na poziomie ' not in food
assert 'Statystyki posiłku: HP ' not in food
assert 'Przyprawienie: ' not in food
assert '"sirroh.skillsaddon.garmazerstwo.food.v1"' in food
assert 'm_customData[Key] = Serialize(data)' in food
assert re.search(r"\bm_shared\s*=(?!=)", food + effects + spices) is None
assert re.search(r"\bm_quality\s*=(?!=)", food + effects + spices) is None
assert 'UnifiedPopup.Push(new YesNoPopup(' in spices
assert 'if (!origin.RemoveItem(a, n))' in spices
assert 'ReferenceEquals(origin.GetItemAt(aPos.x, aPos.y), a)' in spices
assert 'ReferenceEquals(dest.GetItemAt(bPos.x, bPos.y), b)' in spices
assert 'Garmazerstwo.SameData(incoming, __result)' in inventory
assert 'Garmazerstwo.SameData(__instance, __0)' in inventory
assert 'SaveFood(__instance)' in effects
assert 'LoadFood(__instance)' in effects
assert 'ApplyFoodValues(__instance)' in effects
assert 'Player.m_localPlayer' in stations
assert 'Skills.SkillType.Cooking' in stations
assert 'Wykute na poziomie $1 Kuźnictwa' in polish
assert 'Kuźnictwo' in polish
assert "Kowalstwo" not in polish
for icon in ["bron_dwureczna", "lowca_glow", "garmazerstwo", "kuznictwo"]:
    file = Path("assets/skill_icons") / f"{icon}.png"
    assert file.is_file() and file.read_bytes()[:8] == b"\x89PNG\r\n\x1a\n", icon
    assert f'LogicalName="sirroh.icons.{icon}.png"' in csproj
    assert f'sirroh.icons.{icon}.png' in icons
assert "branches: [main, 'test/**']" in workflow
assert "if: github.ref == 'refs/heads/main'" in workflow
print("PASS: 0.4.9 unlimited v3, v1/v2 migration, capped HP regen and automatic split.")

# Packaging identity must not break existing Thunderstore updates.
import struct
package_icon = Path("assets/thunderstore/icon.png").read_bytes()
assert package_icon[:8] == bytes((137, 80, 78, 71, 13, 10, 26, 10))
assert struct.unpack(">II", package_icon[16:24]) == (256, 256)
assert '[BepInPlugin(Guid, "Sirroh Skills [Addon]", Version)]' in plugin
assert manifest["name"] == "Sirroh_Skills_Addon"
assert "tools/build_icon.py dist/package/icon.png" in workflow
print("PASS: refreshed Gale package slug, unchanged custom icon, stable plugin GUID and original skill IDs.")
