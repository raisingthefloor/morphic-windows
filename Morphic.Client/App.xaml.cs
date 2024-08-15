// Copyright 2020-2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using AutoUpdaterDotNET;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Morphic.Core;
using Morphic.Core.Legacy;
using Morphic.Service;
using NHotkey.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Morphic.Client;

using Bar;
using Bar.Data;
using Config;
using Dialogs;
using MainMenu;
using Microsoft.Win32;
using Morphic.TelemetryClient;
using Morphic.WindowsNative.OsVersion;
using Settings.SettingsHandlers;
using Settings.SolutionsRegistry;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

public class AppMain
{
    private static Mutex _singleInstanceMutex;
    private static uint _singleInstanceMessageId;

    // NOTE: we created our own Main function so that we can use a mutex to enforce running only one instance of Morphic at a time
    [STAThread]
    public static void Main()
    {
        // create a message which we can send/receive to indicate that a secondary instance has been started; use the application ID as its backing unique string
        _singleInstanceMessageId = WinApi.RegisterWindowMessage(App.ApplicationId);

        // create a mutex which we will use to make sure only one copy of Morphic runs at a time
        bool mutexCreatedNew;
        _singleInstanceMutex = new Mutex(true, App.ApplicationId, out mutexCreatedNew);

        // if the mutex already existed (i.e. the application is already running), send a message to it now asking it to show its MorphicBar
        if (mutexCreatedNew == false)
        {
            // send the "single instance" message to the main instance; leave both parameters as zero
            MessageWatcherNativeWindow.PostMessage(_singleInstanceMessageId, IntPtr.Zero, IntPtr.Zero);

            // shut down our application (gracefully by returning from Main)
            return;
        }

        // Ensure the current directory is the same as the executable so that relative paths work.
        // OBSERVATION: if the assembly location cannot be retrieved (an unlikely scenario), we do not set the current directory; intermittent issues with relative paths could theoretically occur.
        string? executingAssemblyLocation = Assembly.GetExecutingAssembly().Location;
        //
        string? executingDirectoryName = null;
        if (executingAssemblyLocation is not null)
        {
            executingDirectoryName = Path.GetDirectoryName(executingAssemblyLocation!);
        }
        //
        if (executingDirectoryName is not null)
        {
            Directory.SetCurrentDirectory(executingDirectoryName!);
        }

        App.Main();
    }

    internal static void ReleaseSingleInstanceMutex()
    {
        _singleInstanceMutex.ReleaseMutex();
    }

