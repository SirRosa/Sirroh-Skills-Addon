#!/usr/bin/env python3
"""Guard the deliberately minimal trophy UI and valid Blacksmithing Polish bridge."""
import json
import re
from pathlib import Path

trophy = Path("src/SlayerTrophies.cs").read_text(encoding="utf-8")
plugin = Path("src/Plugin.cs").read_text(encoding="utf-8")
translation = Path("src/BlacksmithingPolish.cs").read_text(encoding="utf-8")
workflow = Path(".github/workflows/build.yml").read_text(encoding="utf-8")
manifest = json.loads(Path("manifest.json").read_text(encoding="utf-8"))

assert "RefreshLocalizedTrophies" in trophy
assert "TrophyLabels" in trophy
assert "KillCounts.TryGetValue(key, out int kills)" in trophy
assert "Points.TryGetValue(key, out var value)" in trophy
assert 'string prefix = Mathf.FloorToInt(value) + "/" + pointsCap.Value' in trophy
assert '" • " + (kills % threshold) + "/" + threshold;' in trophy
assert "private static void ReceiveKill(" in trophy
assert "KillSaveKey" in trophy
assert "DrawProgress" not in trophy
assert "GUI.Box" not in trophy and "GUI.Label" not in trophy
assert "SlayerTrophies.DrawProgress()" not in plugin
assert "PokazPostepZabojstw" not in trophy
assert "PanelPostepuWekwipunku" not in trophy
assert '"Łowca Głów " + Mathf.FloorToInt(value)' not in trophy

entries = re.findall(r'\{"[^"]+\|[^"]+", new Translation\(', translation)
assert len(entries) == 60, f"Expected all 60 BE settings, got {len(entries)}"
assert 'private sealed class ConfigurationManagerAttributes' in translation
assert "private sealed class LabelTag" not in translation
assert 'AccessTools.Method(localizeKey.GetType(), "Polish"' in translation
assert "BlacksmithingPolish.Tick()" in plugin
assert "RefreshConfigurationManager()" in translation
assert "OriginalDescriptions" in translation
assert "Forged at Blacksmithing" in translation and "Elemental Infusion:" in translation
assert "Masterwork crafting created an extra item!" in translation
assert "<Description>k__BackingField" in translation
assert 'definition.Section + "|" + definition.Key' in translation
assert "BlacksmithingPolish.Install(harmony)" in plugin
assert manifest["version_number"] == "0.4.3"
assert "Sirroh_Skills_Addon-v0.4.3" in workflow
print("PASS: trophy-only HUD, preserved counters, all 60 Blacksmithing Polish settings, v0.4.3")
