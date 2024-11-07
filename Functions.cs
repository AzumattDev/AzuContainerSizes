using BepInEx.Configuration;
using UnityEngine;

namespace AzuContainerSizes;

public class Functions
{
    internal static void LoadConfig()
    {
        AzuContainerSizesPlugin.ChestContainerControl = AzuContainerSizesPlugin.Instance.config("1 - General",
            "Chest Container Control",
            AzuContainerSizesPlugin.Toggle.On,
            "Toggle this value to turn off this mod's control over chest container size");
        AzuContainerSizesPlugin.ShipContainerControl = AzuContainerSizesPlugin.Instance.config("1 - General",
            "Ship Container Control",
            AzuContainerSizesPlugin.Toggle.On,
            "Toggle this value to turn off this mod's control over ship chest container size");

        // Chests
        AzuContainerSizesPlugin.PersonalRow = AzuContainerSizesPlugin.Instance.config("2 - Chests",
            "Personal Chest Rows", 2,
            new ConfigDescription("Personal Chest Rows", new AcceptableValueRange<int>(2, 20)));
        AzuContainerSizesPlugin.PersonalCol = AzuContainerSizesPlugin.Instance.config("2 - Chests",
            "Personal Chest Columns", 3,
            new ConfigDescription("Personal Chest Columns", new AcceptableValueRange<int>(3, 8)));
        AzuContainerSizesPlugin.WoodRow = AzuContainerSizesPlugin.Instance.config("2 - Chests", "Wood Chest Rows", 2,
            new ConfigDescription("Wood Chest Rows", new AcceptableValueRange<int>(2, 10)));
        AzuContainerSizesPlugin.WoodCol = AzuContainerSizesPlugin.Instance.config("2 - Chests", "Wood Chest Columns", 5,
            new ConfigDescription("Wood Chest Columns", new AcceptableValueRange<int>(5, 8)));
        AzuContainerSizesPlugin.IronRow = AzuContainerSizesPlugin.Instance.config("2 - Chests", "Iron Chest Rows", 4,
            new ConfigDescription("Iron Chest Rows", new AcceptableValueRange<int>(4, 20)));
        AzuContainerSizesPlugin.IronCol = AzuContainerSizesPlugin.Instance.config("2 - Chests", "Iron Chest Columns", 6,
            new ConfigDescription("Iron Chest Columns", new AcceptableValueRange<int>(6, 8)));
        AzuContainerSizesPlugin.BmRow = AzuContainerSizesPlugin.Instance.config("2 - Chests", "Blackmetal Chest Rows",
            4,
            new ConfigDescription("Blackmetal Chest Rows", new AcceptableValueRange<int>(3, 20)));
        AzuContainerSizesPlugin.BmCol = AzuContainerSizesPlugin.Instance.config("2 - Chests",
            "Blackmetal Chest Columns", 8,
            new ConfigDescription("Blackmetal Chest Columns", new AcceptableValueRange<int>(6, 8)));

        AzuContainerSizesPlugin.ChestList = AzuContainerSizesPlugin.Instance.TextEntryConfig("2 - Chests", "Custom Chest List",
            "",
            "List of chests to change size. Use the name of the chest prefab. You can add as many as you want it follows the Custom Chest Rows/Columns settings. Separate each name with a comma. Example: piece_chest_private,piece_chest,piece_chest_wood,piece_chest_iron,piece_chest_blackmetal");
        AzuContainerSizesPlugin.CustomRowCol = AzuContainerSizesPlugin.Instance.TextEntryConfig("2 - Chests",
            "Custom Chests Rows & Columns",
            "",
            "Custom Chest Rows & Columns. Separate each row and column set with a comma. Must match the number of custom chests you have specified. The row and column will be used in order of the custom chests. Each row and column should be separated by a : \n Example: 4:8,6:8,5:8");

        // Ship Containers
        AzuContainerSizesPlugin.KarveRow = AzuContainerSizesPlugin.Instance.config("3 - Ships", "Karve Rows", 2,
            new ConfigDescription("Rows for Karve", new AcceptableValueRange<int>(2, 30)));
        AzuContainerSizesPlugin.KarveCol = AzuContainerSizesPlugin.Instance.config("3 - Ships", "Karve Columns", 2,
            new ConfigDescription("Columns for Karve", new AcceptableValueRange<int>(2, 8)));
        AzuContainerSizesPlugin.LongRow = AzuContainerSizesPlugin.Instance.config("3 - Ships", "Longboat Rows", 3,
            new ConfigDescription("Rows for longboat (VikingShip)", new AcceptableValueRange<int>(3, 30)));
        AzuContainerSizesPlugin.LongCol = AzuContainerSizesPlugin.Instance.config("3 - Ships", "Longboat Columns", 6,
            new ConfigDescription("Columns for longboat  (VikingShip)", new AcceptableValueRange<int>(6, 8)));

        AzuContainerSizesPlugin.ShipList = AzuContainerSizesPlugin.Instance.TextEntryConfig("3 - Ships", "Custom Ship List",
            "",
            "List of ships to change size of their containers. Use the name of the ship prefab. You can add as many as you want it follows the Custom Ship Rows/Columns settings. Separate each name with a comma. Example: CargoShip,Skuldelev,LittleBoat,MercantShip,BigCargoShip,FishingBoat,FishingCanoe,WarShip");
        AzuContainerSizesPlugin.ShipCustomRowCol = AzuContainerSizesPlugin.Instance.TextEntryConfig("3 - Ships",
            "Custom Ship Container Rows & Columns",
            "", "Custom Ship Container Rows & Columns. Separate each row and column set with a comma. Must match the number of ships you have specified. The row and column will be used in order of the ships. Each row and column should be separated by a : \n Example: 4:8,6:8,5:8");


        AzuContainerSizesPlugin.CartRow = AzuContainerSizesPlugin.Instance.config("4 - Carts", "Cart Rows", 3,
            new ConfigDescription("Rows for Cart", new AcceptableValueRange<int>(3, 30)));
        AzuContainerSizesPlugin.CartCol = AzuContainerSizesPlugin.Instance.config("4 - Carts", "Cart Columns", 6,
            new ConfigDescription("Columns for Cart", new AcceptableValueRange<int>(6, 8)));
        // Delegates
        AzuContainerSizesPlugin.PersonalRow.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.PersonalCol.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.WoodRow.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.WoodCol.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.IronRow.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.IronCol.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.BmRow.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.BmCol.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.ChestList.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.CustomRowCol.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.KarveRow.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.KarveCol.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.LongRow.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.LongCol.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.ShipList.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.ShipCustomRowCol.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.CartRow.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        AzuContainerSizesPlugin.CartCol.SettingChanged += (_, _) => ContainerFunctions.UpdateContainerSize();
        
    }

    internal static void TextAreaDrawer(ConfigEntryBase entry)
    {
        GUILayout.ExpandHeight(true);
        GUILayout.ExpandWidth(true);
        entry.BoxedValue = GUILayout.TextArea((string)entry.BoxedValue, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
    }
}