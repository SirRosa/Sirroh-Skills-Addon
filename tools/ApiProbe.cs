using System;
using System.Linq;
using Mono.Cecil;

if (args.Length != 1 || !System.IO.File.Exists(args[0]))
    throw new ArgumentException("Pass path to assembly_valheim.dll");
using var assembly = AssemblyDefinition.ReadAssembly(args[0]);

string[][] methodTargets =
{
    new[] { "Character", "Damage", "ApplyPushback", "AddStaggerDamage", "RPC_Damage" },
    new[] { "CharacterDrop", "OnDeath", "DropItems" },
    new[] { "Ragdoll", "SaveLootList", "SpawnLoot" },
    new[] { "Player", "Start", "Save", "Update", "OnDestroy" },
    new[] { "Skills", "LowerAllSkills", "CheatRaiseSkill", "CheatResetSkill" },
    new[] { "InventoryGui", "UpdateTrophyList" },
    new[] { "ZNetScene", "Awake" },
    new[] { "Game", "Start" },
};
int failures = 0;
foreach (var target in methodTargets)
{
    var type = assembly.MainModule.Types.FirstOrDefault(t => t.FullName == target[0]);
    if (type == null)
    {
        Console.Error.WriteLine("MISSING TYPE: " + target[0]);
        failures++;
        continue;
    }
    foreach (var methodName in target.Skip(1))
    {
        var matches = type.Methods.Where(m => m.Name == methodName).ToList();
        if (matches.Count == 0)
        {
            Console.Error.WriteLine("MISSING: " + target[0] + "." + methodName);
            failures++;
        }
        else foreach (var method in matches)
            Console.WriteLine("OK: " + target[0] + "." + methodName + "(" +
                string.Join(", ", method.Parameters.Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
    }
}
if (failures > 0) throw new Exception("Valheim 1.0 runtime target drift: " + failures + " missing methods");
Console.WriteLine("Harmony target inspection passed.");
