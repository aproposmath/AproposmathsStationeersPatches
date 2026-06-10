namespace AproposmathsStationeersPatches
{
    using BepInEx.Configuration;
    using System;
    using BepInEx;
    using HarmonyLib;
    using Assets.Scripts;

    [BepInPlugin(ThisModInfo.ModID, ThisModInfo.AssemblyName, ThisModInfo.Version)]
    public class CommunityPatchesPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = ThisModInfo.ModID;
        public const string PluginName = ThisModInfo.AssemblyName;
        public const string PluginVersion = ThisModInfo.Version;

        public static ConfigEntry<bool> RotateLogicDisplayText;
        public static ConfigEntry<bool> FixIC10StackSize;
        public static ConfigEntry<bool> PlayerStats;
        public static ConfigEntry<bool> FixLoadSetIndirectOrDefine;
        public static ConfigEntry<bool> FixPressureRegulator;

        private void BindAllConfigs()
        {
            RotateLogicDisplayText = Config.Bind("General", "Rotate Logic Display Text", true, "Rotate the text on logic displays if mounted upside down");
            FixIC10StackSize = Config.Bind("General", "Fix IC10 StackSize LogicType", true, "Fix reading the number of attached devices on a cable network in IC10");
            PlayerStats = Config.Bind("General", "PlayerStats", true, "Show more detailed player stats in tooltips");
            FixLoadSetIndirectOrDefine = Config.Bind("General", "fix_load_set_indirect_define", true, "Fix Load/Set instructions in IC10 with defined or indirect reference ids");
            FixPressureRegulator = Config.Bind("General", "FixPressureRegulators", false, "Use gas volume instead of total volume when gas is moved by pressure regulators, fixing regulation overshoot in certain cases");
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
                // _harmony.PatchAll();

                // This was fixed with version 0.2.6057.26562 of Stationeers, so never apply the patch for newer versions
                if (gameVersion < Version.Parse("0.2.6057.26562") && RotateLogicDisplayText.Value)
                {
                    _harmony.CreateClassProcessor(typeof(PatchLogicDisplayOrientation), true).Patch();
                    L.Info("RotateLogicDisplayText patch applied");
                }

                if (PlayerStats.Value)
                {
                    _harmony.CreateClassProcessor(typeof(PatchHumanStats), true).Patch();
                    L.Info("PlayerStats patch applied");
                }

                if (FixPressureRegulator.Value)
                {
                    _harmony.CreateClassProcessor(typeof(PatchPressureRegulator), true).Patch();
                    L.Info("FixPressureRegulator patch applied");
                }

                // This was fixed with version 0.2.6330.27281 of Stationeers, so never apply the patch for newer versions
                if (gameVersion < Version.Parse("0.2.6330.27281"))
                {
                    if (FixIC10StackSize.Value)
                    {
                        _harmony.CreateClassProcessor(typeof(PatchStackSize), true).Patch();
                        L.Info("FixIC10StackSize patch applied");
                    }

                    if (FixLoadSetIndirectOrDefine.Value)
                    {
                        _harmony.CreateClassProcessor(typeof(PatchSetLoadIndirectOrDefine), true).Patch();
                        L.Info("FixLoadSetIndirectOrDefine patch applied");
                    }
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
                L.Info("Debug build detected, unpatching all Harmony patches");
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
