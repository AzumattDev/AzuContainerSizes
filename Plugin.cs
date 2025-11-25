using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace AzuContainerSizes
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class AzuContainerSizesPlugin : BaseUnityPlugin
    {
        internal const string ModName = "AzuContainerSizes";
        internal const string ModVersion = "1.1.4";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        internal static AzuContainerSizesPlugin Instance { get; private set; } = null!;
        public static readonly ManualLogSource AzuContainerSizesLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            Instance = this;
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            Functions.LoadConfig();

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                AzuContainerSizesLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                AzuContainerSizesLogger.LogError($"There was an issue loading your {ConfigFileName}");
                AzuContainerSizesLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<Toggle> ChestContainerControl = null!;
        public static ConfigEntry<Toggle> ShipContainerControl = null!;
        public static ConfigEntry<string> ChestList = null!;
        public static ConfigEntry<string> CustomRowCol = null!;
        public static ConfigEntry<string> ShipList = null!;
        public static ConfigEntry<int> KarveRow = null!;
        public static ConfigEntry<int> KarveCol = null!;
        public static ConfigEntry<int> LongRow = null!;
        public static ConfigEntry<int> LongCol = null!;
        public static ConfigEntry<int> CartRow = null!;
        public static ConfigEntry<int> CartCol = null!;
        public static ConfigEntry<int> PersonalRow = null!;
        public static ConfigEntry<int> PersonalCol = null!;
        public static ConfigEntry<int> WoodRow = null!;
        public static ConfigEntry<int> WoodCol = null!;
        public static ConfigEntry<int> IronRow = null!;
        public static ConfigEntry<int> IronCol = null!;
        public static ConfigEntry<int> BmRow = null!;
        public static ConfigEntry<int> BmCol = null!;
        public static ConfigEntry<string> ShipCustomRowCol = null!;

        internal ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        internal ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        internal ConfigEntry<T> TextEntryConfig<T>(string group, string name, T value, string desc, bool synchronizedSetting = true)
        {
            ConfigurationManagerAttributes attributes = new()
            {
                CustomDrawer = Functions.TextAreaDrawer
            };
            return config(group, name, value, new ConfigDescription(desc, null, attributes), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() => "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
        }

        #endregion
    }

    public static class ToggleExtentions
    {
        public static bool IsOn(this AzuContainerSizesPlugin.Toggle value)
        {
            return value == AzuContainerSizesPlugin.Toggle.On;
        }

        public static bool IsOff(this AzuContainerSizesPlugin.Toggle value)
        {
            return value == AzuContainerSizesPlugin.Toggle.Off;
        }
    }
}