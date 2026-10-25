using System;
using System.Linq;
using Mono.Cecil;

if (args.Length < 1 || !System.IO.File.Exists(args[0]))
    throw new ArgumentException("Pass path to assembly_valheim.dll");
using var assembly = AssemblyDefinition.ReadAssembly(args[0]);

string[][] methodTargets =
{
    new[] { "Character", "Damage", "ApplyPushback", "AddStaggerDamage", "RPC_Damage", "OnDeath" },
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

static System.Collections.Generic.IEnumerable<TypeDefinition> Descend(TypeDefinition type)
{
    yield return type;
    foreach (var nested in type.NestedTypes)
        foreach (var descendant in Descend(nested))
            yield return descendant;
}
var itemEnums = assembly.MainModule.Types.SelectMany(Descend)
    .Where(t => t.IsEnum && t.Name == "ItemType").ToList();
if (itemEnums.Count == 0)
{
    Console.Error.WriteLine("Cannot find ItemType enum");
    failures++;
}
else
{
    var values = itemEnums.SelectMany(t => t.Fields.Where(f => f.IsLiteral)
        .Select(f => new { f.Name, Value = Convert.ToInt32(f.Constant) })).ToList();
    foreach (var check in new[] { (Name: "OneHandedWeapon", Value: 3), (Name: "TwoHandedWeapon", Value: 14) })
    {
        bool present = values.Any(v => v.Name == check.Name && v.Value == check.Value);
        Console.WriteLine("ITEM TYPE " + check.Name + " = " + check.Value + (present ? " OK" : " MISMATCH"));
        if (!present) failures++;
    }
}


if (args.Length > 1 && System.IO.File.Exists(args[1]))
{
    using var bep = AssemblyDefinition.ReadAssembly(args[1]);
    var entryType = bep.MainModule.Types.FirstOrDefault(t => t.FullName == "BepInEx.Configuration.ConfigEntryBase");
    if (entryType != null)
    {
        var getter = entryType.Properties.FirstOrDefault(p => p.Name == "Description");
        var fields = entryType.Fields.Where(f => f.Name.Contains("Description")).Select(f => f.Name).ToArray();
        Console.WriteLine("BEPINEX ConfigEntryBase.Description property: " + (getter != null) +
            " | setter: " + (getter?.SetMethod != null) + " | fields: " + string.Join(",", fields));
    }
}

if (failures > 0) throw new Exception("Valheim 1.0 runtime target drift: " + failures + " missing methods");
Console.WriteLine("Harmony target inspection passed.");
