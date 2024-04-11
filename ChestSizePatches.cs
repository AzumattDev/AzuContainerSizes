using HarmonyLib;
using UnityEngine;

namespace AzuContainerSizes;

[HarmonyPatch(typeof(Container), nameof(Container.Awake))]
public static class ContainerAwakePatch
{
    private static void Postfix(Container __instance, ref Inventory ___m_inventory)
    {
        if (__instance.name.StartsWith("Treasure") || ___m_inventory == null ||
            !__instance.m_nview.IsValid() ||
            __instance.m_nview.GetZDO().GetLong("creator".GetStableHashCode()) == 0L)
            return;

        Inventory? inventory = __instance.GetInventory();
        ref Inventory? containerinventory = ref inventory;
        var root = __instance.transform.root;
        string inventoryName = root.name.Trim().Replace("(Clone)", "");


        ref int inventoryColumns = ref containerinventory.m_width;
        ref int inventoryRows = ref containerinventory.m_height;
        ContainerFunctions.UpdateContainerSize(ref inventoryName, ref inventoryRows, ref inventoryColumns);

    }
}

// Check on Container Interact as well since the awake above isn't guaranteed to run on all containers (usually right after they are built)
[HarmonyPatch(typeof(Container), nameof(Container.Interact))]
public static class ContainerInteractPatch
{
    private static void Prefix(Container __instance, Humanoid character, bool hold, bool alt)
    {
        if (__instance.name.StartsWith("Treasure") || __instance.GetInventory() == null ||
            !__instance.m_nview.IsValid() ||
            __instance.m_nview.GetZDO().GetLong("creator".GetStableHashCode()) == 0L)
            return;

        Inventory? inventory = __instance.GetInventory();
        ref Inventory? containerinventory = ref inventory;
        var root = __instance.transform.root;
        string inventoryName = root.name.Trim().Replace("(Clone)", "");


        ref int inventoryColumns = ref containerinventory.m_width;
        ref int inventoryRows = ref containerinventory.m_height;
        ContainerFunctions.UpdateContainerSize(ref inventoryName, ref inventoryRows, ref inventoryColumns);
    }
}

public static class ContainerFunctions
{
    public static void UpdateContainerSize(ref string inventoryName, ref int inventoryRows, ref int inventoryColumns)
    {
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

            string[] chestList = AzuContainerSizesPlugin.ChestList.Value.Trim().Split(',');
            string[] chestRowColList = AzuContainerSizesPlugin.CustomRowCol.Value.Trim().Split(',');
            for (int index = 0; index < chestList.Length; index++)
            {
                string chestName = chestList[index];
                if (inventoryName != chestName) continue;

                if (int.TryParse(chestRowColList[index].Trim().Split(':')[0], out int inventoryRow))
                {
                    inventoryRows = inventoryRow;
                }
                else
                {
                    AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError(
                        $"Custom Chest Container Rows & Columns value for {chestName} row is not a valid integer.");
                }

                if (int.TryParse(chestRowColList[index].Trim().Split(':')[1], out int inventoryColumn))
                {
                    inventoryColumns = inventoryColumn;
                }
                else
                {
                    AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError(
                        $"Custom Chest Container Rows & Columns value for {chestName} column is not a valid integer.");
                }
            }
        }

        if (AzuContainerSizesPlugin.ShipContainerControl.Value == AzuContainerSizesPlugin.Toggle.On)
        {
            switch (inventoryName)
            {
                case "Karve":
                    inventoryRows = AzuContainerSizesPlugin.KarveRow.Value;
                    inventoryColumns = AzuContainerSizesPlugin.KarveCol.Value;
                    break;
                // Longboat (Large boat)
                case "VikingShip":
                    // Log the height and width of the container
                    inventoryRows = AzuContainerSizesPlugin.LongRow.Value;
                    inventoryColumns = AzuContainerSizesPlugin.LongCol.Value;
                    break;
                // Cart (Wagon)
                case "Cart":
                    inventoryRows = AzuContainerSizesPlugin.CartRow.Value;
                    inventoryColumns = AzuContainerSizesPlugin.CartCol.Value;
                    break;
            }

            string[] shipList = AzuContainerSizesPlugin.ShipList.Value.Trim().Split(',');
            string[] shipRcList = AzuContainerSizesPlugin.ShipCustomRowCol.Value.Trim().Split(',');
            if (shipList.Length != shipRcList.Length)
            {
                AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError(
                    $"Custom Ship List and Custom Ship Container Rows & Columns length mismatch. Currently you have {shipList.Length} ships and {shipRcList.Length} row/column sets respectively.");
                return;
            }

            for (int i = 0; i < shipList.Length; ++i)
            {
                string shipName = shipList[i];
                if (inventoryName != shipName) continue;
                if (int.TryParse(shipRcList[i].Trim().Split(':')[0], out int inventoryRow))
                {
                    inventoryRows = inventoryRow;
                }
                else
                {
                    AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError(
                        $"Custom Ship Container Rows & Columns value for {shipName} row is not a valid integer.");
                }

                if (int.TryParse(shipRcList[i].Trim().Split(':')[1], out int inventoryColumn))
                {
                    inventoryColumns = inventoryColumn;
                }
                else
                {
                    AzuContainerSizesPlugin.AzuContainerSizesLogger.LogError(
                        $"Custom Ship Container Rows & Columns value for {shipName} column is not a valid integer.");
                }
            }
        }
    }
    public static void UpdateContainerSize()
    {
        foreach (Container container in Resources.FindObjectsOfTypeAll<Container>())
        {
            if (container.name.StartsWith("Treasure") || container.GetInventory() == null ||
                !container.m_nview.IsValid() ||
                container.m_nview.GetZDO().GetLong("creator".GetStableHashCode()) == 0L)
                return;
            Inventory? inventory = container.GetInventory();
            ref Inventory? containerinventory = ref inventory;
            var root = container.transform.root;
            string inventoryName = root.name.Trim().Replace("(Clone)", "");
            ref var containerInvWidth = ref containerinventory.m_width;
            ref var containerInvHeight = ref containerinventory.m_height;
            UpdateContainerSize(ref inventoryName, ref containerInvHeight, ref containerInvWidth);
        }
    }
}