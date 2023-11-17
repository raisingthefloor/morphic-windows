// BarManager.cs: Loads and shows bar.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt


namespace Morphic.Client.Bar
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;
    using Config;
    using Morphic.Core.Legacy;
    using Morphic.Core.Legacy.Community;
    using Data;
    using Dialogs;
    using Microsoft.Extensions.Logging;
    using Service;
    using UI;
    using MessageBox = System.Windows.Forms.MessageBox;
    using SystemJson = System.Text.Json;

    /// <summary>
    /// Looks after the bar.
    /// </summary>
    public class BarManager : INotifyPropertyChanged
    {
        private PrimaryBarWindow? barWindow;
        private ILogger Logger => App.Current.Logger;

        public event EventHandler<BarEventArgs>? BarLoaded;
        public event EventHandler<BarEventArgs>? BarUnloaded;

        private bool firstBar = true;

        public bool BarVisible => this.barWindow?.Visibility == Visibility.Visible;

        public BarManager()
        {
        }

        public bool BarIsLoaded { get; private set; } = false;

        /// <summary>
        /// Show a bar that's already loaded.
        /// </summary>
        public void ShowBar(bool setFocusToMorphicButton = false)
        {
            if (this.barWindow is not null)
            {
                AppOptions.Current.MorphicBarIsVisible = true;
                this.barWindow.Visibility = Visibility.Visible;
                // OBSERVATION: setting focus to the bar window doesn't necessarily move the keyboard focus away from the current app
                this.barWindow.Focus();
                if (setFocusToMorphicButton == true)
                {
                    // NOTE: we set focus to the morphic button first, and then we activate the bar window
                    this.barWindow.SetFocusToMorphicButton();
                    this.barWindow.Activate();
                }
            }
        }

        public void HideBar()
        {
            AppOptions.Current.MorphicBarIsVisible = false;
            this.barWindow?.Hide();
            this.barWindow?.OtherWindow?.Hide();
        }

        /// <summary>
        /// Closes the bar.
        /// </summary>
        public void CloseBar()
        {
            this.BarIsLoaded = false;

            if (this.barWindow is not null)
            {
                this.OnBarUnloaded(this.barWindow);
                BarData bar = this.barWindow.Bar;
                this.barWindow.IsClosing = true;
                this.barWindow.Close();
                this.barWindow = null;
                bar.Dispose();
            }
        }

        public BarWindow CreateBarWindow(BarData bar, bool? morphicBarVisibilityOverride = null)
        {
            this.barWindow = new PrimaryBarWindow(bar);
            this.barWindow.BarLoaded += this.OnBarLoaded;
            this.barWindow.IsVisibleChanged += this.BarWindowOnIsVisibleChanged;

            bool showMorphicBar = false;
            if (AppOptions.Current.AutoShow == true)
            {
                showMorphicBar = true;
            }
            if (this.firstBar == false)
            {
                showMorphicBar = true;
            }
            if (AppOptions.Current.MorphicBarIsVisible == true)
            {
                showMorphicBar = true;
            }
            if (ConfigurableFeatures.MorphicBarVisibilityAfterLogin is not null)
            {
                switch (ConfigurableFeatures.MorphicBarVisibilityAfterLogin.Value)
                {
                    case ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption.Show:
                        showMorphicBar = true;
                        break;
                    case ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption.Restore:
                        // if the bar has not been shown before, show it now; if it has been shown/hidden before, use the last known visibility state
                        showMorphicBar = AppOptions.Current.MorphicBarIsVisible ?? true;
                        break;
                    case ConfigurableFeatures.MorphicBarVisibilityAfterLoginOption.Hide:
                        showMorphicBar = false;
                        break;
                }
            }

            // if we were started up manually, always show the MorphicBar
            if (Environment.GetCommandLineArgs().Contains("--run-after-login") == false)
            {
                showMorphicBar = true;
            }
            if (morphicBarVisibilityOverride is not null)
            {
                showMorphicBar = morphicBarVisibilityOverride!.Value;
            }

            if (showMorphicBar == true)
            {
                AppOptions.Current.MorphicBarIsVisible = true;
                this.barWindow.Show();
            }

            this.firstBar = false;
            return this.barWindow;
        }

        private void BarWindowOnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.OnPropertyChanged(nameof(this.BarVisible));
        }

        /// <summary>
        /// Called when a bar has loaded.
        /// </summary>
        protected virtual void OnBarLoaded(object sender, EventArgs? args = null)
        {
            if (sender is PrimaryBarWindow window)
            {
                this.BarLoaded?.Invoke(this, new BarEventArgs(window));
            }
        }

        /// <summary>
        /// Called when a bar has closed.
        /// </summary>
        protected virtual void OnBarUnloaded(object sender, EventArgs? args = null)
        {
            if (sender is PrimaryBarWindow window)
            {
                this.BarUnloaded?.Invoke(this, new BarEventArgs(window));
            }
        }

        #region DataLoading
        private void OnBarOnReloadRequired(object? sender, EventArgs args)
        {
            if (sender is BarData bar)
            {
                string source = bar.Source;

                this.CloseBar();
                this.LoadFromBarJson(source);
            }
        }

        public BarData? LoadBasicMorphicBar(bool? morphicBarVisibilityOverride = null)
        {
            var result = LoadFromBarJson(AppPaths.GetConfigFile("basic-bar.json5", true), null, null, morphicBarVisibilityOverride);
            AppOptions.Current.LastCommunity = null;
            AppOptions.Current.LastMorphicbarId = null;
            return result;
        }

        /// <summary>
        /// Loads and shows a bar.
        /// </summary>
        /// <param name="path">JSON file containing the bar data.</param>
        /// <param name="content">The file content (if it's already loaded).</param>
        /// <param name="serviceProvider"></param>
        public BarData? LoadFromBarJson(string path, string? content = null, IServiceProvider? serviceProvider = null, bool? morphicBarVisibilityOverride = null)
        {
            if (this.firstBar && AppOptions.Current.Launch.BarFile is not null)
            {
                path = AppOptions.Current.Launch.BarFile;
            }

            BarData? bar = null;
            try
            {
                bar = BarData.Load(serviceProvider ?? App.Current.ServiceProvider, path, content);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                this.Logger.LogError(e, "Problem loading the bar.");
            }

            if (this.barWindow is not null)
            {
                this.CloseBar();
            }

            this.BarIsLoaded = true;

            if (bar is not null)
            {
                // if any of the items are application actions and the application isn't available, remove the button from the items collection
                var index = 0;
                while (index < bar.AllItems.Count)
                {
                    var actionAsApplicationAction = bar.AllItems[index].Action as Data.Actions.ApplicationAction;
                    if (actionAsApplicationAction is not null)
                    {
                        if (actionAsApplicationAction.IsAvailable == false)
                        {
                            bar.AllItems.RemoveAt(index);
                            continue;
                        }
                    }

                    index += 1;
                }

                this.CreateBarWindow(bar, morphicBarVisibilityOverride);
                bar.ReloadRequired += this.OnBarOnReloadRequired;
            }

            return bar;
        }

        /// <summary>
        /// Loads the bar for the given session. If the user is a member of several, either the last one is used,
        /// or a selection dialog is presented.
        /// </summary>
        /// <param name="session">The current session.</param>
        /// <param name="showCommunityId">Force this community to show.</param>
        public async Task LoadSessionBarAsync(MorphicSession session, string communityId, string? morphicbarId)
        {
            if (this.firstBar && AppOptions.Current.Launch.BarFile is not null)
            {
                this.LoadFromBarJson(AppOptions.Current.Launch.BarFile);
                return;
            }

            this.Logger.LogInformation($"Loading a bar ({session.Communities.Length} communities)");

            UserBar? bar;

            UserCommunity? community = null;
            UserBar? userBar = null;

            //if (session.Communities.Length == 0)
            //{
            //    MessageBox.Show("You are not part of a Morphic community yet.", "Morphic");
            //}
            //else if (session.Communities.Length == 1)
            //{
            //    community = session.Communities.First();
            //}
            //else
            //{
                // The user is a member of multiple communities.

                //// See if any membership has changed
                //bool changed = session.Communities.Length != lastCommunities.Length
                //    || !session.Communities.Select(c => c.Id).OrderBy(id => id)
                //        .SequenceEqual(lastCommunities.OrderBy(id => id));

                if (/*!changed &&*/ communityId is not null)
                {
                    community = session.Communities.FirstOrDefault(c => c.Id == communityId);
                }

                //if (community is null)
                //{
                //    this.Logger.LogInformation("Showing community picker");

                //    // Load the bars while the picker is shown
                //    Dictionary<string, Task<UserBar>> bars =
                //        session.Communities.ToDictionary(c => c.Id, c => session.GetBar(c.Id));

                //    // Show the picker
                //    CommunityPickerWindow picker = new CommunityPickerWindow(session.Communities);
                //    bool gotCommunity = picker.ShowDialog() == true;
                //    community = gotCommunity ? picker.SelectedCommunity : null;

                //    if (community is not null)
                //    {
                //        userBar = await bars[community.Id];
                //    }
                //}
            //}

            if (community is not null)
            {
                var legacyBars = await session.GetBarsAsync(community.Id);
                foreach (var legacyBar in legacyBars) {
                    var useThisBar = false;
                    if (morphicbarId is null)
                    {
                        // if the user selected this community id instead of a specific morphicbar (Morphic v1.0-v1.2), then use the first bar
                        useThisBar = true;
                    }
                    else if (legacyBar.Id == morphicbarId)
                    {
                        // if the user previously selected this specific morphicbar, use it
                        useThisBar = true;
                    }

                    if (useThisBar == true)
                    {
                        // OBSERVATION: not sure why the "??=" (userBar is null) check is done here; this logic seems brittle
                        if (userBar is null)
                        {
                            userBar = legacyBar;
                            break;
                        }
                    }
                }
                // NOTE: if the morphicbar was not found, we do not set it to null (to remain consistent with previous code logic)

                // added to protect against not finding the specific community bar
                if (userBar is not null)
                {
                    this.Logger.LogInformation($"Showing bar for community {community.Id} {community.Name}");
                    string barJson = this.GetUserBarJson(userBar);
                    BarData? barData = this.LoadFromBarJson(userBar.Id, barJson);
                    if (barData is not null)
                    {
                        barData.CommunityId = community.Id;
                    }

                    AppOptions.Current.LastCommunity = community?.Id;
                    AppOptions.Current.LastMorphicbarId = userBar.Id;
                }
                else
                { 
                    // if the community or the specific community bar could not be found, show the Basic MorphicBar instead
                    this.LoadBasicMorphicBar();

                    AppOptions.Current.LastCommunity = null;
                    AppOptions.Current.LastMorphicbarId = null;
                } 
            }
            else
            {
                // if the community could not be found, show the Basic MorphicBar instead
                this.LoadBasicMorphicBar();

                AppOptions.Current.LastCommunity = null;
                AppOptions.Current.LastMorphicbarId = null;
            }
        }

        /// <summary>
        /// Gets the json for a <see cref="UserBar"/>, so it can be loaded with a better deserialiser.
        /// </summary>
        /// <param name="userBar">Bar data object from Morphic.Core</param>
        private string GetUserBarJson(UserBar userBar)
        {
            // Serialise the bar data so it can be loaded with a better deserialiser.
            SystemJson.JsonSerializerOptions serializerOptions = new SystemJson.JsonSerializerOptions();
            serializerOptions.Converters.Add(new JsonElementInferredTypeConverter());
            serializerOptions.Converters.Add(
                new SystemJson.Serialization.JsonStringEnumConverter(SystemJson.JsonNamingPolicy.CamelCase));
            string barJson = SystemJson.JsonSerializer.Serialize(userBar, serializerOptions);

            // Dump to a file, for debugging.
            string barFile = AppPaths.GetConfigFile("last-bar.json5");
            File.WriteAllText(barFile, barJson);

            return barJson;
        }

        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

    }

    public class BarEventArgs : EventArgs
    {
        public BarEventArgs(PrimaryBarWindow window)
        {
            this.Window = window;
            this.Bar = this.Window.Bar;
        }

        public BarData Bar { get; private set; }
        public PrimaryBarWindow Window { get; private set; }

    }
}
