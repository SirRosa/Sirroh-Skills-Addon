using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    // One spice unit seasons *all* items in a compatible base-stack group.
    // Plans are calculated from a snapshot, confirmed by the player, and
    // revalidated after the Valheim ZDO ownership handshake before any writes.
    internal static class GarmazerstwoMixing
    {
        private const float RelativeBaseThreshold = .85f;
        private static readonly FieldInfo CurrentChestField =
            AccessTools.Field(typeof(InventoryGui), "m_currentContainer");
        private static readonly FieldInfo ChestInventoryField =
            AccessTools.Field(typeof(Container), "m_inventory");
        private static readonly FieldInfo ChestViewField =
            AccessTools.Field(typeof(Container), "m_nview");
        private static readonly MethodInfo CheckAccessMethod =
            AccessTools.Method(typeof(Container), "CheckAccess", new[] { typeof(long) });
        private static readonly MethodInfo SaveMethod =
            AccessTools.Method(typeof(Container), "Save");
        private static readonly MethodInfo ChangedMethod =
            AccessTools.Method(typeof(Inventory), "Changed",
                new[] { typeof(bool), typeof(bool) });
        private static readonly MethodInfo AddAtMethod =
            AccessTools.Method(typeof(Inventory), "AddItem",
                new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int),
                    typeof(int), typeof(bool) });

        internal sealed class Group
        {
            internal string Prefab;
            internal string Name;
            internal string DataSignature;
            internal float Value;
            internal readonly List<ItemDrop.ItemData> Targets = new List<ItemDrop.ItemData>();
            internal readonly List<ItemDrop.ItemData> Spices = new List<ItemDrop.ItemData>();
            internal string Result;
            internal int Count => Targets.Sum(t => t.m_stack);
        }

        private sealed class Snapshot
        {
            internal ItemDrop.ItemData Item;
            internal ItemDrop.ItemData Backup;
            internal int Stack;
            internal Vector2i Position;
        }

        internal sealed class Plan
        {
            internal Container Container;
            internal Inventory Inventory;
            internal string Fingerprint;
            internal readonly List<Group> Bases = new List<Group>();
            internal readonly Dictionary<ItemDrop.ItemData,int> Consumed =
                new Dictionary<ItemDrop.ItemData,int>();
            internal int ConsumedCount;
            internal int BaseCount => Bases.Sum(g => g.Count);
        }

        private static bool _pending;
        private static int _confirmationFrame;
        private static Plan _pendingPlan;
        private static bool _working;

        internal static void Install() { }

        internal static void Shutdown()
        {
            _pendingPlan = null;
            _pending = false;
            _working = false;
        }

        internal static void Tick()
        {
            if (_pending && Time.frameCount > _confirmationFrame + 1 &&
                !UnifiedPopup.IsVisible())
            {
                _pending = false;
                _pendingPlan = null;
            }
        }

        internal static Container CurrentContainer(InventoryGui gui)
        {
            return gui == null ? null : CurrentChestField?.GetValue(gui) as Container;
        }

        private static Inventory Contents(Container chest)
        {
            return chest == null ? null : ChestInventoryField?.GetValue(chest) as Inventory;
        }

        private static ZNetView View(Container chest)
        {
            return chest == null ? null : ChestViewField?.GetValue(chest) as ZNetView;
        }

        private static bool Available(Container chest, Player player)
        {
            var gui = InventoryGui.instance;
            var view = View(chest);
            if (chest == null || player == null || gui == null ||
                CurrentContainer(gui) != chest || Contents(chest) == null ||
                view == null || !view.IsValid() || view.GetZDO() == null ||
                CheckAccessMethod == null || SaveMethod == null ||
                ChangedMethod == null) return false;
            if (chest.GetComponentInParent<Ship>() != null) return false;
            try
            {
                if (!(bool)CheckAccessMethod.Invoke(chest,
                    new object[] { player.GetPlayerID() })) return false;
                if (chest.m_checkGuardStone &&
                    !PrivateArea.CheckAccess(chest.transform.position, 0f, true, false))
                    return false;
            }
            catch (Exception) { return false; }
            return true;
        }

        private static string StableData(ItemDrop.ItemData item)
        {
            if (item == null) return "null";
            var sb = new StringBuilder();
            sb.Append(Garmazerstwo.Prefab(item)).Append('|')
              .Append(item.m_quality).Append('|').Append(item.m_stack).Append('|')
              .Append(item.m_gridPos.x).Append(',').Append(item.m_gridPos.y);
            if (item.m_customData != null)
            {
                foreach (var pair in item.m_customData.OrderBy(p => p.Key, StringComparer.Ordinal))
                    sb.Append('|').Append(pair.Key.Length).Append(':').Append(pair.Key)
                      .Append('=').Append(pair.Value?.Length ?? 0).Append(':').Append(pair.Value);
            }
            return sb.ToString();
        }

        private static string InventoryFingerprint(Inventory inventory)
        {
            if (inventory == null) return "";
            var parts = inventory.GetAllItems().Where(i => i != null)
                .OrderBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x)
                .Select(StableData);
            return string.Join("\n", parts);
        }

        private static string DataSignature(ItemDrop.ItemData item)
        {
            // Include all metadata, not just Sirroh's key: an item with a
            // different modded effect must never be changed into another stack.
            var sb = new StringBuilder().Append(item.m_quality);
            if (item.m_customData != null)
                foreach (var pair in item.m_customData.OrderBy(p => p.Key, StringComparer.Ordinal))
                    sb.Append('|').Append(pair.Key).Append('=').Append(pair.Value);
            return sb.ToString();
        }

        private static float Value(ItemDrop.ItemData item)
        {
            Garmazerstwo.Effective(item, out float hp, out float stamina,
                out float eitr, out float hpRegen, out _, out _);
            float duration = 1800f;
            try
            {
                var durationField = AccessTools.Field(item.m_shared.GetType(), "m_foodBurnTime");
                if (durationField != null)
                    duration = Convert.ToSingle(durationField.GetValue(item.m_shared),
                        CultureInfo.InvariantCulture);
            }
            catch (Exception) { }
            // Duration is deliberately a minor modifier; it cannot make a
            // mediocre ingredient outrank an obviously stronger meal.
            float durationFactor = Mathf.Clamp(Mathf.Sqrt(
                Mathf.Max(1f, duration) / 1800f), .85f, 1.15f);
            return (hp + stamina + 1.5f * eitr + 4f * hpRegen) * durationFactor;
        }

        internal static Plan Analyze(Container chest)
        {
            var inv = Contents(chest);
            if (inv == null) return null;
            var items = inv.GetAllItems().Where(item =>
                item != null && item.m_stack > 0 && !item.m_equipped &&
                Garmazerstwo.IsFood(item)).ToList();
            if (items.Count < 2) return null;

            // Score by food prefab: already-seasoned variants of the same
            // dish remain individual base groups but do not inflate the count
            // of distinct meal types.
            var ranked = items.GroupBy(Garmazerstwo.Prefab)
                .Select(g => new { Prefab = g.Key, Score = g.Max(Value) })
                .OrderByDescending(g => g.Score).ThenBy(g => g.Prefab,
                    StringComparer.Ordinal).ToList();
            if (ranked.Count < 2 || ranked[0].Score <= 0f) return null;
            float minimum = ranked[0].Score * RelativeBaseThreshold;
            var selected = new HashSet<string>(
                ranked.Where(g => g.Score >= minimum).Select(g => g.Prefab),
                StringComparer.Ordinal);

            var plan = new Plan { Container = chest, Inventory = inv,
                Fingerprint = InventoryFingerprint(inv) };
            foreach (var item in items.Where(i => selected.Contains(Garmazerstwo.Prefab(i))))
            {
                string prefab = Garmazerstwo.Prefab(item);
                string signature = DataSignature(item);
                var group = plan.Bases.FirstOrDefault(g =>
                    g.Prefab == prefab && g.DataSignature == signature);
                if (group == null)
                {
                    group = new Group { Prefab = prefab,
                        Name = Localization.instance != null
                            ? Localization.instance.Localize(item.m_shared.m_name)
                            : item.m_shared.m_name,
                        DataSignature = signature,
                        Value = Value(item) };
                    plan.Bases.Add(group);
                }
                group.Targets.Add(item);
            }
            plan.Bases.Sort((a,b) =>
            {
                int byValue = b.Value.CompareTo(a.Value);
                return byValue != 0 ? byValue :
                    string.CompareOrdinal(a.Prefab + a.DataSignature,
                                           b.Prefab + b.DataSignature);
            });
            var ingredients = items.Where(i => !selected.Contains(Garmazerstwo.Prefab(i)))
                .OrderBy(Garmazerstwo.Prefab, StringComparer.Ordinal)
                .ThenBy(i => i.m_gridPos.y).ThenBy(i => i.m_gridPos.x)
                .ToList();

            int index = 0;
            foreach (var source in ingredients)
            {
                for (int unit = 0; unit < source.m_stack; unit++)
                {
                    var target = plan.Bases[index % plan.Bases.Count];
                    // Source prefab cannot be the target prefab because
                    // all selected base prefabs are excluded from ingredients.
                    target.Spices.Add(source);
                    if (!plan.Consumed.ContainsKey(source)) plan.Consumed[source] = 0;
                    plan.Consumed[source]++;
                    plan.ConsumedCount++;
                    index++;
                }
            }
            return plan.Bases.Count > 0 && plan.ConsumedCount > 0 ? plan : null;
        }

        private static void Message(string message)
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, message);
        }

        internal static void Click()
        {
            if (_pending || _working || Garmazerstwo.Enabled?.Value != true) return;
            var player = Player.m_localPlayer;
            var chest = CurrentContainer(InventoryGui.instance);
            if (!Available(chest, player))
            {
                Message("Mieszaj: brak dostępu do otwartej skrzyni.");
                return;
            }
            if (!UnifiedPopup.IsAvailable() || UnifiedPopup.IsVisible()) return;
            var plan = Analyze(chest);
            if (plan == null)
            {
                Message("Mieszaj: brak odpowiednio słabych potraw do wykorzystania jako przyprawy.");
                return;
            }
            string names = string.Join(", ", plan.Bases
                .GroupBy(g => g.Prefab)
                .Select(g =>
                    g.Sum(v => v.Count) + "× " + g.First().Name)
                .Take(6).ToArray());
            if (plan.Bases.Select(g=>g.Prefab).Distinct().Count() > 6)
                names += " i pozostałe";
            _pending = true;
            _pendingPlan = plan;
            _confirmationFrame = Time.frameCount;
            try
            {
                UnifiedPopup.Push(new YesNoPopup("Mieszaj",
                    "Czy na pewno chcesz przyprawić " + names +
                    " pozostałą zawartością skrzyni?\n\n" +
                    "Wykorzystane przyprawy: " + plan.ConsumedCount +
                    "\nWzmocnione posiłki: " + plan.BaseCount +
                    "\nNowe sloty: 0",
                    Approve, Cancel, false, false));
            }
            catch (Exception e)
            {
                _pending = false;
                _pendingPlan = null;
                Plugin.Instance.Warning("Mieszaj popup: " + e.Message);
                Message("Mieszaj: nie można otworzyć potwierdzenia.");
            }
        }

        private static void Cancel()
        {
            if (UnifiedPopup.IsVisible()) UnifiedPopup.Pop();
            _pending = false;
            _pendingPlan = null;
        }

        private static void Approve()
        {
            if (UnifiedPopup.IsVisible()) UnifiedPopup.Pop();
            var plan = _pendingPlan;
            _pendingPlan = null;
            _pending = false;
            if (plan == null || Plugin.Instance == null || _working) return;
            Plugin.Instance.StartCoroutine(Execute(plan));
        }

        private static bool Revalidate(Plan plan)
        {
            return plan != null && Available(plan.Container, Player.m_localPlayer) &&
                ReferenceEquals(Contents(plan.Container), plan.Inventory) &&
                InventoryFingerprint(plan.Inventory) == plan.Fingerprint;
        }

        private static IEnumerator Execute(Plan plan)
        {
            _working = true;
            try
            {
                if (!Revalidate(plan)) { Message("Mieszaj: zawartość skrzyni się zmieniła."); yield break; }
                var view = View(plan.Container);
                if (!view.IsOwner())
                {
                    view.ClaimOwnership();
                    float expiry = Time.realtimeSinceStartup + .9f;
                    while (view != null && view.IsValid() && !view.IsOwner() &&
                           Time.realtimeSinceStartup < expiry)
                        yield return null;
                }
                if (view == null || !view.IsOwner() || !Revalidate(plan))
                {
                    Message("Mieszaj: brak własności skrzyni lub zawartość uległa zmianie.");
                    yield break;
                }

                // Build every result in memory before touching the inventory.
                float skillLevel = Garmazerstwo.SkillLevel(Player.m_localPlayer);
                foreach (var group in plan.Bases)
                {
                    var exemplar = group.Targets[0];
                    var record = Garmazerstwo.Default(exemplar);
                    foreach (var ingredient in group.Spices)
                        if (!Garmazerstwo.AddSpice(record, ingredient, exemplar, skillLevel))
                        {
                            Message("Mieszaj: nie udało się przeliczyć potrawy.");
                            yield break;
                        }
                    group.Result = Garmazerstwo.Serialize(record);
                }
                if (!Revalidate(plan))
                {
                    Message("Mieszaj: zawartość skrzyni uległa zmianie.");
                    yield break;
                }

                // Preserve every consumed stack for recovery. We do not create
                // any new stack on the successful path: only metadata of the
                // existing base stacks changes and ingredients are removed.
                var backups = plan.Consumed.Keys.Concat(
                    plan.Bases.SelectMany(g=>g.Targets)).Distinct().Select(item =>
                    new Snapshot { Item = item, Backup = item.Clone(),
                        Stack = item.m_stack, Position = item.m_gridPos }).ToList();
                bool success = false;
                try
                {
                    foreach (var group in plan.Bases)
                        foreach (var item in group.Targets)
                            item.m_customData[Garmazerstwo.Key] = group.Result;
                    foreach (var take in plan.Consumed)
                        if (!plan.Inventory.RemoveItem(take.Key, take.Value))
                            throw new InvalidOperationException("RemoveItem failed");
                    ChangedMethod.Invoke(plan.Inventory, new object[] { false, false });
                    SaveMethod.Invoke(plan.Container, null);
                    success = true;
                }
                catch (Exception e)
                {
                    // Restore metadata and original quantities. A fully removed
                    // ingredient requires a native exact-slot reinsertion.
                    foreach (var snapshot in backups)
                    {
                        try
                        {
                            var item = snapshot.Item;
                            if (plan.Inventory.ContainsItem(item))
                            {
                                item.m_stack = snapshot.Stack;
                                RestoreData(item, snapshot.Backup);
                            }
                            else if (AddAtMethod != null)
                            {
                                AddAtMethod.Invoke(plan.Inventory, new object[] {
                                    snapshot.Backup, snapshot.Stack,
                                    snapshot.Position.x, snapshot.Position.y, false });
                            }
                        }
                        catch (Exception restoreError)
                        {
                            Plugin.Instance.Warning("Mieszaj: ROLLBACK FAILED: " +
                                restoreError.Message);
                        }
                    }
                    try
                    {
                        ChangedMethod.Invoke(plan.Inventory, new object[] { false, false });
                        SaveMethod.Invoke(plan.Container, null);
                    }
                    catch (Exception) { }
                    Plugin.Instance.Warning("Mieszaj: transakcja anulowana: " + e);
                    Message("Mieszaj: błąd zapisu, przywrócono zawartość skrzyni.");
                }
                if (success)
                {
                    Garmazerstwo.AwardSpiceXp(plan.ConsumedCount);
                    Message("Mieszaj: przyprawiono " + plan.BaseCount +
                        " posiłków, zużyto " + plan.ConsumedCount + " przypraw.");
                }
            }
            finally { _working = false; }
        }

        private static void RestoreData(ItemDrop.ItemData item, ItemDrop.ItemData backup)
        {
            if (item.m_customData == null || backup.m_customData == null) return;
            item.m_customData.Clear();
            foreach (var pair in backup.m_customData)
                item.m_customData[pair.Key] = pair.Value;
        }
    }
}