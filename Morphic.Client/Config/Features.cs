namespace Morphic.Client.Config
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

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
        // NOTE: this setting has no effect if Autorun is disabled
        public static MorphicBarVisibilityAfterLoginOption? MorphicBarVisibilityAfterLogin = MorphicBarVisibilityAfterLoginOption.Restore;
        public static List<Morphic.Client.App.MorphicBarExtraItem> MorphicBarExtraItems = new List<Morphic.Client.App.MorphicBarExtraItem>();

        public static void SetFeatures(
            AutorunConfigOption? autorunConfig,
            bool checkForUpdatesIsEnabled,
            bool cloudSettingsTransferIsEnabled,
            MorphicBarVisibilityAfterLoginOption? morphicBarvisibilityAfterLogin,
            List<Morphic.Client.App.MorphicBarExtraItem> morphicBarExtraItems
            )
        {
            ConfigurableFeatures.AutorunConfig = autorunConfig;
            ConfigurableFeatures.CheckForUpdatesIsEnabled = checkForUpdatesIsEnabled;
            ConfigurableFeatures.CloudSettingsTransferIsEnabled = cloudSettingsTransferIsEnabled;
            ConfigurableFeatures.MorphicBarVisibilityAfterLogin = morphicBarvisibilityAfterLogin;
            ConfigurableFeatures.MorphicBarExtraItems = morphicBarExtraItems;
        }
    }

    [Flags]
    public enum Features : uint
    {
        None = 0,
        Basic = 1 << 0,
        Community = 1 << 1,
        All = ~0u
    }

    /// <summary>
    /// Reads the features that are enabled for this build.
    /// </summary>
    public static class BuildFeatures
    {
        private static readonly string FeatureFile =
            AppOptions.Current.Launch.FeaturesFile ?? AppPaths.GetAppFile("features");

        public static Features EnabledFeatures { get; private set; } = Features.None;

        public static string EditionName { get; private set; } = "internal";

        static BuildFeatures()
        {
            BuildFeatures.LoadFeatures();
        }

        /// <summary>
        /// Loads the features file, which is a list of features which are enabled for this build.
        /// A line in the file that starts with "name=" specifies the name of the edition of the build (eg, "Basic").
        /// </summary>
        private static void LoadFeatures()
        {
            IEnumerable<string> lines = File.ReadAllLines(BuildFeatures.FeatureFile)
                .Select(l => l.Trim())
                .Where(l => !l.StartsWith('#'))
                .ToList();

            Features features = 0;

            foreach (string line in lines)
            {
                if (line.StartsWith("name=", StringComparison.InvariantCultureIgnoreCase))
                {
                    BuildFeatures.EditionName = line.Substring(line.IndexOf('=') + 1);
                }

                if (Enum.TryParse(line, true, out Features feature))
                {
                    features |= feature;
                }
            }

            BuildFeatures.EnabledFeatures = features;
        }

        /// <summary>Checks if all the given features are enabled.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEnabled(this Features input)
        {
            return (EnabledFeatures & input) == input;
        }

        /// <summary>Checks if any of the given features are enabled.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AnyEnabled(this Features input)
        {
            return (EnabledFeatures & input) != 0;
        }
    }
}
