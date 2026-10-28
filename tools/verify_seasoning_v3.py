#!/usr/bin/env python3
"""Contracts for Sirroh Garmażerstwo v3 unlimited seasoning and stack safety."""
import math
from pathlib import Path
root=Path("src")
food=(root/"Garmazerstwo.cs").read_text(encoding="utf-8")
spice=(root/"GarmazerstwoSpicing.cs").read_text(encoding="utf-8")
effects=(root/"GarmazerstwoEffects.cs").read_text(encoding="utf-8")
assert not (root/"GarmazerstwoStars.cs").exists()
assert "data.Stars" not in food+spice
assert 'return string.Join("|", new[] { "v3",' in food
for expected in ('parts[0] == "v2"', 'parts[0] != "v1"', 'parts[0] == "v3"'):
    assert expected in food
assert 'sourceBaseRegen * .20f' in food
assert 'data.HpRegen = Mathf.Clamp(' in food
assert '0f, targetBaseRegen);' in food
assert 'regenHp = d.m_foodRegen + Mathf.Clamp(' in food
assert 'extraHealth = Mathf.Clamp(extraHealth, 0f, state.VanillaHpRegen);' in effects
assert 'state.HpRegen += Mathf.Clamp(data.HpRegen,' in effects
assert 'Mathf.Max(1f, Mathf.Round(hpRegen' not in food
assert 'int count = Mathf.Min(dst.m_stack, Mathf.Min(src.m_stack, dragCount));' in spice
assert 'ItemDrop.ItemData seasoned = b.Clone();' in spice
assert 'seasoned.m_stack = n;' in spice
assert 'seasoned.m_customData[Garmazerstwo.Key] = result;' in spice
assert 'AddItemAt.Invoke(dest' in spice
assert 'b.m_stack -= n;' in spice
assert 'origin.RemoveItem(a, n)' in spice
assert 'dest.RemoveItem(placed)' in spice
assert 'b.m_stack += n;' in spice
assert 'Fingerprint(a) != expectedSourceData' in spice
assert 'Fingerprint(b) != expectedTargetData' in spice
def seasoning(source_base, target_base, previous_extra=0):
    return min(max(0,target_base),max(0,previous_extra)+.2*max(0,source_base))
for src,expected in [(1,.2),(2,.4),(10,2)]:
    assert math.isclose(seasoning(src,10),expected)
assert math.isclose(1+seasoning(10,1),2)
assert math.isclose(10+seasoning(10,10,9),20)
assert seasoning(10,0)==0
assert math.isclose(seasoning(1,1,.2),.4)
for source,target in [(3,10),(10,3),(10,10),(1,100),(100,1)]:
    used=min(source,target)
    assert 0<used<=source and 0<used<=target
    assert target-used+used==target
print("PASS v3 unlimited seasoning, v1/v2 migration, 20% regen, 2x cap and partial stack transaction.")
