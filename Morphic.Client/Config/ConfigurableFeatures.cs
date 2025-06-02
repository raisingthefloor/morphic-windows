namespace Morphic.Client.Config
{
    using System;
    using System.Collections.Generic;

    public static class ConfigurableFeatures
    {
        public enum AutorunConfigOption
        {
            Disabled,
            AllLocalUsers,
            CurrentUser
        }

        // NOTE: this setting has no effect if Autorun is disabled
        public enum MorphicBarVisibilityAfterLoginOption
        {
            Restore, // show/hide the MorphicBar based on the visibility when the user last signed out
            Show, // always show the MorphicBar after login
            Hide // always hide the MorphicBar after login
        }

        public enum MorphicBarDefaultLocationOption
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            //
            TopLeading,
            TopTrailing,
            BottomLeading,
            BottomTrailing,
        }

        public static bool AtOnDemandIsEnabled = true;
        //
        public static bool AtUseCounterIsEnabled = false;
        //
        public static DateTime? HideMorphicAfterLoginUntil = null;
        //
        public static AutorunConfigOption? AutorunConfig = null;
        //
        public static bool CheckForUpdatesIsEnabled = false;
        //
        // OBSERVATION: should this instead be set to true by default?
        public static bool CloudSettingsTransferIsEnabled = false;
        //
        public static bool CustomMorphicBarsIsEnabled = true;
        //
        public static bool ResetSettingsIsEnabled = false;
        //
        public static bool SignInIsEnabled = true;
        //
        public static bool TelemetryIsEnabled = true;
        //
        // NOTE: this setting has no effect if Autorun is disabled
        public static MorphicBarDefaultLocationOption MorphicBarDefaultLocation = MorphicBarDefaultLocationOption.BottomTrailing;
        public static MorphicBarVisibilityAfterLoginOption? MorphicBarVisibilityAfterLogin = MorphicBarVisibilityAfterLoginOption.Restore;
        public static List<Morphic.Client.App.MorphicBarExtraItem> MorphicBarExtraItems = new List<Morphic.Client.App.MorphicBarExtraItem>();
        //
        public static string? TelemetrySiteId = null;

        public static void SetFeatures(
            bool atOnDemandIsEnabled,
            bool atUseCounterIsEnabled,
            DateTime? hideMorphicAfterLoginUntil,
            AutorunConfigOption? autorunConfig,
            bool checkForUpdatesIsEnabled,
            bool cloudSettingsTransferIsEnabled,
            bool customMorphicBarsIsEnabled,
            bool resetSettingsIsEnabled,
            bool signInIsEnabled,
            bool telemetryIsEnabled,
            MorphicBarDefaultLocationOption morphicBarDefaultLocation,
            MorphicBarVisibilityAfterLoginOption? morphicBarvisibilityAfterLogin,
            List<Morphic.Client.App.MorphicBarExtraItem> morphicBarExtraItems,
            string? telemetrySiteId
            )
        {
            // NOTE: if ConfigurableFeatures.SignInIsEnabled is false, then cascade this 'false' setting to force-disable related login-related features
            if (signInIsEnabled == false)
            {
                cloudSettingsTransferIsEnabled = false;
                customMorphicBarsIsEnabled = false;
            }

            ConfigurableFeatures.AtOnDemandIsEnabled = atOnDemandIsEnabled;
            ConfigurableFeatures.AtUseCounterIsEnabled = atUseCounterIsEnabled;
            ConfigurableFeatures.HideMorphicAfterLoginUntil = hideMorphicAfterLoginUntil;
            ConfigurableFeatures.AutorunConfig = autorunConfig;
            ConfigurableFeatures.CheckForUpdatesIsEnabled = checkForUpdatesIsEnabled;
            ConfigurableFeatures.CloudSettingsTransferIsEnabled = cloudSettingsTransferIsEnabled;
            ConfigurableFeatures.CustomMorphicBarsIsEnabled = customMorphicBarsIsEnabled;
            ConfigurableFeatures.ResetSettingsIsEnabled = resetSettingsIsEnabled;
            ConfigurableFeatures.SignInIsEnabled = signInIsEnabled;
            ConfigurableFeatures.TelemetryIsEnabled = telemetryIsEnabled;
            ConfigurableFeatures.MorphicBarDefaultLocation = morphicBarDefaultLocation;
            ConfigurableFeatures.MorphicBarVisibilityAfterLogin = morphicBarvisibilityAfterLogin;
            ConfigurableFeatures.MorphicBarExtraItems = morphicBarExtraItems;
            ConfigurableFeatures.TelemetrySiteId = telemetrySiteId;
        }
    }
}
