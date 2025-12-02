namespace AproposmathsStationeersPatches
{
    using BepInEx.Configuration;
    using System;
    using BepInEx;
    using HarmonyLib;
    using Assets.Scripts;
    using System.Diagnostics;

    class L
    {
        private static BepInEx.Logging.ManualLogSource _logger;

        public static void SetLogger(BepInEx.Logging.ManualLogSource logger)
        {
            _logger = logger;
        }

        public static void Debug(string message)
        {
            _logger?.LogDebug(message);
        }

        public static void Log(string message)
        {
            _logger?.LogInfo(message);
        }

        public static void Info(string message)
        {
            _logger?.LogInfo(message);
        }

        public static void Error(string message)
        {
            _logger?.LogError(message);
        }

        public static void Warning(string message)
        {
            _logger?.LogWarning(message);
        }

    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class CommunityPatchesPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "aproposmaths-stationeers-patches";
        public const string PluginName = "Aproposmaths Stationeers Patches";
        public const string PluginVersion = VersionInfo.Version;

        public static ConfigEntry<bool> RegisterIC10Instructions;
        public static ConfigEntry<bool> RotateLogicDisplayText;
        public static ConfigEntry<bool> FixIC10StackSize;
        public static ConfigEntry<bool> PlayerStats;

        private void BindAllConfigs()
        {
            RotateLogicDisplayText = Config.Bind("General", "Rotate Logic Display Text", true, "Rotate the text on logic displays if mounted upside down");
            FixIC10StackSize = Config.Bind("General", "Fix IC10 StackSize LogicType", true, "Fix reading the number of attached devices on a cable network in IC10");
            RegisterIC10Instructions = Config.Bind("General", "Register IC10 Instructions (experimental)", false, "Register IC10 Instructions 'hash', 'hash2', 'hashn' and 'itoa'. These are very experimental and will likely change/be removed. Use only for testing");
            PlayerStats = Config.Bind("General", "PlayerStats", true, "Show more detailed player stats in tooltips");
        }

        private Harmony _harmony = null;

        private void Awake()
        {
            try
            {
                L.SetLogger(this.Logger);
                this.Logger.LogInfo(
                    $"Awake ${PluginName} {VersionInfo.VersionGit}, build time {VersionInfo.BuildTime}");

                var gameVersion = typeof(GameManager).Assembly.GetName().Version;

                BindAllConfigs();

                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll();

                if (IC10HashInstructions.Enabled && RegisterIC10Instructions.Value)
                {
                    IC10HashInstructions.Register();
                    L.Info("IC10 Hash Instructions registered");
                }

                // This was fixed with version 0.2.6057.26562 of Stationeers, so never apply the patch for newer versions
                if (gameVersion < System.Version.Parse("0.2.6057.26562") && RotateLogicDisplayText.Value)
                {
                    _harmony.CreateClassProcessor(typeof(PatchLogicDisplayOrientation), true).Patch();
                    L.Info("RotateLogicDisplayText patch applied");
                }

                if (FixIC10StackSize.Value)
                {
                    _harmony.CreateClassProcessor(typeof(PatchStackSize), true).Patch();
                    L.Info("FixIC10StackSize patch applied");
                }

                if (PlayerStats.Value)
                {
                    _harmony.CreateClassProcessor(typeof(PatchHumanStats), true).Patch();
                    L.Info("PlayerStats patch applied");
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError($"Error during ${PluginName} {VersionInfo.VersionGit} init: {ex}");
            }
        }

        private void OnDestroy()
        {
            Logger.LogInfo($"[{PluginName}] OnDestroy (Version: {VersionInfo.VersionGit})");

            if (_harmony is null)
                return;

            try
            {
#if DEBUG
                // assume that a debug build is loaded by BepInEx ScriptEngine and unpatch
                _harmony.UnpatchSelf();
#endif
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    $"[{PluginName}] Error while unpatching Harmony in {nameof(OnDestroy)}: {ex}"
                );
            }
            finally
            {
                _harmony = null;
            }
        }
    }
}
