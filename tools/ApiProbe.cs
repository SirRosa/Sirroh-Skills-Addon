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


string[][] foodTargets =
{
    new[] { "Player", "Load", "EatFood", "GetTotalFoodValue", "UseStamina", "UseEitr", "Jump", "GetJogSpeedFactor", "GetRunSpeedFactor", "GetSwimSpeedFactor", "GetFood" },
    new[] { "CookingStation", "OnInteract", "SpawnItem", "RPC_RemoveDoneItem", "GetSlot", "IsItemDone" },
    new[] { "InventoryGrid", "OnLeftDown", "GetButtonPos" },
    new[] { "Inventory", "AddItem", "FindFreeStackItem", "MoveItemToThis", "Changed" },
    new[] { "ItemDrop/ItemData", "IsSameType", "GetTooltip" },
    new[] { "Tameable", "DecreaseRemainingTime", "UpdateTaming" },
};
foreach (var target in foodTargets)
{
    var ty = assembly.MainModule.Types.SelectMany(Descend)
        .FirstOrDefault(t => t.FullName.Replace("/", "/") == target[0] || t.Name == target[0]);
    if (ty == null) { Console.WriteLine("FOOD PROBE TYPE MISSING " + target[0]); continue; }
    foreach (var name in target.Skip(1))
    {
        var ms = ty.Methods.Where(m => m.Name == name).ToList();
        Console.WriteLine("FOOD PROBE " + target[0] + "." + name + ": " +
            (ms.Count == 0 ? "MISSING" : string.Join(" | ", ms.Select(m => "(" +
                string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name + " " + p.Name)) +
                ") -> " + m.ReturnType.Name))));
    }
}
var foodType = assembly.MainModule.Types.SelectMany(Descend)
    .FirstOrDefault(t => t.FullName == "Player/Food" || t.Name == "Food");
if (foodType != null)
    foreach (var fld in foodType.Fields)
        Console.WriteLine("FOOD FIELD " + fld.Name + " " + fld.FieldType.Name);

foreach (var typeName in new[] { "Player", "Character", "Humanoid", "CookingStation" })
{
    var ty = assembly.MainModule.Types.FirstOrDefault(t => t.Name == typeName);
    if (ty == null) continue;
    foreach (var m in ty.Methods.Where(m =>
        m.Name.IndexOf("Jump", StringComparison.OrdinalIgnoreCase) >= 0 ||
        m.Name.IndexOf("Swim", StringComparison.OrdinalIgnoreCase) >= 0 ||
        m.Name.IndexOf("Cook", StringComparison.OrdinalIgnoreCase) >= 0))
        Console.WriteLine("FOOD SEARCH " + typeName + "." + m.Name + "(" +
            string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name + " " + p.Name)) +
            ") -> " + m.ReturnType.Name);
}


foreach (var tn in new[] { "InventoryElement", "InventoryGrid", "ItemDrop/ItemData" })
{
    var ty = assembly.MainModule.Types.SelectMany(Descend)
        .FirstOrDefault(t => t.Name == tn || t.FullName == tn);
    if (ty == null) continue;
    foreach (var fld in ty.Fields.Where(f =>
        f.Name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0 ||
        f.Name.IndexOf("quality", StringComparison.OrdinalIgnoreCase) >= 0 ||
        f.Name.IndexOf("queued", StringComparison.OrdinalIgnoreCase) >= 0 ||
        f.Name.IndexOf("element", StringComparison.OrdinalIgnoreCase) >= 0))
        Console.WriteLine("FOOD UI FIELD " + tn + "." + fld.Name + " : " + fld.FieldType.FullName);
    foreach (var m in ty.Methods.Where(m => m.Name == "GetTooltip" || m.Name == "UpdateGui"))
    {
        Console.WriteLine("FOOD UI METHOD " + ty.Name + "." + m.Name + "(" +
            string.Join(", ", m.Parameters.Select(p => p.ParameterType.FullName)) + ")");
        if (m.HasBody)
            foreach (var ins in m.Body.Instructions.Where(i => i.OpCode.Code == Mono.Cecil.Cil.Code.Ldstr))
            {
                var val = Convert.ToString(ins.Operand);
                if (!string.IsNullOrEmpty(val) && (val.Contains("food") || val.Contains("quality") || val.Contains("item_")))
                    Console.WriteLine("FOOD UI STRING " + val.Replace("\\n", "\\n").Replace("\\r","").Substring(0, Math.Min(180, val.Length)));
            }
    }
}
// Game-version diagnostic: read the currently fetched Steam dedicated server's
// managed assembly metadata without asserting that it is a tested 1.0.21 client.
foreach (var vt in assembly.MainModule.Types.Where(t =>
    t.Name.IndexOf("Version", StringComparison.OrdinalIgnoreCase) >= 0).Take(12))
    foreach (var fld in vt.Fields.Where(f =>
        f.Name.IndexOf("version", StringComparison.OrdinalIgnoreCase) >= 0).Take(16))
        Console.WriteLine("GAME VERSION REFERENCE " + vt.Name + "." + fld.Name +
            " = " + (fld.HasConstant ? Convert.ToString(fld.Constant) : "<runtime>"));

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
