#!/usr/bin/env python3
"""Static regression checks for user-visible Łowca Głów progress and Blacksmithing PL."""
from pathlib import Path
import re
import json

source = Path("src/SlayerTrophies.cs").read_text(encoding="utf-8")
plugin = Path("src/Plugin.cs").read_text(encoding="utf-8")
translation = Path("src/BlacksmithingPolish.cs").read_text(encoding="utf-8")
workflow = Path(".github/workflows/build.yml").read_text(encoding="utf-8")
manifest = json.loads(Path("manifest.json").read_text(encoding="utf-8"))

for marker in (
    "Original Slayer Skills rebuilt its localized trophy names",
    "RefreshLocalizedTrophies",
    "TrophyLabels",
    "Points.TryGetValue(key, out var value)",
    "KillCounts.TryGetValue(key, out int kills)",
    "string prefix = \"Łowca Głów \"",
    "string detail = text.text",
    "private static void ReceiveKill(",
    "PanelPostepuWekwipunku",
    "PokazPostepZabojstw",
    "ŁOWCA GŁÓW  •  POSTĘP",
    "InventoryGui.IsVisible()",
    "ProgressKeys",
    "KillSaveKey",
    "Common",
):
    if marker == "Common":
        continue
    assert marker in source, f"Łowca Głów UI regression: {marker}"
assert "SlayerTrophies.DrawProgress()" in plugin
entries = re.findall(r'\{"[^"]+\|[^"]+", new Translation\(', translation)
assert len(entries) == 60, f"Expected all 60 original BE settings; found {len(entries)}"
assert 'AccessTools.Method(localizeKey.GetType(), "Polish"' in translation
assert "Forged at Blacksmithing" in translation and "Elemental Infusion:" in translation
assert "Masterwork crafting created an extra item!" in translation
assert "<Description>k__BackingField" in translation
assert "definition.Section + \"|\" + definition.Key" in translation, "Do not change BepInEx config keys"
assert "BlacksmithingPolish.Install(harmony)" in plugin
assert manifest["version_number"] == "0.3.0"
assert "SirrohSkillsAddon-v0.3.0" in workflow
print("PASS: Łowca Głów visible UI, original trophy mapping, all 60 PL BE settings, v0.3.0")
