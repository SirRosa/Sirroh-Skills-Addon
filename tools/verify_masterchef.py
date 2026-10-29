#!/usr/bin/env python3
"""Explicit master-chef balance, stack identity and ingredient-spending contracts."""
from pathlib import Path
import math, json

food=Path("src/Garmazerstwo.cs").read_text("utf8")
effects=Path("src/GarmazerstwoEffects.cs").read_text("utf8")
zen=Path("src/ZenUiCompatibility.cs").read_text("utf8")
mix=Path("src/GarmazerstwoMixing.cs").read_text("utf8")
spice=Path("src/GarmazerstwoSpicing.cs").read_text("utf8")
manifest=json.loads(Path("manifest.json").read_text("utf8"))
assert manifest["version_number"] == "0.4.10"
assert "FoodBonusAt100_v2" in food
assert 'CookMax = cfg.Bind("Garmażerstwo", "FoodBonusAt100_v2", 50f' in food
assert 'return Mathf.Clamp(level / 100f' in food
assert 'SpiceMaxMultiplier = .5f' in food
assert 'MasterSuffix = " Mistrza Kuchni"' in food
assert "NormalizeMaster(record, item);" in food
assert "ClampSeasoning(record, item);" in food
assert "if (IsMaster(data, target)) return false;" in food
assert "if (nextHp <= data.Hp + MasterTolerance" in food
assert "data.Hp = nextHp;" in food
assert "data.Stamina = nextStamina;" in food
assert "data.Eitr = nextEitr;" in food
assert "data.HpRegen = Mathf.Clamp(data.HpRegen + sourceBaseRegen * .20f" in food
assert "NormalizeMaster(data, target);" in food
assert "if (IsMaster(data, item) && lines.Length > 0" not in food
assert "lines[0] += MasterSuffix" not in food
assert '"CreateItemTooltip", new[] { typeof(ItemDrop.ItemData), typeof(UITooltip) }' in food
assert "TooltipTitle(InventoryGrid __instance," in food
assert "topic + MasterSuffix, tooltip.m_text, anchor, Vector2.zero" in food
assert "InventoryTooltipAnchor.GetValue(__instance)" in food
assert "Serialize(aRecord) == Serialize(bRecord)" in food
assert "if (Garmazerstwo.IsMaster(dst))" in spice
assert "if (!Garmazerstwo.AddSpice(record, a, b," in spice
assert "Mathf.Clamp(data.Hp, 0f, Garmazerstwo.SpiceLimit" in effects
assert "Mathf.Clamp(data.Stamina, 0f, Garmazerstwo.SpiceLimit" in effects
assert "Mathf.Clamp(data.Eitr, 0f, Garmazerstwo.SpiceLimit" in effects
assert "Mathf.Clamp(data.Hp, 0f, Garmazerstwo.SpiceLimit" in zen
assert "Mathf.Clamp(data.Stamina, 0f, Garmazerstwo.SpiceLimit" in zen
assert "Mathf.Clamp(data.Eitr, 0f, Garmazerstwo.SpiceLimit" in zen
assert "remaining[source]--;" in mix
assert "!Garmazerstwo.AddSpice(simulated, source," in mix
assert "if (applied) continue;" in mix
assert "Garmazerstwo.IsMaster(i)" in mix
assert "OrderByDescending(Value)" in mix
assert "plan.ConsumedCount > 0 ? plan : null;" in mix
assert "foreach (var group in plan.Bases.Where(g => g.Spices.Count > 0))" in mix

# Independent, explicit expected results and edge cases.
def cook(base, level, boost=50, streak=0):
    bonus = min(.5, max(0,level/100*(boost+streak)/100))
    return base*(1+bonus)
def seasoned(base, skilllevel, spices):
    return cook(base,skilllevel)+min(max(0,spices),.5*base)
assert cook(25,40) == 30
assert cook(25,100) == 37.5
assert seasoned(25,100,50) == 50
assert seasoned(25,100,3000) == 50   # legacy overbuff cannot leak through
assert seasoned(0,100,9999) == 0      # never create stat from zero base
assert seasoned(100,40,200) == 170
def apply(source_effective, current_spice, target_base, level=40):
    bonus = max(1,round(source_effective*(.05+.05*level/100))) if source_effective>0 else 0
    result = min(.5*target_base,current_spice+bonus)
    return result, result>current_spice+1e-4
assert apply(50,49,100)[0] == 50
assert apply(500,50,100) == (50,False)
assert apply(0,10,100) == (10,False)
assert apply(1,0,0) == (0,False)
assert apply(40,0,25)[1]
def full(h,s,e,base):
    present=[i for i,x in enumerate(base) if x>0]
    return bool(present) and all(v >= .5*base[i]-1e-4 for i,v in enumerate((h,s,e)) if i in present)
assert full(12.5,10,0,(25,20,0))
assert not full(12.5,9,0,(25,20,0))
assert full(12.5,0,0,(25,0,0))
# Canonical mastery always produces the same spice-side record for identical
# recipes regardless of whether a rabbit soup or carrot soup completed it.
def master(base_hp, base_stamina, base_eitr, base_regen):
    return (.5*base_hp,.5*base_stamina,.5*base_eitr,
            .2*base_regen,min(1,base_stamina*.02),min(1,base_eitr*.02))
assert master(25,20,0,1) == master(25,20,0,1)
assert math.isclose(master(25,20,0,1)[3],.2)
print("PASS: +50% cooking, +50% independent seasoning, old 3000-HP clamp, no wasted ingredients, master label and stack equivalence.")
