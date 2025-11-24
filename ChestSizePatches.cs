using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AzuContainerSizes;

[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
public static class ZNetSceneAwakePatch
{
    private static void Postfix(ZNetScene __instance)
    {
        foreach (GameObject prefab in __instance.m_prefabs)
        {
            if (!prefab)
                continue;

            Container? container = prefab.GetComponentInChildren<Container>(true);
            if (!container)
                continue;

            int rows = container.m_height;
            int cols = container.m_width;

            ContainerFunctions.GetConfiguredSizeForPrefab(prefab.name, ref rows, ref cols);

            container.m_height = rows;
            container.m_width = cols;
        }
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.Load))]
public static class ContainerLoadPatch
{
    private static void Prefix(Container __instance)
    {
        if (!__instance || !__instance.m_nview || !__instance.m_nview.IsValid())
            return;

        if (!__instance.m_nview.IsOwner())
            return;

        ZDO zdo = __instance.m_nview.GetZDO();
        if (zdo == null)
            return;

        string itemsString = zdo.GetString(ZDOVars.s_items);
        if (string.IsNullOrEmpty(itemsString))
            return;
        ZPackage pkg = new(itemsString);
        // Use a large enough grid to load any items that might have been in a bigger container in the past.
        Inventory tempInv = new(__instance.m_name, null, 30, 30);
        tempInv.Load(pkg);

        List<ItemDrop.ItemData> tempItems = tempInv.GetAllItems();
        if (tempItems == null || tempItems.Count == 0)
            return;

        int maxX = -1;
        int maxY = -1;

        foreach (ItemDrop.ItemData item in tempItems)
        {
            if (item is not { m_stack: > 0 })
                continue;

            Vector2i pos = item.m_gridPos;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y > maxY) maxY = pos.y;
        }

        if (maxX < 0 && maxY < 0)
            return; // nothing valid

        int neededWidth = maxX + 1;
        int neededHeight = maxY + 1;

