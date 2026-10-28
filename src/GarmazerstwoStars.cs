using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sirroh.SkillsAddon
{
    // Overlay lives only inside each InventoryGrid slot and follows the item.
    // It never alters vanilla quality, prefab sprites, QOL slot sorting or extra slots.
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    internal static class GarmazerstwoStars
    {
        private const string ObjectName = "SirrohGarmaStars";

        [HarmonyPostfix]
        private static void Draw(Inventory ___m_inventory, List<InventoryElement> ___m_elements)
        {
            if (Garmazerstwo.Enabled?.Value != true ||
                ___m_inventory == null || ___m_elements == null) return;
            try
            {
                int width = ___m_inventory.GetWidth();
                int count = Math.Min(___m_elements.Count, width * ___m_inventory.GetHeight());
                for (int i = 0; i < count; i++)
                {
                    Image anchor = ___m_elements[i]?.m_queued;
                    if (anchor == null) continue;
                    var existing = anchor.transform.Find(ObjectName);
                    var text = existing == null ? null : existing.GetComponent<TextMeshProUGUI>();
                    ItemDrop.ItemData item = ___m_inventory.GetItemAt(i % width, i / width);
                    var record = Garmazerstwo.IsFood(item) ? Garmazerstwo.Read(item) : null;
                    int stars = record?.Stars ?? 0;
                    if (stars == 0)
                    {
                        if (text != null) text.gameObject.SetActive(false);
                        continue;
                    }
                    if (text == null)
                    {
                        var obj = new GameObject(ObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
                        obj.transform.SetParent(anchor.transform, false);
                        var rect = obj.GetComponent<RectTransform>();
                        rect.anchorMin = new Vector2(.05f, .72f);
                        rect.anchorMax = new Vector2(.96f, 1f);
                        rect.offsetMin = rect.offsetMax = Vector2.zero;
                        text = obj.GetComponent<TextMeshProUGUI>();
                        text.raycastTarget = false;
                        text.enableWordWrapping = false;
                        text.fontSize = 15f;
                        text.fontStyle = FontStyles.Bold;
                        text.alignment = TextAlignmentOptions.TopRight;
                        text.color = new Color(1f, .79f, .18f, 1f);
                        text.overflowMode = TextOverflowModes.Overflow;
                    }
                    if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
                    // The count is intentional: legible at 64 px and does not overlap
                    // five glyphs across food art. It represents 1–5 seasoning stars.
                    string mark = "★" + stars;
                    if (text.text != mark) text.text = mark;
                }
            }
            catch (Exception e)
            {
                Plugin.Instance?.Warning("Garmażerstwo stars overlay: " + e.GetType().Name);
            }
        }
    }
}
