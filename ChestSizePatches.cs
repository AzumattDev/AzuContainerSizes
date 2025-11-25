using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace AzuContainerSizes;

/// <summary>
/// Transpiles Container.Awake to call a sizing helper *before* the Inventory ctor
/// arguments (m_name, m_bkg, m_width, m_height) are loaded.
/// This lets me:
///  - Apply config size.
///  - Expand to fit any saved items in ZDO.
/// so the initial Inventory is always big enough, even if the chest was offline (not loaded)
/// when the config shrank.
/// </summary>
[HarmonyPatch(typeof(Container), nameof(Container.Awake))]
internal static class ContainerAwakeTranspilerPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> list = new(instructions);
        FieldInfo? nameField = AccessTools.Field(typeof(Container), "m_name");
        MethodInfo? adjustMethod = AccessTools.Method(typeof(ContainerFunctions), nameof(ContainerFunctions.AdjustSizeBeforeInventoryCtor));

        bool injected = false;

        for (int i = 0; i < list.Count; ++i)
        {
            CodeInstruction? ins = list[i];
            var next = i + 1;
            // Find the first "ldarg.0; ldfld Container::m_name" that starts the arg
            // sequence for new Inventory(...), and inject sizing call *before* it.
            if (!injected && ins.opcode == OpCodes.Ldarg_0 && next < list.Count && list[next].opcode == OpCodes.Ldfld && Equals(list[next].operand, nameField))
            {
                // Inject: AdjustSizeBeforeInventoryCtor(this);
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Call, adjustMethod);

                injected = true;
            }

            yield return ins;
        }

        if (!injected)
        {
            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogWarning("Failed to inject Container.Awake transpiler; AdjustSizeBeforeInventoryCtor was not inserted.");
        }
    }
}

internal static class ContainerFunctions
{
    /// <summary>
    /// Called from the Awake transpiler *before* Inventory is constructed.
    /// 1. Applies config size (chest/ship/carts/custom lists).
    /// 2. Reads saved items from ZDO.s_items and expands width/height if needed,
    ///    so I never create an Inventory that is smaller than the existing contents.
    ///
    /// Result: no items are ever clipped when the chest was offline at config change.
    /// </summary>
    internal static void AdjustSizeBeforeInventoryCtor(Container container)
    {
        if (!TryGetValidZdo(container, out ZDO zdo))
            return;

        int rows = container.m_height;
        int cols = container.m_width;

        string inventoryName = GetInventoryName(container);
        GetConfiguredSizeForName(inventoryName, ref rows, ref cols);

        rows = Mathf.Max(1, rows);
        cols = Mathf.Max(1, cols);

        // Ensure not to shrink below what the items already saved in it need in order to fit.
        EnsureFitsSavedItems(container, zdo, ref rows, ref cols, inventoryName);

        container.m_width = cols;
        container.m_height = rows;
    }

    /// <summary>
    /// Called from config SettingChanged handlers.
    /// Applies config to currently loaded containers but never shrinks below what
    /// the saved items require.
    ///
    /// This avoids item loss or out-of-range visuals on live changes as well.
    /// </summary>
    internal static void UpdateContainerSize()
    {
        foreach (Container container in Resources.FindObjectsOfTypeAll<Container>())
        {
            if (!TryGetValidZdo(container, out ZDO zdo))
                continue;

            Inventory inv = container.GetInventory();
            if (inv == null)
                continue;

            int rows = inv.m_height;
            int cols = inv.m_width;

            string inventoryName = GetInventoryName(container);
            GetConfiguredSizeForName(inventoryName, ref rows, ref cols);
            rows = Mathf.Max(1, rows);
            cols = Mathf.Max(1, cols);

            EnsureFitsSavedItems(container, zdo, ref rows, ref cols, inventoryName);

            inv.m_width = cols;
            inv.m_height = rows;
        }
    }

    private static void GetConfiguredSizeForName(string inventoryName, ref int rows, ref int cols)
    {
        int inventoryRows = rows;
        int inventoryColumns = cols;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetValidZdo(Container container, out ZDO zdo)
    {
        zdo = null;

        if (!container)
            return false;

        ZNetView nview = container.m_nview;
        if (!nview || !nview.IsValid())
            return false;

        zdo = nview.GetZDO();
        return zdo != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetInventoryName(Container container)
    {
        Transform root = container.transform.root;
        return root ? root.name.Trim().Replace("(Clone)", "") : container.gameObject.name;
    }

    /// <summary>
    /// Reads ZDO.s_items and ensures rows/cols are not smaller than the maximum
    /// grid positions of saved items.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureFitsSavedItems(Container container, ZDO zdo, ref int rows, ref int cols, string inventoryName)
    {
        string itemsStr = zdo.GetString(ZDOVars.s_items);
        if (string.IsNullOrEmpty(itemsStr))
            return;

        try
        {
            // Parse the saved package into a big temporary inventory to
            // see all grid positions without clipping.
            ZPackage pkg = new(itemsStr);
            Inventory tempInv = new(container.m_name, null, 30, 30);
            tempInv.Load(pkg);

            List<ItemDrop.ItemData> items = tempInv.GetAllItems();
            if (items is { Count: > 0 })
            {
                int maxX = -1;
                int maxY = -1;

                foreach (ItemDrop.ItemData item in items)
                {
                    if (item is not { m_stack: > 0 })
                        continue;

                    Vector2i p = item.m_gridPos;
                    if (p.x > maxX) maxX = p.x;
                    if (p.y > maxY) maxY = p.y;
                }

                if (maxX >= 0)
                    cols = Mathf.Max(cols, maxX + 1);
                if (maxY >= 0)
                    rows = Mathf.Max(rows, maxY + 1);
            }
        }
        catch (Exception e)
        {
            AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError($"Failed to probe saved items for '{inventoryName}': {e}");
        }
    }
}