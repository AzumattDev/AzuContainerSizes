using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AzuContainerSizes;

[HarmonyPatch(typeof(Container), nameof(Container.Awake))]
public static class ContainerAwakePatch
{
    private static void Postfix(Container __instance)
    {
        ContainerFunctions.ApplyConfiguredSize(__instance);
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
public static class ContainerInteractPatch
{
    private static void Prefix(Container __instance, Humanoid character, bool hold, bool alt)
    {
        ContainerFunctions.ApplyConfiguredSize(__instance);
    }
}

public static class ContainerFunctions
{
    public static void ApplyConfiguredSize(Container container)
    {
        if (!IsValidContainer(container))
            return;

        Inventory inventory = container.GetInventory();
        if (inventory == null)
            return;

        int currentRows = inventory.m_height;
        int currentCols = inventory.m_width;

        Transform root = container.transform.root;
        string inventoryName = root ? root.name.Trim().Replace("(Clone)", "") : container.name;

        int newRows = currentRows;
        int newCols = currentCols;

        ApplySizeConfigForName(ref inventoryName, ref newRows, ref newCols);

        // Safety clamp in case someone hand-edits configs to garbage
        newRows = Mathf.Max(1, newRows);
        newCols = Mathf.Max(1, newCols);

        // Nothing changed – bail early
        if (newRows == currentRows && newCols == currentCols)
            return;

        // Protect items before we actually change the size
        ProtectItemsOnResize(container, inventory, newRows, newCols);

        inventory.m_height = newRows;
        inventory.m_width = newCols;

        try
        {
            inventory.Changed();
        }
        catch (Exception e)
        {
            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogDebug($"Failed to invoke Inventory.Changed for container '{inventoryName}': {e}");
        }
    }

    public static void UpdateContainerSize()
    {
        foreach (Container container in Resources.FindObjectsOfTypeAll<Container>())
        {
            ApplyConfiguredSize(container);
        }
    }

    private static bool IsValidContainer(Container container)
    {
        if (!container)
            return false;

        if (!container.m_nview || !container.m_nview.IsValid())
            return false;

        ZDO zdo = container.m_nview.GetZDO();
        if (zdo == null)
            return false;

        if (zdo.GetLong(ZDOVars.s_creator) == 0L)
            return false;

        if (!container.m_nview.IsOwner())
            return false;

        return container.GetInventory() != null;
    }

    private static void ApplySizeConfigForName(ref string inventoryName, ref int inventoryRows, ref int inventoryColumns)
    {
        if (AzuContainerSizesPlugin.ChestContainerControl.Value.IsOn())
        {
            switch (inventoryName)
            {
                // Personal chest
                case "piece_chest_private":
                    inventoryRows = AzuContainerSizesPlugin.PersonalRow.Value;
                    inventoryColumns = AzuContainerSizesPlugin.PersonalCol.Value;
                    break;
                // Wood chest
                case "piece_chest_wood":
                    inventoryRows = AzuContainerSizesPlugin.WoodRow.Value;
                    inventoryColumns = AzuContainerSizesPlugin.WoodCol.Value;
                    break;
                // Iron chest
                case "piece_chest":
                    inventoryRows = AzuContainerSizesPlugin.IronRow.Value;
                    inventoryColumns = AzuContainerSizesPlugin.IronCol.Value;
                    break;
                // Blackmetal chest
                case "piece_chest_blackmetal":
                    inventoryRows = AzuContainerSizesPlugin.BmRow.Value;
                    inventoryColumns = AzuContainerSizesPlugin.BmCol.Value;
                    break;
            }

            string chestListRaw = AzuContainerSizesPlugin.ChestList.Value;
            string chestRowColRaw = AzuContainerSizesPlugin.CustomRowCol.Value;

            if (!string.IsNullOrWhiteSpace(chestListRaw) && !string.IsNullOrWhiteSpace(chestRowColRaw))
            {
                string[] chestList = chestListRaw.Trim().Split([','], StringSplitOptions.RemoveEmptyEntries);
                string[] chestRowColList = chestRowColRaw.Trim().Split([','], StringSplitOptions.RemoveEmptyEntries);

                if (chestList.Length != chestRowColList.Length)
                {
                    AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Custom Chest List and Custom Chest Rows & Columns length mismatch. Currently you have {chestList.Length} chests and {chestRowColList.Length} row/column sets respectively.");
                }
                else
                {
                    for (int index = 0; index < chestList.Length; ++index)
                    {
                        string chestName = chestList[index].Trim();
                        if (!string.Equals(inventoryName, chestName, StringComparison.Ordinal))
                            continue;

                        string[] parts = chestRowColList[index].Trim().Split(':');
                        if (parts.Length != 2)
                        {
                            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Custom Chest Rows & Columns value for '{chestName}' is not in 'rows:cols' format.");
                            continue;
                        }

                        if (int.TryParse(parts[0], out int inventoryRow))
                            inventoryRows = inventoryRow;
                        else
                            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Custom Chest Container Rows & Columns value for {chestName} row is not a valid integer.");

                        if (int.TryParse(parts[1], out int inventoryColumn))
                            inventoryColumns = inventoryColumn;
                        else
                            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Custom Chest Container Rows & Columns value for {chestName} column is not a valid integer.");
                    }
                }
            }
        }

        if (AzuContainerSizesPlugin.ShipContainerControl.Value.IsOn())
        {
            switch (inventoryName)
            {
                case "Karve":
                    inventoryRows = AzuContainerSizesPlugin.KarveRow.Value;
                    inventoryColumns = AzuContainerSizesPlugin.KarveCol.Value;
                    break;
                // Longboat (Large boat)
                case "VikingShip":
                    inventoryRows = AzuContainerSizesPlugin.LongRow.Value;
                    inventoryColumns = AzuContainerSizesPlugin.LongCol.Value;
                    break;
                // Cart (Wagon)
                case "Cart":
                    inventoryRows = AzuContainerSizesPlugin.CartRow.Value;
                    inventoryColumns = AzuContainerSizesPlugin.CartCol.Value;
                    break;
            }

            string shipListRaw = AzuContainerSizesPlugin.ShipList.Value;
            string shipRowColRaw = AzuContainerSizesPlugin.ShipCustomRowCol.Value;

            if (!string.IsNullOrWhiteSpace(shipListRaw) && !string.IsNullOrWhiteSpace(shipRowColRaw))
            {
                string[] shipList = shipListRaw.Trim().Split([','], StringSplitOptions.RemoveEmptyEntries);
                string[] shipRcList = shipRowColRaw.Trim().Split([','], StringSplitOptions.RemoveEmptyEntries);

                if (shipList.Length != shipRcList.Length)
                {
                    AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Custom Ship List and Custom Ship Container Rows & Columns length mismatch. Currently you have {shipList.Length} ships and {shipRcList.Length} row/column sets respectively.");
                }
                else
                {
                    for (int i = 0; i < shipList.Length; ++i)
                    {
                        string shipName = shipList[i].Trim();
                        if (!string.Equals(inventoryName, shipName, StringComparison.Ordinal))
                            continue;

                        string[] parts = shipRcList[i].Trim().Split(':');
                        if (parts.Length != 2)
                        {
                            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Custom Ship Container Rows & Columns value for '{shipName}' is not in 'rows:cols' format.");
                            continue;
                        }

                        if (int.TryParse(parts[0], out int inventoryRow))
                            inventoryRows = inventoryRow;
                        else
                            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Custom Ship Container Rows & Columns value for {shipName} row is not a valid integer.");

                        if (int.TryParse(parts[1], out int inventoryColumn))
                            inventoryColumns = inventoryColumn;
                        else
                            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Custom Ship Container Rows & Columns value for {shipName} column is not a valid integer.");
                    }
                }
            }
        }
    }

    private static void ProtectItemsOnResize(Container container, Inventory inventory, int newRows, int newCols)
    {
        List<ItemDrop.ItemData> items = inventory.GetAllItems();
        if (items == null || items.Count == 0)
            return;

        bool[,] occupied = new bool[newCols, newRows];
        List<ItemDrop.ItemData> inBounds = new(items.Count);
        List<ItemDrop.ItemData> overflow = new(items.Count);

        // Classify items as "already valid" or "overflow"
        foreach (ItemDrop.ItemData item in items)
        {
            if (item is not { m_stack: > 0 })
                continue;

            Vector2i pos = item.m_gridPos;

            if (pos.x >= 0 && pos.x < newCols && pos.y >= 0 && pos.y < newRows)
            {
                if (!occupied[pos.x, pos.y])
                {
                    occupied[pos.x, pos.y] = true;
                    inBounds.Add(item);
                }
                else
                {
                    // Multiple items in one slot: keep the first, overflow the rest
                    overflow.Add(item);
                }
            }
            else
            {
                overflow.Add(item);
            }
        }

        if (overflow.Count == 0)
            return;

        TryMergeOverflowIntoExistingStacks(overflow, inBounds);

        foreach (ItemDrop.ItemData item in overflow)
        {
            if (item is not { m_stack: > 0 })
                continue;

            if (TryPlaceInFreeSlot(item, occupied, newCols, newRows, out Vector2i newPos))
            {
                item.m_gridPos = newPos;
                inBounds.Add(item);
            }
            else
            {
                // No space left at all in the resized container – drop it next to the chest
                DropItemFromContainer(container, item);
                item.m_stack = 0;
            }
        }

        items.RemoveAll(i => i is not { m_stack: > 0 });
    }

    private static void TryMergeOverflowIntoExistingStacks(List<ItemDrop.ItemData> overflow, List<ItemDrop.ItemData> inBounds)
    {
        if (overflow.Count == 0 || inBounds.Count == 0)
            return;

        foreach (ItemDrop.ItemData overflowItem in overflow)
        {
            if (overflowItem is not { m_stack: > 0 })
                continue;

            int remaining = overflowItem.m_stack;

            foreach (ItemDrop.ItemData target in inBounds)
            {
                if (target == null)
                    continue;

                if (!CanStack(target, overflowItem))
                    continue;

                int maxStack = target.m_shared.m_maxStackSize;
                if (maxStack <= 1)
                    continue;

                int space = maxStack - target.m_stack;
                if (space <= 0)
                    continue;

                int move = Mathf.Min(space, remaining);
                target.m_stack += move;
                remaining -= move;

                if (remaining <= 0)
                    break;
            }

            overflowItem.m_stack = remaining;
        }

        overflow.RemoveAll(i => i is not { m_stack: > 0 });
    }

    private static bool CanStack(ItemDrop.ItemData a, ItemDrop.ItemData b)
    {
        if (a.m_shared == null || b.m_shared == null)
            return false;

        if (!string.Equals(a.m_shared.m_name, b.m_shared.m_name, StringComparison.Ordinal))
            return false;

        if (a.m_shared.m_maxStackSize <= 1)
            return false;

        // Keep rules simple/safe – same quality & variant only
        if (a.m_quality != b.m_quality)
            return false;

        return a.m_variant == b.m_variant;
    }

    private static bool TryPlaceInFreeSlot(ItemDrop.ItemData item, bool[,] occupied, int cols, int rows, out Vector2i pos)
    {
        for (int y = 0; y < rows; ++y)
        {
            for (int x = 0; x < cols; ++x)
            {
                if (occupied[x, y])
                    continue;

                occupied[x, y] = true;
                pos = new Vector2i(x, y);
                return true;
            }
        }

        pos = default(Vector2i);
        return false;
    }

    /// <summary>
    /// Spawns the item as a world drop next to the container, preserving as much data as possible.
    /// Only called on the owner.
    /// </summary>
    private static void DropItemFromContainer(Container container, ItemDrop.ItemData item)
    {
        if (item.m_stack <= 0)
            return;

        if (!item.m_dropPrefab)
        {
            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogWarning($"Unable to drop overflow item '{item.m_shared?.m_name ?? "<null>"}' from container '{container.name}' because it has no drop prefab.");
            return;
        }

        try
        {
            Vector3 chestPos = container.transform.position;
            Vector3 forward = container.transform.forward;

            Vector3 dropPos = chestPos + forward * 0.6f + Vector3.up * 0.3f;
            Vector3 dropVel = forward * 1.5f + Vector3.up * 2f;

            GameObject worldObject = Object.Instantiate(item.m_dropPrefab.gameObject, dropPos, Quaternion.identity);

            if (worldObject.TryGetComponent(out Rigidbody rb))
            {
                rb.linearVelocity = dropVel;
            }

            if (!worldObject.TryGetComponent(out ItemDrop drop)) return;
            ItemDrop.ItemData data = drop.m_itemData;

            // Copy core state
            data.m_stack = item.m_stack;
            data.m_quality = item.m_quality;
            data.m_variant = item.m_variant;
            data.m_durability = item.m_durability;
            data.m_crafterID = item.m_crafterID;
            data.m_crafterName = item.m_crafterName;

            if (item.m_customData is not { Count: > 0 }) return;
            data.m_customData.Clear();
            foreach (KeyValuePair<string, string> kv in item.m_customData)
                data.m_customData[kv.Key] = kv.Value;
        }
        catch (Exception e)
        {
            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Failed to drop overflow item '{item.m_shared?.m_name ?? "<null>"}' from container '{container.name}': {e}");
        }
    }
}