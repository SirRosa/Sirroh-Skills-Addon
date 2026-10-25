#!/usr/bin/env python3
from pathlib import Path
import json

src = {p.name: p.read_text(encoding='utf-8') for p in Path('src').glob('*.cs')}
assert {'Plugin.cs', 'SlayerCombat.cs', 'SlayerTrophies.cs', 'BlacksmithingBridge.cs'} <= src.keys()
all_code = '\n'.join(src.values())
assert all_code.count('[BepInPlugin(') == 1, 'Exactly one BepInEx plugin DLL'
assert '[BepInDependency(OverhaulGuid)]' in all_code
assert '[BepInIncompatibility("neithar.Slayer")]' in all_code
assert '[BepInIncompatibility("neobotics.valheim_mod.slayerskills")]' in all_code
assert 'Identifier = "neithar.Slayer"' in all_code, 'Retain previous skill ID'
assert 'Identifier = Guid + ".zawadiaka"' in all_code
assert 'Name = "Kolos"' in all_code and 'Name = "Zawadiaka"' in all_code
assert 'IsBrawlerWeapon' in all_code and 'BrawlerLevel' in all_code
assert 'else if (brawlerEnabled.Value && IsBrawlerWeapon(weapon))' in all_code
assert 'SlayerSkills_1847__C' in all_code, 'Legacy trophy collection migration'
assert 'sirroh.skillsaddon.headhunter.kills.v1' in all_code
assert 'CountKill' in all_code and 'ReceiveKill' in all_code
assert 'CharacterDeathCounter' in all_code and 'KillAwardedZdo' in all_code
assert 'ZabójstwaNaPunkt' in all_code and 'DużeStworzeniaNaPunkt' in all_code
assert 'FallenValkyrie,Serpent,Gjall,Morgen,Bjorn,Bjorn_sleeping,Unbjorn' in all_code
assert 'Player.m_localPlayer.m_customData' not in all_code  # Never mutate a global player blindly
assert '.m_customData[SaveKey]' in all_code
assert 'NetworkKey' in all_code and 'PublishAll' in all_code
assert 'InitialNetworkPublication' in all_code
assert 'OnlyHostMayAwardPoints' in all_code
assert 'NativeEligible' in all_code
assert 'ProtectPreviousCraftLevel' in all_code and 'ApplyCraftingBonuses' in src['BlacksmithingBridge.cs']
assert 'ItemEligibilityCache' in all_code
assert 'ApplyCraftingBonuses' not in src['SlayerCombat.cs']
assert 'hit.m_damage.Modify' in src['SlayerCombat.cs']
assert 'm_shared = ' not in all_code, 'Never overwrite Blacksmithing clones'
assert 'm_quality =' not in all_code
assert 'RaiseSkill("Blacksmithing"' not in all_code
assert 'CharacterDrop' in all_code and 'Ragdoll' in all_code
manifest = json.loads(Path('manifest.json').read_text(encoding='utf-8'))
assert manifest['name'] == 'SirrohSkillsAddon'
print("Static contracts passed: game-owned items, migration, isolated patches, one DLL.")
