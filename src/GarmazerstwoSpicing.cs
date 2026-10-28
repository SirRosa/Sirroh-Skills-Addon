using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Sirroh.SkillsAddon
{
    // The source is always the dragged food. The target is the clicked food in the player grid.
    // Strict source/target revalidation is done AFTER the modal confirmation.
    internal static class GarmazerstwoSpicing
    {
        private static readonly FieldInfo DragItem = AccessTools.Field(typeof(InventoryGui), "m_dragItem");
        private static readonly FieldInfo DragInventory = AccessTools.Field(typeof(InventoryGui), "m_dragInventory");
        private static readonly FieldInfo DragAmount = AccessTools.Field(typeof(InventoryGui), "m_dragAmount");
        private static readonly FieldInfo GridInventory = AccessTools.Field(typeof(InventoryGrid), "m_inventory");
        private static readonly MethodInfo ButtonPos = AccessTools.Method(typeof(InventoryGrid),
            "GetButtonPos", new[] { typeof(GameObject) });
        private static readonly MethodInfo ClearDrag = AccessTools.Method(typeof(InventoryGui),
            "SetupDragItem", new[] { typeof(ItemDrop.ItemData), typeof(Inventory), typeof(int) });
        private static readonly MethodInfo Changed = AccessTools.Method(typeof(Inventory),
            "Changed", new[] { typeof(bool), typeof(bool) });

        private static ItemDrop.ItemData source, target;
        private static Inventory sourceInventory, targetInventory;
        private static Vector2i sourcePos, targetPos;
        private static int amount;
        private static int sourceStack, targetStack;
        private static string sourceData, targetData;
        private static int pendingFrame;
        private static bool pending;

        internal static void Install(Harmony harmony)
        {
            var method = AccessTools.Method(typeof(InventoryGrid), "OnLeftDown");
            if (method == null || DragItem == null || DragInventory == null ||
                DragAmount == null || GridInventory == null || ButtonPos == null || ClearDrag == null)
            {
                Plugin.Instance.Warning("Garmażerstwo: API przeciągania niezgodne; wyłączono przyprawianie.");
                return;
            }
            harmony.Patch(method, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(GarmazerstwoSpicing), nameof(InterceptDrop))));
        }

        private static bool InterceptDrop(InventoryGrid __instance, object clickHandler)
        {
            if (Garmazerstwo.Enabled?.Value != true || pending || clickHandler == null ||
                InventoryGui.instance == null || Player.m_localPlayer == null ||
                __instance != InventoryGui.instance.m_playerGrid) return true;
            var gui = InventoryGui.instance;
            var src = DragItem.GetValue(gui) as ItemDrop.ItemData;
            var srcInv = DragInventory.GetValue(gui) as Inventory;
            if (src == null || srcInv == null) return true;
            // Never steal operations targeting other mods' external or protected inventories.
            if (srcInv != Player.m_localPlayer.GetInventory()) return true;
            Vector2i position;
            try { position = (Vector2i)ButtonPos.Invoke(__instance,
                new object[] { ((Component)clickHandler).gameObject }); }
            catch (Exception) { return true; }
            var dstInv = GridInventory.GetValue(__instance) as Inventory;
            if (dstInv == null || dstInv != Player.m_localPlayer.GetInventory() ||
                position.x < 0 || position.y < 0) return true;
            var dst = dstInv.GetItemAt(position.x, position.y);
            if (dst == null || ReferenceEquals(src, dst) ||
                !Garmazerstwo.IsFood(src) || !Garmazerstwo.IsFood(dst)) return true;

            // This is a food-on-food gesture; consume the event even on invalid conditions.
            int dragCount = Convert.ToInt32(DragAmount.GetValue(gui));
            int count = Mathf.Min(dst.m_stack, Mathf.Min(src.m_stack, dragCount));
            if (count < 1)
            {
                Notify("Brak posiłków do przyprawienia.");
                return false;
            }
            // If only part of the target stack is improved, the untouched
            // remainder must stay at its original slot, with its original data.
            if (count < dst.m_stack && !HasFreeSlot(dstInv))
            {
                Notify("Przyprawianie: zwolnij jeden slot na nieulepszoną część stosu.");
                return false;
            }
            if (!UnifiedPopup.IsAvailable() || UnifiedPopup.IsVisible())
            {
                Notify("Nie można otworzyć potwierdzenia przyprawiania.");
                return false;
            }
            source = src; sourceInventory = srcInv; sourcePos = src.m_gridPos;
            target = dst; targetInventory = dstInv; targetPos = dst.m_gridPos;
            amount = count;
            sourceStack = src.m_stack;
            targetStack = dst.m_stack;
            sourceData = Fingerprint(src);
            targetData = Fingerprint(dst);
            pending = true;
            pendingFrame = Time.frameCount;
            try
            {
                string a = Localization.instance?.Localize(src.m_shared.m_name) ?? src.m_shared.m_name;
                string b = Localization.instance?.Localize(dst.m_shared.m_name) ?? dst.m_shared.m_name;
                UnifiedPopup.Push(new YesNoPopup("Potwierdzenie przyprawiania",
                    "Czy chcesz wykorzystać " + count + " × " + a + " jako przyprawę dla " +
                    count + " × " + b + "?\n" + a + " zostanie utracone.",
                    Approve, Cancel, false, false));
                ClearDrag.Invoke(gui, new object[] { null, null, 0 });
            }
            catch (Exception e)
            {
                Plugin.Instance.Warning("Garmażerstwo popup: " + e.GetType().Name);
                ClearPending();
                Notify("Nie udało się rozpocząć przyprawiania. Nic nie zużyto.");
            }
            return false;
        }

        internal static void Tick()
        {
            if (pending && Time.frameCount > pendingFrame + 1 &&
                !UnifiedPopup.IsVisible()) ClearPending();
        }

        private static void Approve()
        {
            UnifiedPopup.Pop();
            ItemDrop.ItemData a = source, b = target;
            Inventory origin = sourceInventory, dest = targetInventory;
            Vector2i aPos = sourcePos, bPos = targetPos;
            int n = amount;
            int expectedSource = sourceStack, expectedTarget = targetStack;
            string expectedSourceData = sourceData, expectedTargetData = targetData;
            ClearPending();
            if (Player.m_localPlayer == null || a == null || b == null || origin == null ||
                dest == null || a == b || n < 1 ||
                origin != Player.m_localPlayer.GetInventory() ||
                dest != Player.m_localPlayer.GetInventory() ||
                !ReferenceEquals(origin.GetItemAt(aPos.x, aPos.y), a) ||
                !ReferenceEquals(dest.GetItemAt(bPos.x, bPos.y), b) ||
                a.m_stack != expectedSource || b.m_stack != expectedTarget ||
                a.m_stack < n || b.m_stack < n ||
                Fingerprint(a) != expectedSourceData ||
                Fingerprint(b) != expectedTargetData ||
                !Garmazerstwo.IsFood(a) || !Garmazerstwo.IsFood(b))
            {
                Notify("Przyprawianie anulowane: zawartość ekwipunku uległa zmianie.");
                return;
            }
            var record = Garmazerstwo.Default(b);
            if (!Garmazerstwo.AddSpice(record, a, b, Garmazerstwo.SkillLevel(Player.m_localPlayer)))
            {
                Notify("Nie udało się obliczyć przyprawienia. Nic nie zużyto.");
                return;
            }
            string result = Garmazerstwo.Serialize(record);
            if (n == b.m_stack)
            {
                // The entire target stack receives the same boost.
                if (!origin.RemoveItem(a, n))
                {
                    Notify("Nie udało się zużyć przyprawy. Posiłek pozostał bez zmian.");
                    return;
                }
                b.m_customData[Garmazerstwo.Key] = result;
            }
            else
            {
                // Native Inventory API: clone target (including metadata), place
                // upgraded portion in an explicitly empty grid cell. Original B
                // remains with its old stats and untouched count.
                Vector2i slot;
                if (!TryFindFreeSlot(dest, out slot))
                {
                    Notify("Brak wolnego slotu na automatyczny podział stosu.");
                    return;
                }

                ItemDrop.ItemData seasoned = b.Clone();
                seasoned.m_stack = n;
                seasoned.m_gridPos = slot;
                seasoned.m_customData[Garmazerstwo.Key] = result;
                // The ingredient is NOT consumed before a valid, separate
                // stack exists; failure cannot turn the source into nothing.
                // This overload adds exactly n units to the selected cell.
                // Verified against Valheim 1.0.21 IL: it creates another clone
                // in the inventory, so do NOT test reference equality with the
                // supplied (consumed) ItemData object.
                bool inserted = dest.AddItem(seasoned, n, slot.x, slot.y, false);
                ItemDrop.ItemData placed = dest.GetItemAt(slot.x, slot.y);
                if (!inserted || placed == null || placed.m_stack != n ||
                    Fingerprint(placed) != result)
                {
                    // Never remove a pre-existing item from an occupied slot.
                    if (inserted && placed != null && Fingerprint(placed) == result)
                        dest.RemoveItem(placed);
                    Notify("Nie udało się wydzielić przyprawionego stosu. Nic nie zużyto.");
                    return;
                }

                b.m_stack -= n;
                if (!origin.RemoveItem(a, n))
                {
                    // Transaction rollback: restore original B and remove the
                    // newly created stack; source A was not consumed.
                    b.m_stack += n;
                    dest.RemoveItem(placed);
                    try { Changed?.Invoke(dest, new object[] { false, false }); }
                    catch (Exception) { }
                    Notify("Nie udało się zużyć przyprawy. Przywrócono poprzednie stosy.");
                    return;
                }
            }

            try { Changed?.Invoke(dest, new object[] { false, false }); }
            catch (Exception) { /* The committed item data remains intact. */ }
            Garmazerstwo.AwardSpiceXp(n);
            Notify("Przyprawiono " + n + " × posiłek.");
        }

        private static string Fingerprint(ItemDrop.ItemData item)
        {
            if (item?.m_customData == null) return null;
            return item.m_customData.TryGetValue(Garmazerstwo.Key, out string data) ? data : null;
        }

        private static bool TryFindFreeSlot(Inventory inv, out Vector2i slot)
        {
            slot = new Vector2i(-1, -1);
            if (inv == null) return false;
            // FindEmptySlot is private in Valheim 1.0.21. Scan only
            // actual free cells in the player's vanilla inventory grid.
            for (int y = 0; y < inv.GetHeight(); y++)
            for (int x = 0; x < inv.GetWidth(); x++)
                if (inv.GetItemAt(x, y) == null)
                {
                    slot = new Vector2i(x, y);
                    return true;
                }
            return false;
        }

        private static bool HasFreeSlot(Inventory inv)
        {
            Vector2i free;
            return TryFindFreeSlot(inv, out free);
        }

        private static void Cancel()
        {
            UnifiedPopup.Pop();
            ClearPending();
        }

        private static void ClearPending()
        {
            pending = false;
            source = null; target = null;
            sourceInventory = null; targetInventory = null;
            amount = 0;
            sourceStack = targetStack = 0;
            sourceData = targetData = null;
        }

        private static void Notify(string text)
        {
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, text);
        }

        internal static void Shutdown() => ClearPending();
    }
}