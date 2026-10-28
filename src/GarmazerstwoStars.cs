using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;

namespace Sirroh.SkillsAddon
{
    // Reuse the quality counter Valheim already positions over each inventory icon.
    // Only its visible label is changed: m_quality on the item is never modified.\n    // Native numeric quality (1-5) avoids missing star glyphs in Valheim fonts.
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class GarmazerstwoStars
    {
        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void Draw(Inventory ___m_inventory, List<InventoryElement> ___m_elements)
        {
            if (Garmazerstwo.Enabled?.Value != true ||
                ___m_inventory == null || ___m_elements == null) return;
            int width = ___m_inventory.GetWidth();
            if (width <= 0) return;
            int count = Math.Min(___m_elements.Count, width * ___m_inventory.GetHeight());
            for (int index = 0; index < count; index++)
            {
                var counter = ___m_elements[index]?.m_quality;
                if (counter == null) continue;
                var item = ___m_inventory.GetItemAt(index % width, index / width);
                var record = Garmazerstwo.IsFood(item) ? Garmazerstwo.Read(item) : null;
                int stars = record?.Stars ?? 0;
                // For ordinary items, let the original game own this label.
                if (stars <= 0) continue;
                string value = stars.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (counter.text != value) counter.text = value;
                // Vanilla hides its quality counter on ordinary quality-1 food.
                // Enable it for seasoned food without creating another UI layer.
                if (!counter.gameObject.activeSelf) counter.gameObject.SetActive(true);
                if (!counter.enabled) counter.enabled = true;
            }
        }
    }
}
