﻿namespace Morphic.Client.Dialogs
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using Elements;
    using Service;

    public partial class CopyStartPanel : StackPanel, IStepPanel
    {
        private readonly MorphicSession morphicSession;
        private readonly IServiceProvider serviceProvider;

        public CopyStartPanel(MorphicSession morphicSession, IServiceProvider serviceProvider)
        {
            this.morphicSession = morphicSession;
            this.serviceProvider = serviceProvider;
            this.InitializeComponent();
        }

        private Task<bool> EnsureLoggedOn(bool applyPreferencesAfterLogin)
        {
            Task<bool> task;

            if (this.morphicSession.User is null)
            {
                TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();
                task = completionSource.Task;

                LoginPanel loginPanel = this.StepFrame.PushPanel<LoginPanel>();
                loginPanel.ApplyPreferencesAfterLogin = applyPreferencesAfterLogin;
                loginPanel.Completed += (sender, args) =>
                {
                    completionSource.SetResult(true);
                };
            }
            else
            {
                task = Task.FromResult(false);
            }

            return task;
        }

        internal async void CopyToCloud(object sender, RoutedEventArgs e)
        {
            await this.EnsureLoggedOn(false /* applySettingsAfterLogin */);
            CapturePanel capturePanel = this.StepFrame.PushPanel<CapturePanel>();
            capturePanel.Completed += (o, args) => this.Completed?.Invoke(this, EventArgs.Empty);
        }

        internal async void CopyFromCloud(object sender, RoutedEventArgs e)
        {
            await this.EnsureLoggedOn(false /* applySettingsAfterLogin */);
            ApplyPanel applyPanel = this.StepFrame.PushPanel<ApplyPanel>();
            applyPanel.Completed += (o, args) => this.Completed?.Invoke(this, EventArgs.Empty);
        }

        #region IStepPanel

        public StepFrame StepFrame { get; set; } = null!;
        public event EventHandler? Completed;
        #endregion

    }
}