        Inventory inv = __instance.m_inventory;
        if (inv == null)
        {
            inv = new Inventory(__instance.m_name, null, neededWidth, neededHeight);
            __instance.m_inventory = inv;
        }
        else
        {
            if (inv.m_width < neededWidth)
                inv.m_width = neededWidth;
            if (inv.m_height < neededHeight)
                inv.m_height = neededHeight;
        }
    }
}

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

        int targetRows = currentRows;
        int targetCols = currentCols;

        GetConfiguredSizeForName(inventoryName, ref targetRows, ref targetCols);

        targetRows = Mathf.Max(1, targetRows);
        targetCols = Mathf.Max(1, targetCols);

        if (targetRows == currentRows && targetCols == currentCols)
            return;

        bool shrinking = targetRows < currentRows || targetCols < currentCols;

        if (!shrinking)
        {
            inventory.m_height = targetRows;
            inventory.m_width = targetCols;

            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogDebug($"Container '{inventoryName}' resized (grow) from {currentCols}x{currentRows} to {targetCols}x{targetRows}.");
            return;
        }

        HandleShrinkWithOverflowDrop(container, inventory, inventoryName, currentCols, currentRows, targetCols, targetRows);

        inventory.m_height = targetRows;
        inventory.m_width = targetCols;

        AzuContainerSizesPlugin.AzuContainerSizesLogger.LogDebug($"Container '{inventoryName}' resized (shrink) from {currentCols}x{currentRows} to {targetCols}x{targetRows}.");
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

    /// <summary>
    /// Shrinking:
    /// - Finds items that will be outside the new bounds.
    /// - Tries to move them into free slots inside the new grid.
    /// - Drops any that still don't fit using ItemDrop.DropItem.
    /// </summary>
    private static void HandleShrinkWithOverflowDrop(Container container, Inventory inv, string inventoryName, int oldCols, int oldRows, int newCols, int newRows)
    {
        List<ItemDrop.ItemData> items = inv.GetAllItems();
        if (items == null || items.Count == 0)
            return;

        // Track occupancy for the *new* grid
        bool[,] occupied = new bool[newCols, newRows];
        List<ItemDrop.ItemData> overflow = new(items.Count);

        // First pass: mark items already within new bounds; overflow the rest
        foreach (ItemDrop.ItemData item in items)
        {
            if (item is not { m_stack: > 0 })
                continue;

            Vector2i pos = item.m_gridPos;

            // Negative positions are considered overflow
            if (pos.x < 0 || pos.y < 0 || pos.x >= newCols || pos.y >= newRows)
            {
                overflow.Add(item);
                continue;
            }

            if (!occupied[pos.x, pos.y])
            {
                occupied[pos.x, pos.y] = true;
            }
            else
            {
                // Two items in same slot? Keep the first, overflow the rest.
                overflow.Add(item);
            }
        }

        // Second pass: try to move overflow items into free slots inside the new grid
        List<ItemDrop.ItemData> stillOverflow = [];

        foreach (ItemDrop.ItemData item in overflow)
        {
            if (item is not { m_stack: > 0 })
                continue;

            if (TryFindFirstFreeSlot(occupied, newCols, newRows, out Vector2i freePos))
            {
                item.m_gridPos = freePos;
                occupied[freePos.x, freePos.y] = true;
            }
            else
            {
                // No room left anywhere in the new grid
                stillOverflow.Add(item);
            }
        }

        if (stillOverflow.Count == 0)
            return;

        // Third pass: drop the truly overflowing items next to the container
        Vector3 basePos = container.transform.position;
        Vector3 forward = container.transform.forward;

        Vector3 dropBasePos = basePos + forward * 0.6f + Vector3.up * 0.3f;

        int index = 0;

        foreach (ItemDrop.ItemData item in stillOverflow)
        {
            if (item is not { m_stack: > 0 })
                continue;

            int amount = item.m_stack;

            // Slight spread so they don't all land in the same spot
            float angle = index * 20f;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            Vector3 dropPos = dropBasePos + dir * 0.2f;

            // Remove from inventory first, like Player.DropItem does
            bool removed = inv.RemoveItem(item, amount);
            if (!removed)
            {
                AzuContainerSizesPlugin.AzuContainerSizesLogger.LogWarning($"Failed to remove overflow item '{item.m_shared?.m_name ?? "<null>"}' from container '{inventoryName}' while shrinking.");
                continue;
            }

            try
            {
                ItemDrop.DropItem(item, amount, dropPos, Quaternion.identity);
            }
            catch (Exception e)
            {
                AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Exception while dropping overflow item '{item.m_shared?.m_name ?? "<null>"}' from container '{inventoryName}': {e}");
            }

            index++;
        }
    }

    private static bool TryFindFirstFreeSlot(bool[,] occupied, int cols, int rows, out Vector2i pos)
    {
        for (int y = 0; y < rows; ++y)
        {
            for (int x = 0; x < cols; ++x)
            {
                if (occupied[x, y]) continue;
                pos = new Vector2i(x, y);
                return true;
            }
        }

        pos = default;
        return false;
    }

    private static void GetConfiguredSizeForName(string inventoryName, ref int rows, ref int cols)
    {
        int inventoryRows = rows;
        int inventoryColumns = cols;

        if (AzuContainerSizesPlugin.ChestContainerControl.Value == AzuContainerSizesPlugin.Toggle.On)
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

                        string pair = chestRowColList[index].Trim();
                        string[] parts = pair.Split(':');
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

                        string pair = shipRcList[i].Trim();
                        string[] parts = pair.Split(':');
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

        rows = Mathf.Max(1, inventoryRows);
        cols = Mathf.Max(1, inventoryColumns);
    }

    public static void GetConfiguredSizeForPrefab(string prefabName, ref int rows, ref int cols)
    {
        GetConfiguredSizeForName(prefabName, ref rows, ref cols);
    }
}