    internal static uint SingleInstanceMessageId
    {
        get
        {
            return _singleInstanceMessageId;
        }
    }
}

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // NOTE: we initialize this in the Application_Startup, so we set it to null -- but we initialize it during application startup so it should always be available
    internal Morphic.Controls.HybridTrayIcon HybridTrayIcon = null!;
    //
    // NOTE: we initialize this in the Application_Startup, so we set it to null -- but we initialize it during application startup so it should always be available
    private Morphic.Client.Theme.ThemeManager ThemeManager = null!;
    //
    // NOTE: as we cannot initialize this object in the App() constructor, we set it to null -- but we initialize it during application startup so it should always be available
    private Morphic.Client.MainMenu.MorphicMainMenu MorphicMainMenu = null!;

    /// <summary>Current application instance.</summary>
    public new static App Current { get; private set; } = null!;

    public IServiceProvider ServiceProvider { get; private set; } = null!;
    public IConfiguration Configuration { get; private set; } = null!;
    public ILogger<App> Logger { get; private set; } = null!;

    public MorphicSession MorphicSession { get; private set; } = null!;

    private MorphicTelemetryClient? _telemetryClient = null;

    public AppOptions AppOptions => AppOptions.Current;

    public DialogManager Dialogs { get; } = new DialogManager();
    public BarManager BarManager { get; } = new BarManager();

    public const string ApplicationId = "A6E8092B-51F4-4CAA-A874-A791152B5698";

    #region Configuration & Startup

    private Timer? _telemetryHeartbeatTimer = null;

    public App()
    {
        App.Current = this;
    }

    public class MorphicBarExtraItem
    {
        public string? type { get; set; }
        public string? label { get; set; }
        public string? tooltipHeader { get; set; }
        public string? tooltipText { get; set; }
        // for type: link
        public string? url { get; set; }
        // for type: action
        public string? function { get; set; }
        // for type: control
        public string? feature { get; set; }
        // for type: application
        public string? appId { get; set; }
    }
    //
    public class TelemetryConfigSection
    {
        public string? siteId { get; set; }
    }
    //
    public class ConfigFileContents
    {
        public class FeaturesConfigSection
        {
            public class EnabledFeature
            {
                public bool? enabled { get; set; }
                public string? scope { get; set; }
            }
            //
            public EnabledFeature? atOnDemand { get; set; }
            public EnabledFeature? atUseCounter { get; set; }
            public EnabledFeature? autorunAfterLogin { get; set; }
            public EnabledFeature? checkForUpdates { get; set; }
            public EnabledFeature? cloudSettingsTransfer { get; set; }
            public EnabledFeature? resetSettings { get; set; }
        }
        public class MorphicBarConfigSection
        {
            public string? visibilityAfterLogin { get; set; }
            public List<MorphicBarExtraItem>? extraItems { get; set; }
        }
        //
        public int? version { get; set; }
        public FeaturesConfigSection? features { get; set; }
        public MorphicBarConfigSection? morphicBar { get; set; }
        public TelemetryConfigSection? telemetry { get; set; }
        public string? hideMorphicAfterLoginUntil { get; set; }
    }
    //
    private struct CommonConfigurationContents
    {
        public bool AtOnDemandIsEnabled;
        public bool AtUseCounterIsEnabled;
        public DateTime? HideMorphicAfterLoginUntil;
        public ConfigurableFeatures.AutorunConfigOption? AutorunConfig;
        public bool CheckForUpdatesIsEnabled;
        public bool CloudSettingsTransferIsEnabled;
        public bool ResetSettingsIsEnabled;
        public ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption? MorphicBarVisibilityAfterLogin;
        public List<MorphicBarExtraItem> ExtraMorphicBarItems;
        public string? TelemetrySiteId;
    }
    private async Task<CommonConfigurationContents> GetCommonConfigurationAsync()
    {
        // set up default configuration
        var result = new CommonConfigurationContents();
        //
        // at on demand
        result.AtOnDemandIsEnabled = true;
        //
        // at use counter
        result.AtUseCounterIsEnabled = false;
        //
        // hide MorphicBar until (date)
        result.HideMorphicAfterLoginUntil = null;
        //
        // autorun
        result.AutorunConfig = null;
        //
        // check for updates
        result.CheckForUpdatesIsEnabled = true;
        //
        // copy settings to/from cloud
        result.CloudSettingsTransferIsEnabled = true;
        //
        // reset settings (to standard)
        result.ResetSettingsIsEnabled = false;
        //
        // morphic bar (visibility and extra items)
        result.MorphicBarVisibilityAfterLogin = null;
        result.ExtraMorphicBarItems = new List<MorphicBarExtraItem>();

        // NOTE: we have intentionally chosen not to create the CommonConfigDir (e.g. "C:\ProgramData\Morphic") since Morphic does not currently create files in this folder.
        var morphicCommonConfigPath = AppPaths.GetCommonConfigDir("", false);
        if (Directory.Exists(morphicCommonConfigPath) == false)
        {
            // no config file; return defaults
            return result;
        }

        var morphicConfigFilePath = Path.Combine(morphicCommonConfigPath, "config.json");
        if (File.Exists(morphicConfigFilePath) == false)
        {
            // no config file; return defaults
            return result;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(morphicConfigFilePath);
        }
        catch (Exception ex)
        {
            // error reading config file; return defaults
            // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
            Logger?.LogError("Could not read configuration file: " + morphicConfigFilePath + "; error: " + ex.Message);
            return result;
        }

        ConfigFileContents deserializedJson;
        try
        {
            ConfigFileContents? nullableDeserializedJson;
            nullableDeserializedJson = JsonSerializer.Deserialize<ConfigFileContents>(json);
            if (nullableDeserializedJson is null)
            {
                Logger?.LogError("Could not deserialize json configuration file: " + morphicConfigFilePath + "; deserialized json is null");
                return result;
            }
            deserializedJson = nullableDeserializedJson!;
        }
        catch (Exception ex)
        {
            // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
            Logger?.LogError("Could not deserialize json configuration file: " + morphicConfigFilePath + "; error: " + ex.Message);
            return result;
        }

        if ((deserializedJson.version is null) || (deserializedJson.version.Value < 0) || (deserializedJson.version.Value > 0))
        {
            // sorry, we don't understand this version of the file
            // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
            Logger?.LogError("Unknown config file version: " + deserializedJson.version.ToString());
            return result;
        }

        // capture the autorun setting
        if (deserializedJson.features?.autorunAfterLogin?.enabled is not null)
        {
            if (deserializedJson.features!.autorunAfterLogin!.enabled == false)
            {
                result.AutorunConfig = ConfigurableFeatures.AutorunConfigOption.Disabled;
            }
            else
            {
                switch (deserializedJson.features!.autorunAfterLogin!.scope)
                {
                    case "allLocalUsers":
                        result.AutorunConfig = ConfigurableFeatures.AutorunConfigOption.AllLocalUsers;
                        break;
                    case "currentUser":
                        result.AutorunConfig = ConfigurableFeatures.AutorunConfigOption.CurrentUser;
                        break;
                    case null:
                        // no scope present; use the default scope
                        break;
                    default:
                        // sorry, we don't understand this scope setting
                        // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                        Logger?.LogError("Unknown autorunAfterLogin scope: " + deserializedJson.features!.autorunAfterLogin!.scope);
                        return result;
                }
            }
        }

        // capture the at on demand "is enabled" setting
        if (deserializedJson.features?.atOnDemand?.enabled is not null)
        {
            result.AtOnDemandIsEnabled = deserializedJson.features.atOnDemand.enabled.Value;
        }
        else
        {
            // NOTE: for version 0 of the config.json file, we set AtOnDemandIsEnabled to FALSE by default
            if (deserializedJson.version == 0)
            {
                result.AtOnDemandIsEnabled = false;
            }
        }

        // capture the at use counter "is enabled" setting
        if (deserializedJson.features?.atUseCounter?.enabled is not null)
        {
            result.AtUseCounterIsEnabled = deserializedJson.features.atUseCounter.enabled.Value;
        }
        //else
        //{
        //     // NOTE: for version 0 of the config.json file, we set AtUseCounterIsEnabled to FALSE by default
        //     if (deserializedJson.version == 0)
        //     {
        //          // result.AtUseCounterIsEnabled = false; // not needed, as this feature is disabled by default for all users
        //     }
        //}

        // capture the "hide MorphicBar until" setting (date only)
        if (deserializedJson.hideMorphicAfterLoginUntil is not null)
        {
            var hideMorphicAfterLoginUntilAsIso8601 = deserializedJson.hideMorphicAfterLoginUntil;
            var parseHideMorphicAfterLoginUntilResult = App.ParseDateUsingIso8601(hideMorphicAfterLoginUntilAsIso8601);
            if (parseHideMorphicAfterLoginUntilResult.IsError == true)
            {
                Logger?.LogError("hideMorphicAtLoginUntil date cannot be parsed (must be in yyyy-mm-dd or yyyymmdd format): " + deserializedJson.hideMorphicAfterLoginUntil);
                return result;
            }
            result.HideMorphicAfterLoginUntil = parseHideMorphicAfterLoginUntilResult.Value!;
        }

        // capture the check for updates "is enabled" setting
        if (deserializedJson.features?.checkForUpdates?.enabled is not null)
        {
            result.CheckForUpdatesIsEnabled = deserializedJson.features.checkForUpdates.enabled.Value;
        }
        else
        {
            // NOTE: for version 0 of the config.json file, we set CheckForUpdatesIsEnabled to FALSE by default
            if (deserializedJson.version == 0)
            {
                result.CheckForUpdatesIsEnabled = false;
            }
        }

        // capture the cloud settings transfer "is enabled" setting
        if (deserializedJson.features?.cloudSettingsTransfer?.enabled is not null)
        {
            result.CloudSettingsTransferIsEnabled = deserializedJson.features.cloudSettingsTransfer.enabled.Value;
        }

        // capture the reset settings (to standard) "is enabled" setting
        if (deserializedJson.features?.resetSettings?.enabled is not null)
        {
            result.ResetSettingsIsEnabled = deserializedJson.features.resetSettings.enabled.Value;
        }

        // capture the desired after-login (autorun) visibility of the MorphicBar
        switch (deserializedJson.morphicBar?.visibilityAfterLogin)
        {
            case "restore":
                result.MorphicBarVisibilityAfterLogin = ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption.Restore;
                break;
            case "show":
                result.MorphicBarVisibilityAfterLogin = ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption.Show;
                break;
            case "hide":
                result.MorphicBarVisibilityAfterLogin = ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption.Hide;
                break;
            case null:
                // no setting present; use the default setting
                break;
            default:
                // sorry, we don't understand this visibility setting
                // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                Logger?.LogError("Unknown morphicBar.visibilityAfterLogin setting: " + deserializedJson.morphicBar?.visibilityAfterLogin);
                return result;
        }


        // capture any extra items (up to 3)
        if (deserializedJson.morphicBar?.extraItems is not null)
        {
            foreach (var extraItem in deserializedJson.morphicBar!.extraItems)
            {
                // if we already captured 3 extra items, skip this one
                if (result.ExtraMorphicBarItems.Count >= 3)
                {
                    continue;
                }

                var extraItemType = extraItem.type;
                var extraItemLabel = extraItem.label;
                var extraItemTooltipHeader = extraItem.tooltipHeader;
                var extraItemTooltipText = extraItem.tooltipText;
                // for type: link
                var extraItemUrl = extraItem.url;
                // for type: action
                var extraItemFunction = extraItem.function;
                // for type: control
                var extraItemFeature = extraItem.feature;
                // for type: application
                var extraItemAppId = extraItem.appId;

                // if the item is invalid, log the error and skip this item
                if (extraItemType is null)
                {
                    // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                    Logger?.LogError("Invalid MorphicBar item: " + extraItem.ToString());
                    continue;
                }
                if ((extraItemType != "control") && (extraItemLabel is null))
                {
                    // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                    Logger?.LogError("Invalid MorphicBar item: " + extraItem.ToString());
                    continue;
                }

                // if the "application" is missing its appId, log the error and skip this item
                if ((extraItemType == "application") && (extraItemAppId is null))
                {
                    // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                    Logger?.LogError("Invalid MorphicBar item: " + extraItem.ToString());
                    continue;
                }

                // if the "link" is missing its url, log the error and skip this item
                if ((extraItemType == "link") && (extraItemUrl is null))
                {
                    // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                    Logger?.LogError("Invalid MorphicBar item: " + extraItem.ToString());
                    continue;
                }

                // if the "action" is missing its function, log the error and skip this item
                if ((extraItemType == "action") && (extraItemFunction is null || extraItemFunction == ""))
                {
                    // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                    Logger?.LogError("Invalid MorphicBar item: " + extraItem.ToString());
                    continue;
                }

                // if the "control" is missing its feature, log the error and skip this item
                if ((extraItem.type == "control") && (extraItemFeature is null || extraItemFeature == ""))
                {
                    // NOTE: consider refusing to start up (for security reasons) if the configuration file cannot be read
                    Logger?.LogError("Invalid MorphicBar item: " + extraItem.ToString());
                    continue;
                }

                var extraMorphicBarItem = new MorphicBarExtraItem();
                extraMorphicBarItem.type = extraItemType;
                extraMorphicBarItem.label = extraItemLabel;
                extraMorphicBarItem.tooltipHeader = extraItemTooltipHeader;
                extraMorphicBarItem.tooltipText = extraItemTooltipText;
                extraMorphicBarItem.url = extraItemUrl;
                extraMorphicBarItem.function = extraItemFunction;
                extraMorphicBarItem.feature = extraItemFeature;
                extraMorphicBarItem.appId = extraItemAppId;
                result.ExtraMorphicBarItems.Add(extraMorphicBarItem);
            }
        }

        // capture telemetry site id
        result.TelemetrySiteId = deserializedJson.telemetry?.siteId;

        return result;
    }

    private bool ShouldTelemetryBeDisabled()
    {
        // NOTE: we have intentionally chosen not to create the CommonConfigDir (e.g. "C:\ProgramData\Morphic") since Morphic does not currently create files in this folder.
        var morphicCommonConfigPath = AppPaths.GetCommonConfigDir("", false);
        if (Directory.Exists(morphicCommonConfigPath) == false)
        {
            // if the Morphic common config path doesn't exist, there's definitely no file
            return false;
        }
        //
        var disableTelemetryFilePath = Path.Combine(morphicCommonConfigPath, "disable_telemetry.txt");

        // if disable_telemetry.txt exists, disable telemetry
        var disableTelemetryFileExists = File.Exists(disableTelemetryFilePath);
        return disableTelemetryFileExists;
    }

    internal string? LocalizeTemplatedString(string? value)
    {
        // if value is null, return null now
        if (value is null)
        {
            return value;
        }

        // replace any l10n-templated resource tags with their respective localized string
        var remainingValue = value;
        var result = new System.Text.StringBuilder();
        while (remainingValue.Length > 0)
        {
            var indexOfTemplateStart = remainingValue.IndexOf("{{");
            var indexOfTemplateEnd = remainingValue.IndexOf("}}", indexOfTemplateStart + 2);
            if (indexOfTemplateStart >= 0 && indexOfTemplateEnd >= indexOfTemplateStart + 2)
            {
                // extract the resource name (and then remove the resource tag from the string)
                var resourceName = remainingValue.Substring(indexOfTemplateStart + 2, indexOfTemplateEnd - indexOfTemplateStart - 2);
                remainingValue = remainingValue.Remove(indexOfTemplateStart, indexOfTemplateEnd - indexOfTemplateStart + 2);

                // if the string contained content before the resource tag, capture it as part of the result before capturing the localized resource
                if (indexOfTemplateStart > 0)
                {
                    var textBeforeTemplatedSection = remainingValue.Substring(0, indexOfTemplateStart);
                    remainingValue = remainingValue.Remove(0, indexOfTemplateStart);
                    result.Append(textBeforeTemplatedSection);
                }

                // get the localized resource; if it doesn't exist, revert to the resource tag instead
                string? localizedText = (string?)Application.Current.FindResource(resourceName.Trim());
                if (localizedText is null)
                {
                    localizedText = "{{" + resourceName + "}}";
                }
                else
                {
                    localizedText = localizedText.Replace("\\n", "\n");
                }
                result.Append(localizedText);
            }
            else
            {
                // no (more) resource tags to localize
                result.Append(remainingValue);
                remainingValue = string.Empty;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Create a Configuration from appsettings.json
    /// </summary>
    /// <returns></returns>
    private IConfiguration GetConfiguration()
    {
        ConfigurationBuilder builder = new ConfigurationBuilder();
        builder.SetBasePath(Directory.GetCurrentDirectory());
        builder.AddJsonFile("appsettings.json", optional: false);
        if (this.AppOptions.Launch.Debug)
        {
            builder.AddJsonFile("appsettings.Debug.json", optional: true);
            builder.AddJsonFile("appsettings.Local.json", optional: true);
        }
        builder.AddEnvironmentVariables();
        return builder.Build();
    }

    /// <summary>
    /// Configure the dependency injection system with services
    /// </summary>
    /// <param name="services"></param>
    private void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(this.ConfigureLogging);
        services.Configure<SessionOptions>(this.Configuration.GetSection("MorphicService"));
        services.Configure<UpdateOptions>(this.Configuration.GetSection("Update"));
        services.AddSingleton<IServiceCollection>(services);
        services.AddSingleton<IServiceProvider>(provider => provider);
        services.AddSingleton<SessionOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SessionOptions>>().Value);
        services.AddSingleton(new StorageOptions { RootPath = AppPaths.GetUserLocalConfigDir("Data") });
        services.AddSingleton(new KeychainOptions { Path = AppPaths.GetUserLocalConfigDir("keychain") });
        services.AddSingleton<UpdateOptions>(serviceProvider => serviceProvider.GetRequiredService<IOptions<UpdateOptions>>().Value);
        services.AddSingleton<IDataProtection, DataProtector>();
        services.AddSingleton<IUserSettings, WindowsUserSettings>();
        services.AddSingleton<Solutions>();
        services.AddSingleton<Keychain>();
        services.AddSingleton<Storage>();
        services.AddSingleton<MorphicSession>();
        services.AddTransient<TravelWindow>();
        services.AddTransient<CreateAccountPanel>();
        services.AddTransient<CapturePanel>();
        services.AddTransient<TravelCompletedPanel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<LoginPanel>();
        services.AddTransient<CopyStartPanel>();
        services.AddTransient<ApplyPanel>();
        services.AddTransient<RestoreWindow>();
        services.AddSingleton<Backups>();
        services.AddTransient<BarData>();
        //
        // AToD-related
        services.AddTransient<Morphic.Client.Dialogs.AtOnDemand.SelectAppsPanel>();
        services.AddTransient<Morphic.Client.Dialogs.AtOnDemand.DownloadAndInstallAppsPanel>();
        services.AddTransient<Morphic.Client.Dialogs.AtOnDemand.AtOnDemandCompletePanel>();
        //
        services.AddSingleton<BarPresets>(s => BarPresets.Default);
        services.AddSolutionsRegistryServices();
        services.AddSingleton<Solutions>(s => Solutions.FromFile(s, AppPaths.GetAppFile("solutions.json5")));
    }

    internal async Task Telemetry_RecordEventAsync(string Key)
    {
        if (ConfigurableFeatures.TelemetryIsEnabled == true)
        {
            _telemetryClient?.EnqueueEvent(Key, null);
        }
    }

    #region Telemetry 

    private Guid? _telemetrySessionId = null;

    private async Task ConfigureTelemetryAsync()
    {
        // TODO: Move metrics-related things to their own class.

        // retrieve the telemetry device ID for this device; if it doesn't exist then create a new one
        var telemetryIds = await Morphic.Client.Utils.TelemetryUtils.GetOrCreateTelemetryIdComponentsAsync();
        var telemetryCompositeId = telemetryIds.CompositeId;
        var telemetrySiteId = telemetryIds.SiteId;
        var telemetryDeviceUuid = telemetryIds.DeviceUuid;

        // configure our telemetry uplink
        IConfigurationSection? section = this.Configuration.GetSection("Telemetry");
        var mqttHostname = section["ServerHostname"];
        var mqttClientId = telemetryDeviceUuid;
        var mqttUsername = section["AppName"];
        var mqttAnonymousPassword = section["AppKey"];

        var mqttConfig = new MorphicTelemetryClient.WebsocketTelemetryClientConfig(
             hostname: mqttHostname,
             port: 443,
             path: "/ws",
             clientId: mqttClientId,
             username: mqttUsername,
             password: mqttAnonymousPassword,
             useTls: true
        );
        MorphicTelemetryClient? telemetryClient = null;
        //
        // TODO: place this log in the 
        string? userLocalAppDirectory = null;
        try
        {
            userLocalAppDirectory = Morphic.Client.Config.AppPaths.UserLocalConfigDir;
            if (System.IO.Directory.Exists(userLocalAppDirectory) == false)
            {
                System.IO.Directory.CreateDirectory(userLocalAppDirectory);
            }
        }
        catch { }
        //
        if (userLocalAppDirectory is not null)
        {
            var pathToOnDiskTransactionLog = Path.Combine(userLocalAppDirectory, "telemetry.log");
            var createTelemetryClientResult = await MorphicTelemetryClient.CreateUsingOnDiskTransactionLogAsync(mqttConfig, pathToOnDiskTransactionLog);
            if (createTelemetryClientResult.IsSuccess == true)
            {
                // we were able to read in the on-disk telemetry log (or create it); proceed with the newly-instantiated telemetry client
                telemetryClient = createTelemetryClientResult.Value!;
            }
            else // createTelemetryClientResult.IsError == true
            {
                // if we could not open the on-disk transaction log, attempt to delete the log and try to create a new file instead
                try
                {
                    // try to delete the existing file
                    System.IO.File.Delete(pathToOnDiskTransactionLog);

                    // try to create a new telemetry file at the specified path
                    createTelemetryClientResult = await MorphicTelemetryClient.CreateUsingOnDiskTransactionLogAsync(mqttConfig, pathToOnDiskTransactionLog);
                    if (createTelemetryClientResult.IsSuccess == true)
                    {
                        telemetryClient = createTelemetryClientResult.Value!;
                    }
                }
                catch { }
            }
        }
        if (telemetryClient is null)
        {
            // if we could not create a telemetry file at the specified path, simply create a telemetry client (without on-disk persistance)
            telemetryClient = MorphicTelemetryClient.Create(mqttConfig);
        }
        //
        if (telemetrySiteId is not null)
        {
            // if a site id is provided, remove any disallowed characters; if no characters remain, set the siteid to null
            var sanitizedTelemetrySiteId = Morphic.Client.Utils.TelemetryUtils.SanitizeSiteId(telemetrySiteId!);
            telemetrySiteId = sanitizedTelemetrySiteId != "" ? sanitizedTelemetrySiteId : null;
        }
        telemetryClient.SetSiteId(telemetrySiteId);
        _telemetryClient = telemetryClient;

        // create random session id
        _telemetrySessionId = Guid.NewGuid();

        //

        // send the first telemetry message (@session begin)
        // NOTE: we enqueue this message as soon as we create the telemetry client object
        var eventData = new SessionTelemetryEventData()
        {
            SessionId = _telemetrySessionId,
            State = "begin"
        };
        telemetryClient.EnqueueEvent("@session", eventData);

        // initialize (and start) our heartbeat timer; it should send the heartbeat message every 12 hours (i.e. twice a day, so that at least one event is recorded per active session per day)
        _telemetryHeartbeatTimer = new System.Threading.Timer(this.SendTelemetryHeartbeat, null, new TimeSpan(12, 0, 0), new TimeSpan(12, 0, 0));
    }

    internal record SessionTelemetryEventData
    {
        [JsonPropertyName("session_id")]
        public Guid? SessionId { get; set; }
        //
        [JsonPropertyName("state")]
        public string? State { get; set; }
    }

    #endregion Telemetry

    void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // TODO: Improve error logging/reporting.

        Exception ex = e.Exception;

        try
        {
            this.Logger.LogError("handled uncaught exception: {msg}", ex.Message);
            this.Logger.LogError(ex.StackTrace);
        }
        catch (Exception)
        {
            // ignore
        }

        Console.WriteLine(ex);

        // in case of unhandled exception, attempt a graceful shutdown
        //
        // uncomment the following line (if it's useful) for (and only during) debugging
        //            MessageBox.Show($"Morphic ran into a problem:\n\n{e.Exception.Message}\n\nFurther information:\n{e.Exception}", "Morphic", MessageBoxButton.OK, MessageBoxImage.Warning);
        //
        try
        {
            this.BarManager.CloseBar();
        }
        catch { }
        //
        try
        {
            this.Shutdown();
        }
        catch
        {
            // if we were unable to shutdown the application, hard-exit instead
            System.Environment.Exit(1);
        }

        // This prevents the exception from crashing the application
        e.Handled = true;
    }

    /// <summary>
    /// Configure the logging for the application
    /// </summary>
    /// <param name="logging"></param>
    private void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.AddConfiguration(this.Configuration);
        logging.AddConsole();
        logging.AddFile(this.AppOptions.Launch.Logfile, options =>
        {
            options.Append = true;
            options.FileSizeLimitBytes = 0x100000;
            options.MaxRollingFiles = 3;
        });
        logging.SetMinimumLevel(LogLevel.Debug);
        logging.AddDebug();
    }

    private static List<WindowsVersion> CompatibleWindowsVersions = new List<WindowsVersion>()
       {
           // NOTE: the first entry in this list represents the "minimum" version of Windows which we support
           WindowsVersion.Win10_v2004,
           WindowsVersion.Win10_v20H2,
           WindowsVersion.Win10_v21H1,
           WindowsVersion.Win10_v21H2,
           WindowsVersion.Win10_v22H2,
           WindowsVersion.Win10_vFuture,
           //
           WindowsVersion.Win11_v21H2,
           WindowsVersion.Win11_v22H2,
           WindowsVersion.Win11_v23H2,
           WindowsVersion.Win11_v24H2,
           WindowsVersion.Win11_vFuture
       };
    private static bool IsOsCompatibleWithMorphic()
    {
        var windowsVersion = OsVersion.GetWindowsVersion();

        if (windowsVersion is null)
        {
            // not a valid version
            return false;
        }
        else if (App.CompatibleWindowsVersions.Contains(windowsVersion.Value) == true)
        {
            return true;
        }
        else
        {
            // either this is an old verison of Windows or it's one we missed that we do support
            Debug.Assert(false, "Incompatible or unknown version of Windows");
            return false;
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        this.Dispatcher.UnhandledException += this.App_DispatcherUnhandledException;

        if (App.IsOsCompatibleWithMorphic() == false)
        {
            MessageBox.Show($"Morphic is not compatible with the current version of Windows.\r\n\r\nPlease upgrade to Windows 10 " + App.CompatibleWindowsVersions[0] + " or newer (or to Windows 11 or newer).");

            this.Shutdown();
            return;
        }

        var isRunningAfterLogin = Environment.GetCommandLineArgs().Contains("--run-after-login");

        this.Configuration = this.GetConfiguration();
        ServiceCollection collection = new ServiceCollection();
        this.ConfigureServices(collection);
        this.ServiceProvider = collection.BuildServiceProvider();

        base.OnStartup(e);
        this.Logger = this.ServiceProvider.GetRequiredService<ILogger<App>>();

        // determine if telemetry should be enabled
        var telemetryShouldBeDisabled = this.ShouldTelemetryBeDisabled();
        var telemetryIsEnabled = (telemetryShouldBeDisabled == false);

        // load (optional) common configuration file
        // NOTE: we currently load this AFTER setting up the logger because the GetCommonConfigurationAsync function logs config file errors to the logger
        var commonConfiguration = await this.GetCommonConfigurationAsync();
        ConfigurableFeatures.SetFeatures(
            atOnDemandIsEnabled: commonConfiguration.AtOnDemandIsEnabled,
            atUseCounterIsEnabled: commonConfiguration.AtUseCounterIsEnabled,
            hideMorphicAfterLoginUntil: commonConfiguration.HideMorphicAfterLoginUntil,
            autorunConfig: commonConfiguration.AutorunConfig,
            checkForUpdatesIsEnabled: commonConfiguration.CheckForUpdatesIsEnabled,
            cloudSettingsTransferIsEnabled: commonConfiguration.CloudSettingsTransferIsEnabled,
            resetSettingsIsEnabled: commonConfiguration.ResetSettingsIsEnabled,
            telemetryIsEnabled: telemetryIsEnabled,
            morphicBarvisibilityAfterLogin: commonConfiguration.MorphicBarVisibilityAfterLogin,
            morphicBarExtraItems: commonConfiguration.ExtraMorphicBarItems,
            telemetrySiteId: commonConfiguration.TelemetrySiteId
            );

        // before initializing any user interface, initialize our localization culture
        var currentUICulture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        var iso639LanguageCode = Morphic.Localization.LocalizationManager.GetIso639LanguageCode(currentUICulture);
        //
        // NOTE: if the current culture is not supported (or if it's the same as the base culture), fail silently and use the base settings
        _ = Morphic.Localization.LocalizationManager.SetUICulture(App.Current.Resources, iso639LanguageCode);

        // determine if Morphic (i.e. the taskbar icon, the MorphicBar, etc.) should be shown
        bool morphicShouldBeHidden = false;
        if (isRunningAfterLogin == true)
        {
            if ((ConfigurableFeatures.HideMorphicAfterLoginUntil is not null) && (DateTime.Now < ConfigurableFeatures.HideMorphicAfterLoginUntil))
            {
                morphicShouldBeHidden = true;
            }
        }

        this.MorphicSession = this.ServiceProvider.GetRequiredService<MorphicSession>();
        this.MorphicSession.UserChangedAsync += this.Session_UserChangedAsync;

        this.Logger.LogInformation("App Started");

        // create a single instance of our main menu
        this.MorphicMainMenu = new();
        //
        // NOTE: if the current culture is not supported (or if it's the same as the base culture), fail silently and use the base settings
        _ = Morphic.Localization.LocalizationManager.SetUICulture(this.MorphicMainMenu.Resources, iso639LanguageCode);

        // initialize our taskbar icon (button); this will not show the button
        this.InitTaskbarIconWithoutShowing();

        // if Morphic (including the taskbar icon button) should be visible, show them now
        if (morphicShouldBeHidden == false)
        {
            this.HybridTrayIcon.Visible = true;
        }

        // initialize our theme manager; this will also set the initial theme for our application
        this.ThemeManager = new();

        this.RegisterGlobalHotKeys();

        // NOTE: if Morphic should be hidden, don't start up telemetry; telemetry sessions are associated with a Morphic session which is started up because the user is actively able to use Morphic
        if (morphicShouldBeHidden == false)
        {
            // NOTE: in the current implementation, we must call GetCommonConfigurationAsync (above) before calling ConfigureTelemetryAsync (because it relies on the site id already being established)
            if (ConfigurableFeatures.TelemetryIsEnabled == true)
            {
                await this.ConfigureTelemetryAsync();
            }
        }

        // NOTE: in the current implementation, we must call GetCommonConfigurationAsync (above) before calling ConfigureAndStartAtUseCounterAsync (because it relies on the site id already being established)
        if (ConfigurableFeatures.AtUseCounterIsEnabled == true)
        {
            // retrieve the telemetry device ID for this device; if it doesn't exist then create a new one
            var telemetryIds = await Morphic.Client.Utils.TelemetryUtils.GetOrCreateTelemetryIdComponentsAsync();

            // configure our telemetry uplink
            IConfigurationSection? section = this.Configuration.GetSection("AtUseCounter");
            var mqttHostname = section["ServerHostname"];
            var mqttUsername = section["AppName"];
            var mqttAnonymousPassword = section["AppKey"];

            await Morphic.Client.AtUseCounter.AtUseCounterEngine.ConfigureAndStartAtUseCounterAsync(mqttHostname, mqttUsername, mqttAnonymousPassword, telemetryIds);
        }

        if (ConfigurableFeatures.CheckForUpdatesIsEnabled == true)
        {
            this.StartCheckingForUpdates();
        }

        this.AddSettingsListener();

        this.BarManager.BarLoaded += BarManager_BarLoaded;

        if (morphicShouldBeHidden == true)
        {
            await this.OpenSessionAsync(morphicBarVisibilityOverride: false);
        }
        else
        {
            await this.OpenSessionAsync();
        }

        // Make settings displayed on the UI update when a system setting has changed, or when the app is focused.
        this.SystemSettingChanged += (sender, args) => SettingsHandler.SystemSettingChanged();
        AppFocus.Current.MouseEnter += (sender, args) => SettingsHandler.SystemSettingChanged();
        AppFocus.Current.Activated += (sender, args) => SettingsHandler.SystemSettingChanged();
    }

    /// <summary>
    /// Actions to perform when this instance is the first since installation.
    /// </summary>
    private async Task OnFirstRun()
    {
        this.Logger.LogInformation("Performing first-run tasks");

        // Set the magnifier to lens mode at 200%
        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\ScreenMagnifier", "Magnification", 200);
        Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\ScreenMagnifier", "MagnificationMode", 3);

        // Set the colour filter type - if it's not currently enabled.
        //bool filterOn = this.MorphicSession.GetBool(SettingsManager.Keys.WindowsDisplayColorFilterEnabled) == true;
        bool filterOn =
            await this.MorphicSession.GetSetting<bool>(SettingId.ColorFiltersEnabled);
        if (!filterOn)
        {
            await this.MorphicSession.SetSetting(SettingId.ColorFiltersFilterType, 5);
        }

        // Set the high-contrast theme, if high-contrast is off.
        bool highcontrastOn = await this.MorphicSession.GetSetting<bool>(SettingId.HighContrastEnabled);
        if (!highcontrastOn)
        {
            // change the user's high contrast theme to the yellow-on-black high contrast theme (theme #1)
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes",
                "LastHighContrastTheme", @"%SystemRoot\resources\Ease of Access Themes\hc1.theme",
                RegistryValueKind.ExpandString);
            //
            // For windows 10 1903+
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast",
                "High Contrast Scheme", "High Contrast #1");
        }
    }

    #region Taskbar Icon (Button)

    private void InitTaskbarIconWithoutShowing()
    {
        // TODO: re-implement using solutions registry.
        // SystemSetting filterType = new SystemSetting("SystemSettings_Notifications_ShowIconsOnTaskbar",
        //     new LoggerFactory().CreateLogger<SystemSetting>());
        // var allNotificationIconsShown = (await filterType.GetValue() as bool? == true) ? TrayIcon.TrayIconLocationOption.NotificationTray : TrayIcon.TrayIconLocationOption.NextToNotificationTry;

        // get the default Morphic icon
        var morphicIconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Morphic.Client.Assets.Icons.morphic.ico")!;
        System.Drawing.Icon morphicIcon = new(morphicIconStream);

        // create an instance of our tray icon (button)
        var hybridTrayIcon = new Morphic.Controls.HybridTrayIcon()
        {
            Icon = morphicIcon,
            Text = "Morphic",
            TrayIconLocation = Controls.HybridTrayIcon.TrayIconLocationOption.NextToNotificationTray,
            Visible = false,
        };
        this.HybridTrayIcon = hybridTrayIcon;

        // wire up click and right-click events for our hybrid tray icon
        this.HybridTrayIcon.Click += HybridTrayIcon_Click;
        this.HybridTrayIcon.SecondaryClick += HybridTrayIcon_SecondaryClick;
    }

    //

    internal void SuppressTaskbarButtonResurfaceChecks(bool suppress)
    {
        this.HybridTrayIcon?.SuppressTaskbarButtonResurfaceChecks(suppress);
    }

    //

    // NOTE: this event is called on a non-UI thread
    private async void HybridTrayIcon_Click(object? sender, System.EventArgs e)
    {
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (this.BarManager.BarVisible)
            {
                this.BarManager.HideBar();
                //
                await App.Current.Telemetry_RecordEventAsync("morphicBarHide");
            }
            else
            {
                this.BarManager.ShowBar();
                //
                await App.Current.Telemetry_RecordEventAsync("morphicBarShow");
            }
        });
    }

    // NOTE: this event is called on a non-UI thread
    private async void HybridTrayIcon_SecondaryClick(object? sender, System.EventArgs e)
    {
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await this.MorphicMainMenu.ShowAsync(null, Morphic.Client.MainMenu.MorphicMainMenu.MenuOpenedSource.trayIcon);
        });
    }

    #endregion Taskbar Icon (Button

    private async Task ResetSettingsAsync()
    {
        // NOTE: we want to move these defaults to config.json, and we want to modify the solutions registry to allow _all_ settings to be specified,
        //       with defaults, in config.json.

        // default values
        var colorFiltersEnabledDefault = false;
        var darkModeEnabledDefault = false;
        var highContrastEnabledDefault = false;
        //
        // NOTE: displayDpiOffsetDefault realistically needs to be a fixed value ("recommended value") until we have logic to adjust by a relative % 
        int displayDpiOffsetDefault = 0;
        //
        var nightModeIsEnabled = false;

        // verify that settings are reset to their default values; if they are not, then set them now
        // NOTE: we do these in an order that makes sense during logout (i.e. we try to do as much as we can before Windows wants to close us, so we push
        //       settings like screen scaling, dark mode and high contrast to the end since they take much longer to change)
        //
        // color filters
        if (await this.MorphicSession.GetSetting<bool>(SettingId.ColorFiltersEnabled) != colorFiltersEnabledDefault)
        {
            await this.MorphicSession.SetSetting(SettingId.ColorFiltersEnabled, colorFiltersEnabledDefault);
        }
        //
        // night mode
        if (await this.MorphicSession.GetSetting<bool>(SettingId.NightModeEnabled) != nightModeIsEnabled)
        {
            await this.MorphicSession.SetSetting(SettingId.NightModeEnabled, nightModeIsEnabled);
        }
        //
        // screen scaling (on all monitors)
        var getAllDisplaysResult = Morphic.WindowsNative.Display.Display.GetAllDisplays();
        if (getAllDisplaysResult.IsSuccess == true)
        {
            var allDisplays = getAllDisplaysResult.Value!;

            foreach (var display in allDisplays)
            {
                // get the current DPI offset for the monitor
                var getCurrentDisplayDpiOffsetAndRangeResult = display.GetCurrentDpiOffsetAndRange();
                if (getCurrentDisplayDpiOffsetAndRangeResult.IsSuccess == true)
                {
                    var currentDisplayDpiOffset = getCurrentDisplayDpiOffsetAndRangeResult.Value!;
                    if (currentDisplayDpiOffset.CurrentDpiOffset != displayDpiOffsetDefault)
                    {
                        _ = await display.SetDpiOffsetAsync(displayDpiOffsetDefault);
                    }
                }
            }
        }
        //
        //
        // high contrast
        if (await this.MorphicSession.GetSetting<bool>(SettingId.HighContrastEnabled) != highContrastEnabledDefault)
        {
            await this.MorphicSession.SetSetting(SettingId.HighContrastEnabled, highContrastEnabledDefault);
        }
        //
        // dark mode
        // NOTE: due to the interrelation between high contrast and dark mode, we reset dark mode AFTER resetting high contrast mode
        var darkModeIsEnabledResult = await Morphic.Client.Bar.Data.Actions.Functions.GetDarkModeStateAsync();
        if (darkModeIsEnabledResult.IsSuccess == true)
        {
            var darkModeIsEnabled = darkModeIsEnabledResult.Value!;

            if (darkModeIsEnabled != darkModeEnabledDefault)
            {
                await Morphic.Client.Bar.Data.Actions.Functions.SetDarkModeStateAsync(darkModeEnabledDefault);
            }
        }
        //
        //
        // word simplify
        var isOfficeInstalled = Morphic.Integrations.Office.WordRibbon.IsOfficeInstalled();
        if (isOfficeInstalled == true)
        {
            var isBasicSimplifyRibbonEnabledResult = Morphic.Integrations.Office.WordRibbon.IsBasicSimplifyRibbonEnabled();
            if (isBasicSimplifyRibbonEnabledResult.IsSuccess == true)
            {
                var isBasicSimplifyRibbonEnabled = isBasicSimplifyRibbonEnabledResult.Value!;
                if (isBasicSimplifyRibbonEnabled == true)
                {
                    _ = Morphic.Integrations.Office.WordRibbon.DisableBasicSimplifyRibbon();
                }
            }

            var isEssentialsSimplifyRibbonEnabledResult = Morphic.Integrations.Office.WordRibbon.IsEssentialsSimplifyRibbonEnabled();
            if (isEssentialsSimplifyRibbonEnabledResult.IsSuccess == true)
            {
                var isEssentialsSimplifyRibbonEnabled = isEssentialsSimplifyRibbonEnabledResult.Value!;
                if (isEssentialsSimplifyRibbonEnabled == true)
                {
                    _ = Morphic.Integrations.Office.WordRibbon.DisableEssentialsSimplifyRibbon();
                }
            }
        }
    }

    private async Task Session_UserChangedAsync(object? sender, MorphicSession.MorphicSessionSignInOrOutEventArgs e)
    {
        if (sender is MorphicSession morphicSession)
        {
            if (morphicSession.SignedIn)
            {
                var lastCommunityId = AppOptions.Current.LastCommunity;
                var lastMorphicbarId = AppOptions.Current.LastMorphicbarId;
                if (lastCommunityId is not null)
                {
                    // if the user previously selected a community bar, show that one now
                    // NOTE: the behavior here may be inconsistent with Morphic on macOS.  If the previously-selected bar is no longer valid (e.g. the user was removed from the community),
                    //       then we should select the first bar in their list (or the next one...depending on what the design spec says); we should do this consistently on both Windows and macOS
                    await this.BarManager.LoadSessionBarAsync(morphicSession, lastCommunityId, lastMorphicbarId);
                }
                else
                {
                    string? newUserSelectedCommunityId = null;
                    if (e.SignedInViaLoginForm == true)
                    {
                        // if the user just signed in and they have not previously selected a community bar on this computer, select their first-available bar in the list (and fall-back to the Basic bar)
                        if (morphicSession.Communities.Length > 0)
                        {
                            newUserSelectedCommunityId = morphicSession.Communities[0].Id;
                        }
                    }

                    if (newUserSelectedCommunityId is not null)
                    {
                        await this.BarManager.LoadSessionBarAsync(morphicSession, newUserSelectedCommunityId, null);
                    }
                    else
                    {
                        // if the user has not selected a community bar, show the basic bar
                        this.BarManager.LoadBasicMorphicBar();
                    }
                }
            }
            else
            {
                // if no user is signed in, clear out the last community tag
                AppOptions.Current.LastCommunity = null;
                AppOptions.Current.LastMorphicbarId = null;

                // if no user is signed in, load the basic bar
                this.BarManager.LoadBasicMorphicBar();
            }

            // reload our list of communities and re-select the current bar
            ResyncCustomMorphicBarMenuItems();
        }
    }

    private void BarManager_BarLoaded(object? sender, BarEventArgs e)
    {
        ResyncCustomMorphicBarMenuItems();
    }

    private void ResyncCustomMorphicBarMenuItems()
    {
        // clear all communities in the menu (before basic)
        var changeMorphicBarMenuItems = this.MorphicMainMenu.ChangeMorphicBar.Items;
        var numberOfMenuItems = changeMorphicBarMenuItems.Count;
        for (int i = 0; i < numberOfMenuItems; i++)
        {
            var submenuItem = (MenuItem)changeMorphicBarMenuItems[0];
            if (submenuItem.Name == "SelectBasicMorphicBar")
            {
                // when we reach the basic MorphicBar entry, exit our loop (so that we don't clear out any remaining items)
                break;
            }
            else
            {
                this.MorphicMainMenu.ChangeMorphicBar.Items.RemoveAt(0);
            }
        }

        bool addedCheckmarkByCurrentCommunityBar = false;

        for (int iCommunity = 0; iCommunity < this.MorphicSession.Communities.Length; iCommunity++)
        {
            var community = this.MorphicSession.Communities[iCommunity];
            //
            var allBarsForCommunity = this.MorphicSession.MorphicBarsByCommunityId[community.Id];
            if (allBarsForCommunity is null)
            {
                // NOTE: this scenario shouldn't happen, but it's a gracefully-degrading failsafe just in case
                continue;
            }
            foreach (var communityBar in allBarsForCommunity)
            {
                var newMenuItem = new MenuItem();
                newMenuItem.Header = communityBar.Name + " (from " + community.Name + ")";
                newMenuItem.Tag = community.Id + "/" + communityBar.Id;
                //
                if (community.Id == AppOptions.Current.LastCommunity)
                {
                    var markThisBar = false;
                    if (AppOptions.Current.LastMorphicbarId is null && addedCheckmarkByCurrentCommunityBar == false)
                    {
                        markThisBar = true;
                    }
                    else if (AppOptions.Current.LastMorphicbarId == communityBar.Id)
                    {
                        markThisBar = true;
                    }
                    if (markThisBar == true)
                    {
                        newMenuItem.IsChecked = true;
                        addedCheckmarkByCurrentCommunityBar = true;
                    }
                }
                newMenuItem.Click += CustomMorphicBarMenuItem_Click;
                //
                this.MorphicMainMenu.ChangeMorphicBar.Items.Insert(Math.Max(this.MorphicMainMenu.ChangeMorphicBar.Items.Count - 1, 0), newMenuItem);
            }
        }

        // if no custom bar was checked, mark the basic bar instead
        this.MorphicMainMenu.SelectBasicMorphicBar.IsChecked = (addedCheckmarkByCurrentCommunityBar == false);
    }

    private async void CustomMorphicBarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var senderAsMenuItem = (MenuItem)sender;
        //var communityName = senderAsMenuItem.Header;
        var (communityId, morphicbarId) = this.ParseMorphicbarMenuItemTag(senderAsMenuItem);

        await this.BarManager.LoadSessionBarAsync(this.MorphicSession, communityId, morphicbarId);
    }

    private (string communityId, string? morphicbarId) ParseMorphicbarMenuItemTag(MenuItem menuItem)
    {
        string communityId;
        string? morphicbarId;

        string tag = (string)menuItem.Tag;
        if (tag.IndexOf("/") >= 0)
        {
            communityId = tag.Substring(0, tag.IndexOf('/'));
            morphicbarId = tag.Substring(tag.IndexOf('/') + 1);
        }
        else
        {
            communityId = tag;
            morphicbarId = null;
        }

        return (communityId, morphicbarId);
    }

    private void RegisterGlobalHotKeys()
    {
        // NOTE: we have chosen to disable the "login hot key" feature in Morphic; we may bring this back in a future release (or we can rely on the user activating the MorphicBar and then opening the menu)
        //EventHandler<NHotkey.HotkeyEventArgs> loginHotKeyPressed = async (sender, e) =>
        //{
        //    // NOTE: if we want the login menu item to apply cloud-saved preferences after login, we should set this flag to true
        //    var applyPreferencesAfterLogin = ConfigurableFeatures.CloudSettingsTransferIsEnabled;
        //    var args = new Dictionary<string, object?>() { { "applyPreferencesAfterLogin", applyPreferencesAfterLogin } };
        //    await this.Dialogs.OpenDialogAsync<LoginWindow>(args);
        //};
        //try
        //{
        //    HotkeyManager.Current.AddOrReplace("Login with Morphic", Key.M, ModifierKeys.Control | ModifierKeys.Shift, loginHotKeyPressed);
        //}
        //catch
        //{
        //    this.Logger.LogError("Could not register hotkey Ctrl+Shift+M for 'Login with Morphic'");
        //}

        EventHandler<NHotkey.HotkeyEventArgs> showMorphicBarHotKeyPressed = async (sender, e) =>
        {
            await this.ShowMorphicBarAndEnsureMorphicIsNotHiddenAsync(setFocusToMorphicButton: true);
        };
        try
        {
            // TODO: should this hotkey be titled "Show MorphicBar" instead?
            //HotkeyManager.Current.AddOrReplace("Show Morphic", Key.M, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, showMorphicBarHotKeyPressed);
            //
            // NOTE: per request on 10-May-2021, this hotkey has been changed from Ctrl+Shift+Alt+M to Ctrl+Shift+Alt+Windows+M
            // TODO: consider changing this modifier key sequence back to Ctrl+Shift+Alt+M (and providing a dialog for the user to decide what key combo they wish to use)
            HotkeyManager.Current.AddOrReplace("Show Morphic", Key.M, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Windows, showMorphicBarHotKeyPressed);
        }
        catch
        {
            this.Logger.LogError("Could not register hotkey Ctrl+Shift+Alt+M for 'Show Morphic'");
        }
    }

    public async Task OpenSessionAsync(bool? morphicBarVisibilityOverride = null)
    {
        await this.MorphicSession.OpenAsync();

        // TODO: when the user first runs Morphic, we probably want to open a welcome window (where the user could then log in)
        //await this.Dialogs.OpenDialog<CommunityLoginWindow>();

        this.OnSessionOpened(morphicBarVisibilityOverride: morphicBarVisibilityOverride);
    }

    /// <summary>
    /// Called when the session open task completes
    /// </summary>
    /// <param name="task"></param>
    private async void OnSessionOpened(bool? morphicBarVisibilityOverride)
    {
        this.Logger.LogInformation("Session Open");

        if (ConfigurableFeatures.ResetSettingsIsEnabled == true)
        {
            await this.ResetSettingsAsync();
        }

        if (this.AppOptions.FirstRun)
        {
            await this.OnFirstRun();
        }

        // if no bar was already loaded, load the Basic bar
        if (this.BarManager.BarIsLoaded == false)
        {
            this.BarManager.LoadBasicMorphicBar(morphicBarVisibilityOverride);
        }
    }

    #endregion

    /// <summary>
    /// The main menu shown from the system tray icon
    /// </summary>

    internal async Task ShowMenuAsync(Control? control = null, MorphicMainMenu.MenuOpenedSource? menuOpenedSource = null)
    {
        await this.MorphicMainMenu?.ShowAsync(control, menuOpenedSource);
    }

    #region Updates

    void StartCheckingForUpdates()
    {
        UpdateOptions? updateOptions = this.ServiceProvider.GetRequiredService<UpdateOptions>();
        string? appCastUrl;
        if (App.WasInstalledUsingEnterpriseInstaller() == true)
        {
            appCastUrl = App.GetEnterpriseAppCastUrlForCurrentProcessor(updateOptions);
        }
        else
        {
            appCastUrl = App.GetAppCastUrlForCurrentProcessor(updateOptions);
        }
        if (string.IsNullOrEmpty(appCastUrl) == false)
        {
            AutoUpdater.Start(appCastUrl);
        }
    }

    internal static bool WasInstalledUsingEnterpriseInstaller()
    {
        string? setupPackageVariantAsString = null;

        var openMachineMorphicRegistryKeyResult = Morphic.WindowsNative.Registry.LocalMachine.OpenSubKey(@"Software\Raising the Floor\Morphic");
        if (openMachineMorphicRegistryKeyResult.IsSuccess == true)
        {
            var morphicRegistryKey = openMachineMorphicRegistryKeyResult.Value!;
            var getSetupPackageVariantResult = morphicRegistryKey.GetValueData("setupPackageVariant");
            if (getSetupPackageVariantResult.IsSuccess == true)
            {
                try
                {
                    setupPackageVariantAsString = (string)getSetupPackageVariantResult.Value!;
                }
                catch
                {
                    Debug.Assert(false, "Registry value 'setupPackageVariant' did not contain valid string data.");
                }
            }
        }

        if (setupPackageVariantAsString is null)
        {
            var openUserMorphicRegistryKeyResult = Morphic.WindowsNative.Registry.CurrentUser.OpenSubKey(@"Software\Raising the Floor\Morphic");
            if (openUserMorphicRegistryKeyResult.IsSuccess == true)
            {
                var morphicRegistryKey = openUserMorphicRegistryKeyResult.Value!;
                var getSetupPackageVariantResult = morphicRegistryKey.GetValueData("setupPackageVariant");
                if (getSetupPackageVariantResult.IsSuccess == true)
                {
                    try
                    {
                        setupPackageVariantAsString = (string)getSetupPackageVariantResult.Value!;
                    }
                    catch
                    {
                        Debug.Assert(false, "Registry value 'setupPackageVariant' did not contain valid string data.");
                    }
                }
            }
        }

        if (setupPackageVariantAsString is null)
        {
            Debug.Assert(false, "Could not detect whether Morphic was installed with Enterprise or non-Enterprise installer");
            // default to "not installed with enterprise installer"
            return false;
        }

        switch (setupPackageVariantAsString!.ToLowerInvariant())
        {
            case "morphicmsi":
                return false;
            case "morphicenterprisemsi":
                return true;
            default:
                Debug.Assert(false, "Registry value 'setupPackageVariant' contains unrecognized string data");
                // default to "not installed with enterprise installer"
                return false;
        }
    }

    internal static string GetAppCastUrlForCurrentProcessor(UpdateOptions updateOptions)
    {
        string appCastUrl;
        // NOTE: GetProcessorArchitecture might return the emulated architecture (in the case of X86 emulation on ARM64) instead of the actual architecture
        switch (Morphic.WindowsNative.Processor.Processor.GetProcessorArchitecture())
        {
            case WindowsNative.Processor.Processor.ProcessorArchitecture.X86:
                appCastUrl = updateOptions.AppCastUrlX86;
                break;
            case WindowsNative.Processor.Processor.ProcessorArchitecture.X64:
                appCastUrl = updateOptions.AppCastUrlX64;
                break;
            case WindowsNative.Processor.Processor.ProcessorArchitecture.Arm64:
                appCastUrl = updateOptions.AppCastUrlArm64;
                break;
            default:
                appCastUrl = updateOptions.AppCastUrl;
                Debug.Assert(false, "Could not detect architecture (to select auto-update URL)");
                break;
        }
        return appCastUrl;
    }

    internal static string? GetEnterpriseAppCastUrlForCurrentProcessor(UpdateOptions updateOptions)
    {
        string? appCastUrl;
        // NOTE: GetProcessorArchitecture might return the emulated architecture (in the case of X86 emulation on ARM64) instead of the actual architecture
        switch (Morphic.WindowsNative.Processor.Processor.GetProcessorArchitecture())
        {
            case WindowsNative.Processor.Processor.ProcessorArchitecture.X86:
                appCastUrl = updateOptions.AppCastUrlEnterpriseX86;
                break;
            case WindowsNative.Processor.Processor.ProcessorArchitecture.X64:
                appCastUrl = updateOptions.AppCastUrlEnterpriseX64;
                break;
            case WindowsNative.Processor.Processor.ProcessorArchitecture.Arm64:
                appCastUrl = updateOptions.AppCastUrlEnterpriseArm64;
                break;
            default:
                appCastUrl = null;
                Debug.Assert(false, "Could not detect architecture (to select auto-update URL)");
                break;
        }
        return appCastUrl;
    }

    #endregion

    private MessageWatcherNativeWindow? _messageWatcherNativeWindow;

    protected override void OnActivated(EventArgs e)
    {
        if (_messageWatcherNativeWindow is null)
        {
            // create a list of the messages we want to watch for
            List<uint> messagesToWatch = new List<uint>();
            messagesToWatch.Add(AppMain.SingleInstanceMessageId); // this is the message that lets us know that another instance of Morphic was started up

            _messageWatcherNativeWindow = new MessageWatcherNativeWindow(messagesToWatch);
            _messageWatcherNativeWindow.WatchedMessageEvent += _messageWatcherNativeWindow_WatchedMessageEvent;
            try
            {
                _messageWatcherNativeWindow.Initialize();
            }
            catch (Exception ex)
            {
                this.Logger.LogError("could not create messages watcher window: {msg}", ex.Message);
            }
        }

        base.OnActivated(e);
    }

    // NOTE: this function is called when SingleInstanceMessageId is received by the existing Morphic instance (i.e. when the user attempts to start a second instance of Morphic)
    private async void _messageWatcherNativeWindow_WatchedMessageEvent(object sender, MessageWatcherNativeWindow.WatchedMessageEventArgs args)
    {
        // when Morphic is launched a second time, show the MorphicBar (and ensure that Morphic is not "hidden" (i.e. that the tray icon is visible, that the telemetry session is started up appropriately, etc.)
        await this.ShowMorphicBarAndEnsureMorphicIsNotHiddenAsync();
    }

    private async Task ShowMorphicBarAndEnsureMorphicIsNotHiddenAsync(bool setFocusToMorphicButton = false)
    {
        // start up telemetry (if not already started)
        if (_telemetryClient is null)
        {
            // NOTE: in the current implementation, we must call GetCommonConfigurationAsync (above) before calling ConfigureTelemetryAsync (because it relies on the site id already being established)
            if (ConfigurableFeatures.TelemetryIsEnabled == true)
            {
                await this.ConfigureTelemetryAsync();
            }
        }

        // show the Morphic taskbar button (if not already shown)
        if (this.HybridTrayIcon is not null)
        {
            this.HybridTrayIcon!.Visible = true;
        }

        // show the MorphicBar (if not already shown)
        this.BarManager.ShowBar();
    }

    private void SendTelemetryHeartbeat(object? state)
    {
        // send a ping/heartbeat telemetry message (@session heartbeat)
        var eventData = new SessionTelemetryEventData()
        {
            SessionId = _telemetrySessionId,
            State = "heartbeat"
        };
        _telemetryClient?.EnqueueEvent("@session", eventData);
    }

    #region Shutdown

    protected override async void OnExit(ExitEventArgs e)
    {
        // immediately hide our tray icon (and dispose of it for good measure, to help ensure that unmanaged resources are cleaned up)
        this.HybridTrayIcon.Visible = false;
        this.HybridTrayIcon.Dispose();

        // NOTE: the CLR may shut down our application quicker than we can send the "session end" event; as we move to the Morphic 2.0 architecture (with cached telemetry messages), the "@session end" message should be more guaranteed to be transmitted

        _messageWatcherNativeWindow?.Dispose();
        if (ConfigurableFeatures.TelemetryIsEnabled == true)
        {
            // dispose of our telemetry heartbeat timer
            _telemetryHeartbeatTimer?.Dispose();

            try
            {
                if (_telemetryClient is not null)
                {
                    // send the final telemetry message (@session end)
                    // NOTE: for Morphic 2.0, enqueue this message as soon as we enter the OnExit function
                    var eventData = new SessionTelemetryEventData()
                    {
                        SessionId = _telemetrySessionId,
                        State = "end"
                    };
                    _telemetryClient.EnqueueEvent("@session", eventData);

                    // wait up to two seconds for the event to be sent
                    var waitTimeSpan = TimeSpan.FromSeconds(2);
                    // NOTE: PrepareForDisposalAsync will attempt to finish sending the current message(s); this will not necessarily send the message we just enqueued, but if not
                    //       then that message will be saved for the next telemetry server link-up and in the interim the telemetry server will count our last-sent event as the
                    //       end of the session instead.  It will also attempt to flush any remaining queued items out to the on-disk persistant log (so they can sent on the next run)
                    var cancellationTokenSource = new CancellationTokenSource();
                    var cancellationToken = cancellationTokenSource.Token;
                    var task = Task.Run(() =>
                    {
                        // NOTE: MorphicTelemetryClient.PrepareForDisposalAsync(...) may only be called once in the current implementation, so it's appropriate to call it here before shutdown
                        _telemetryClient.PrepareForDisposalAsync(waitTimeSpan).GetAwaiter().GetResult();
                        cancellationTokenSource.Cancel();
                    });
                    try
                    {
                        Task.Delay(waitTimeSpan, cancellationToken).GetAwaiter().GetResult();
                        // NOTE: if we reach here, the function did not return on time
                    }
                    catch (TaskCanceledException)
                    {
                        // task was ended before timeout, which is the expected behavior if we ended before timeout
                    }

                    // dispose of the telemetry client; note that this may take up to 250ms (as the dispose function waits up to 250ms for the in-memory logs to be flushed to disk)
                    _telemetryClient.Dispose();
                }
            }
            catch { }
        }

        if (ConfigurableFeatures.AtUseCounterIsEnabled == true)
        {
            // shutdown the AT Use Counter engine
            // NOTE: this function will wait up to two seconds (similar to the telemetry code above); we might want to move that "wait" code to this function instead (and perhaps wait on the telemetry and AtUseCounter telemetry systems to shut down in parallel instead of sequentially)
            await Morphic.Client.AtUseCounter.AtUseCounterEngine.ShutdownAtUseCounterAsync();
        }

        if (ConfigurableFeatures.ResetSettingsIsEnabled == true)
        {
            await this.ResetSettingsAsync();
        }

        AppMain.ReleaseSingleInstanceMutex();

        base.OnExit(e);
    }

    #endregion

    private static MorphicResult<DateTime, MorphicUnit> ParseDateUsingIso8601(string value)
    {
        // see: https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings
        // NOTE: we only allow a subset of ISO 8601 formatted dates; this list could be expanded in the future as appropriate
        var allowedFormats = new string[]
        {
                "yyyy-MM-dd",
                "yyyyMMdd",
        };

        DateTime valueAsDateTime;
        var stringValueIsValid = DateTime.TryParseExact(value, allowedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out valueAsDateTime);
        if (stringValueIsValid == false)
        {
            return MorphicResult.ErrorResult();
        }

        return MorphicResult.OkResult(valueAsDateTime);
    }


    #region SystemEvents

    public event EventHandler? SystemSettingChanged;

    private bool addedSystemEvents;
    private DispatcherTimer? systemSettingTimer;

    /// <summary>
    /// Start listening to some changes to system settings.
    /// </summary>
    private void AddSettingsListener()
    {
        if (this.addedSystemEvents)
        {
            return;
        }

        this.addedSystemEvents = true;
        this.systemSettingTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        this.systemSettingTimer.Tick += (sender, args) =>
        {
            this.systemSettingTimer.Stop();
            this.SystemSettingChanged?.Invoke(this, EventArgs.Empty);
        };

        // NOTE: to avoid needing to handle exceptions when setting up our DisplaySettingsChanged handler, we first manually start up the display settings listener; if our process was
        //       running without the ability or permissions to start up the appropriate system-/session-level listeners, we'd otherwise get an exception when wiring up the event(s).
        var startListeningForDisplaySettingsEventsResult = Morphic.WindowsNative.Display.DisplaySettingsListener.Shared.StartListening();
        if (startListeningForDisplaySettingsEventsResult.IsError == true)
        {
            // NOTE: in the future, we may want to log or otherwise capture the knowledge that we are unable to capture Win32 display settings changes
            Debug.Assert(false, "Could not listen for Win32 display settings changes");
        }
        // NOTE: wiring up this event will result in an exception if the display settings listener could not be started successfully (see note immediately above)
        Morphic.WindowsNative.Display.DisplaySettingsListener.Shared.DisplaySettingsChanged += this.SystemEventsOnDisplaySettingsChanged;
        //
        //
        // NOTE: to avoid needing to handle exceptions when setting up our UserPreferenceChanged handler, we first manually start up the user preference listener; if our process was
        //       running without the ability or permissions to start up the appropriate system-/session-level listeners, we'd otherwise get an exception when wiring up the event(s).
        var startListeningForUserPreferenceEventsResult = Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.StartListening();
        if (startListeningForUserPreferenceEventsResult.IsError == true)
        {
            // NOTE: in the future, we may want to log or otherwise capture the knowledge that we are unable to capture Win32 user preference changes
            Debug.Assert(false, "Could not listen for Win32 user preference changes");
        }
        // NOTE: wiring up this event will result in an exception if the user preference listener could not be started successfully (see note immediately above)
        // NOTE: all of these events were effectively wired up in Morphic v1.6 and earlier; we are wiring them all up out of an abundance of caution, but realistically we should only
        //       wire up the display-related user preference change notifications
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.AccessibilityUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.ColorUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.DesktopUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.GeneralUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.IconUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.KeyboardUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.LocaleUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.MenuUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.MouseUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.PolicyUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.PowerUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.ScreensaverUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.VisualStyleUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;
        Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.WindowUserPreferenceChanged += this.SystemEventsOnDisplaySettingsChanged;

        SystemEvents.SessionEnding += SystemEvents_SessionEnding;

        this.Exit += (sender, args) =>
        {
            // NOTE: technically, we no longer need to do this during exit because our finalizer SHOULD do it automatically [also: note that this Exit closure was not called in testing anyway]
            //Morphic.WindowsNative.Display.DisplaySettingsListener.Shared.DisplaySettingsChanged -= this.SystemEventsOnDisplaySettingsChanged;
            Morphic.WindowsNative.Display.DisplaySettingsListener.Shared.StopListening();

            // NOTE: technically, we no longer need to do this during exit because our finalizer SHOULD do it automatically [also: note that this Exit closure was not called in testing anyway]
            //// NOTE: all of these events were effectively wired up in Morphic v1.6 and earlier; we are wiring them all up out of an abundance of caution, but realistically we should only
            ////       wire up the display-related user preference change notifications
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.AccessibilityUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.ColorUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.DesktopUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.GeneralUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.IconUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.KeyboardUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.LocaleUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.MenuUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.MouseUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.PolicyUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.PowerUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.ScreensaverUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.VisualStyleUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            //Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.WindowUserPreferenceChanged -= this.SystemEventsOnDisplaySettingsChanged;
            Morphic.WindowsNative.UserPreferences.UserPreferenceListener.Shared.StopListening();
        };
    }

    private void SystemEventsOnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Wait a bit, to see if any other events have been raised.
        this.systemSettingTimer?.Start();
    }

    private async void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
    {
        // NOTE: in our preliminary testing, we do not have enough time during shutdown
        // to call/complete this function; we should look for a way to keep Windows from
        // forcibly logging out until we have completed our settings reset (or at least a few
        // critical 'reset settings' items)
        if (ConfigurableFeatures.ResetSettingsIsEnabled == true)
        {
            await this.ResetSettingsAsync();
        }
    }

    #endregion
}
