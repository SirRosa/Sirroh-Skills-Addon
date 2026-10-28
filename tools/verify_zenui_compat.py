#!/usr/bin/env python3
"""Isolated regression contract for optional ZenUI 1.9.3 food summary bridge."""
from pathlib import Path
import re

src = Path("src/ZenUiCompatibility.cs").read_text(encoding="utf-8")
plugin = Path("src/Plugin.cs").read_text(encoding="utf-8")
project = Path("SirrohSkillsAddon.csproj").read_text(encoding="utf-8")
workflow = Path(".github/workflows/build.yml").read_text(encoding="utf-8")
food = Path("src/Garmazerstwo.cs").read_text(encoding="utf-8")

assert 'ZenDragon.ZenUI' in src
assert 'ZenUI.Section.TooltipSummary' in src
assert 'GetTooltipSummary' in src
assert 'Chainloader.PluginInfos.TryGetValue(ZenGuid' in src
assert 'harmony.Patch(target, postfix:' in src
assert 'private static void PatchSummary(object __0, ref string __result)' in src
assert 'Garmazerstwo.Effective(direct' in src
assert 'Garmazerstwo.Parse(raw)' in src
assert 'source is ItemDrop.ItemData direct' in src
assert 'm_customData' in src and 'm_shared' in src
assert '💖' in src and '💪' in src and '✨' in src
assert 'IconBlock.Replace(__result' in src
assert 'IconValue.Replace(block' in src
assert 'bool foundHp = false, foundStamina = false, foundEitr = false;' in src
assert 'if (eitr > 0f && !foundEitr)' in src
assert 'if (foundHp || foundStamina || foundEitr)' in src
assert 'if (installed || disabled || harmony == null' in src
assert '.m_food =' not in src and '.m_foodStamina =' not in src and '.m_foodEitr =' not in src
assert 'm_quality =' not in src
assert 'using ZenUI' not in src
assert 'Zen.ModLib' not in project and 'ZenUI.dll' not in project
assert 'ZenUiCompatibility.TryInstall(harmony);' in plugin
assert 'ZenUiCompatibility.Reset();' in plugin
assert 'tools/verify_zenui_compat.py' in workflow

# Simulate exactly the <nobr> food markup emitted by the extracted ZenUI
# TooltipSummary.MakeIcon helper, and verify scoped numeric replacement.
sample = ('<align=center>jedzenie\n'
          '<nobr><size=+4><color=#f00>💖</color></size><space=2px>'
          '<color=#fff><size=-2>25</size></color></nobr>'
          '<nobr><size=+4><color=#ff0>💪</color></size><space=2px>'
          '<color=#fff><size=-2>8</size></color></nobr>\n'
          '<nobr><size=+4><color=#aaa>⏱</color></size><space=2px>'
          '<color=#fff><size=-2>20</size></color></nobr>'
          '</align>')
blocks = re.compile(r'<nobr>(?:(?!</nobr>).)*</nobr>', re.S)
digits = re.compile(r'(?<=<size=-2>)-?\d+(?:[.,]\d+)?(?=</size>)')
def replace(m):
    block = m.group(0)
    if '💖' in block: return digits.sub('32.8', block, count=1)
    if '💪' in block: return digits.sub('12.2', block, count=1)
    return block
out = blocks.sub(replace, sample)
assert '💖</color></size><space=2px><color=#fff><size=-2>32.8' in out
assert '💪</color></size><space=2px><color=#fff><size=-2>12.2' in out
assert '⏱</color></size><space=2px><color=#fff><size=-2>20' in out
assert 'm_customData[Key] = Serialize(data)' in food
print("PASS: isolated optional ZenUI 1.9.3 simplified tooltip adaptation, no prefab/item mutation.")
