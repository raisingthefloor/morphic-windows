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

        public static AutorunConfigOption? AutorunConfig = null;
        //
        public static bool CheckForUpdatesIsEnabled = false;
        //
        public static bool CloudSettingsTransferIsEnabled = false;
        //
        public static bool ResetSettingsIsEnabled = false;
        //
        // NOTE: this setting has no effect if Autorun is disabled
        public static MorphicBarVisibilityAfterLoginOption? MorphicBarVisibilityAfterLogin = MorphicBarVisibilityAfterLoginOption.Restore;
        public static List<Morphic.Client.App.MorphicBarExtraItem> MorphicBarExtraItems = new List<Morphic.Client.App.MorphicBarExtraItem>();

        public static void SetFeatures(
            AutorunConfigOption? autorunConfig,
            bool checkForUpdatesIsEnabled,
            bool cloudSettingsTransferIsEnabled,
            bool resetSettingsIsEnabled,
            MorphicBarVisibilityAfterLoginOption? morphicBarvisibilityAfterLogin,
            List<Morphic.Client.App.MorphicBarExtraItem> morphicBarExtraItems
            )
        {
            ConfigurableFeatures.AutorunConfig = autorunConfig;
            ConfigurableFeatures.CheckForUpdatesIsEnabled = checkForUpdatesIsEnabled;
            ConfigurableFeatures.CloudSettingsTransferIsEnabled = cloudSettingsTransferIsEnabled;
            ConfigurableFeatures.ResetSettingsIsEnabled = resetSettingsIsEnabled;
            ConfigurableFeatures.MorphicBarVisibilityAfterLogin = morphicBarvisibilityAfterLogin;
            ConfigurableFeatures.MorphicBarExtraItems = morphicBarExtraItems;
        }
    }
}
