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

foreach (var type in assembly.MainModule.Types.SelectMany(Descend)
    .Where(t => t.Name == "ItemData" || t.Name == "ItemDrop" ||
                t.Name == "InventoryGui" || t.Name == "InventoryGrid"))
    foreach (var method in type.Methods.Where(m => m.HasBody &&
        (m.Name.Contains("Tooltip") || m.Name.Contains("Food") || m.Name == "UpdateGui")))
    {
        Console.WriteLine("FOOD DETAIL METHOD " + type.Name + "." + method.Name);
        foreach (var instr in method.Body.Instructions.Where(i =>
            i.OpCode.Code == Mono.Cecil.Cil.Code.Ldstr))
        {
            string v = Convert.ToString(instr.Operand);
            if (!string.IsNullOrEmpty(v) && (v.IndexOf("food", StringComparison.OrdinalIgnoreCase) >= 0 ||
                v.IndexOf("heal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                v.IndexOf("stamina", StringComparison.OrdinalIgnoreCase) >= 0 ||
                v.IndexOf("eitr", StringComparison.OrdinalIgnoreCase) >= 0))
                Console.WriteLine("FOOD DETAIL KEY " + v.Replace("\\r","\\\\r").Replace("\\n","\\\\n")
                    .Substring(0, Math.Min(v.Length, 180)));
        }
    }
foreach (var probeName in new[] { "ItemData", "ItemDrop/ItemData" })
{
    foreach (var td in assembly.MainModule.Types.SelectMany(Descend)
        .Where(t => t.FullName == probeName))
    {
        Console.WriteLine("ZEN BRIDGE TYPE " + td.FullName + " BASE " + td.BaseType?.FullName);
        foreach (var fld in td.Fields.Where(f => f.Name == "m_customData" || f.Name == "m_shared"))
            Console.WriteLine("ZEN BRIDGE FIELD " + td.FullName + "." + fld.Name + " " + fld.FieldType.FullName);
    }
}

var invType = assembly.MainModule.Types.FirstOrDefault(t => t.Name == "Inventory");
var dataType = assembly.MainModule.Types.SelectMany(Descend).FirstOrDefault(t => t.FullName == "ItemDrop/ItemData");
foreach (var ty in new [] { invType, dataType }.Where(t => t != null))
{
    foreach (var m in ty.Methods.Where(m => m.Name.Contains("AddItem") || m.Name.Contains("RemoveItem") ||
        m.Name.Contains("GetAllItems") || m.Name.Contains("GetEmpty") || m.Name.Contains("FindEmpty") ||
        m.Name.Contains("Clone") || m.Name.Contains("Changed") || m.Name.Contains("CanAdd") ||
        m.Name.Contains("GetItemAt")))
    {
        Console.WriteLine("SEASON API " + ty.FullName + "::" + m.Name + "(" +
          string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name+" "+p.Name)) +
          ") -> " + m.ReturnType.FullName);
    }
    foreach (var field in ty.Fields.Where(f => f.Name.Contains("inventory") || f.Name.Contains("gridPos") || f.Name.Contains("stack")))
        Console.WriteLine("SEASON FIELD " + ty.FullName + "::" + field.Name + ": " + field.FieldType.FullName);
}

foreach (var method in (invType?.Methods ?? Enumerable.Empty<Mono.Cecil.MethodDefinition>())
    .Where(m => m.Name == "AddItem" && m.Parameters.Count == 5 &&
                m.Parameters[0].ParameterType.Name == "ItemData" && m.HasBody))
{
    Console.WriteLine("SEASON EXPLICIT IL START");
    foreach (var instruction in method.Body.Instructions.Take(180))
        Console.WriteLine("SEASON EXPLICIT IL " + instruction);
    Console.WriteLine("SEASON EXPLICIT IL END");
}

foreach (var method in (invType?.Methods ?? Enumerable.Empty<Mono.Cecil.MethodDefinition>())
    .Where(m => m.Name == "AddItem" && m.Parameters.Count == 2 &&
                m.Parameters[0].ParameterType.Name == "ItemData" &&
                m.HasBody))
{
    Console.WriteLine("SEASON ADDITEM IL START");
    foreach (var instruction in method.Body.Instructions.Take(150))
        Console.WriteLine("SEASON IL " + instruction);
    Console.WriteLine("SEASON ADDITEM IL END");
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
