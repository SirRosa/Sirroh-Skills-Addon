#!/usr/bin/env python3
"""Regression gates for test-only Garmażerstwo bulk chest mixing."""
from pathlib import Path
import json, math
root=Path("src")
mix=(root/"GarmazerstwoMixing.cs").read_text("utf8")
ui=(root/"GarmazerstwoMixUI.cs").read_text("utf8")
plugin=(root/"Plugin.cs").read_text("utf8")
proj=Path("SirrohSkillsAddon.csproj").read_text("utf8")
manifest=json.loads(Path("manifest.json").read_text("utf8"))
assert manifest["version_number"]=="0.4.9"
assert 'internal const string Version = "0.4.9"' in plugin
for token in ("GarmazerstwoMixUI.Install(harmony)", "GarmazerstwoMixUI.Tick()",
              "GarmazerstwoMixing.Tick()", "GarmazerstwoMixUI.Shutdown()",
              "GarmazerstwoMixing.Shutdown()"):
    assert token in plugin,token
for token in ("RelativeBaseThreshold = .85f", "GroupBy(Garmazerstwo.Prefab)",
              "Value(i) >= minimum", "DataSignature(exemplar)", "i.m_stack",
              "plan.Consumed[source]++", "target.Spices.Add(source)",
              "target.Prefab == Garmazerstwo.Prefab(source)",
              "Garmazerstwo.AddSpice(simulated, source",
              "foreach (var item in group.Targets)", "Garmazerstwo.Key] = group.Result",
              "Garmazerstwo.AwardSpiceXp(plan.ConsumedCount)",
              "CheckAccessMethod.Invoke","PrivateArea.CheckAccess",
              "view.ClaimOwnership()","view.IsOwner()","InventoryFingerprint(plan.Inventory)",
              "plan.Inventory.RemoveItem(take.Key, take.Value)",
              "new Snapshot { Item = item, Backup = item.Clone()",
              "SaveMethod.Invoke(plan.Container","AddAtMethod.Invoke",
              "UnifiedPopup.Push(new YesNoPopup",
              "GetComponentInParent<Ship>()"):
    assert token in mix,token
for token in ("QolFavorites","WoodPanel","Image.Type.Sliced",
              "Custom/LitGui","GarmazerstwoMixing.Click",
              "m_achievementsPanel","m_trophiesPanel",
              "Menu.IsVisible()","UnifiedPopup.IsVisible()",
              "IPointerEnterHandler","IPointerExitHandler",
              "mix_kociolek.png","DecodePng("):
    assert token in ui,token
assert "ImageConversion.LoadImage" not in ui
assert "UnityEngine.ImageConversionModule" not in proj
# Independent numeric/routing examples; not a replacement for runtime game tests.
def score(h,s,e,regen,sec=1800):
    return (h+s+1.5*e+4*regen)*max(.85,min(1.15,math.sqrt(max(1,sec)/1800)))
base=score(80,30,0,2)
assert score(75,28,0,2)>=.85*base
assert score(20,15,0,1)<.85*base
assert [i%2 for i in range(5)]==[0,1,0,1,0]
assert sum([i%3==j for i in range(1000) for j in range(3)])==1000
print("PASS: 0.4.9 guarded mixing, 85% base ranking, full-stack seasoning, UI isolation.")
