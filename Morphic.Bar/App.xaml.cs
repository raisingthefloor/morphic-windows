using System.Windows;

namespace Morphic.Bar
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows.Threading;
    using Bar;
    using Client;
    using Microsoft.Extensions.Logging;
    using UI;
    using UI.AppBarWindow;
    using Application = System.Windows.Application;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;

        private PrimaryBarWindow? barWindow;

        /// <summary>
        /// true if the current application is active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>The mouse has entered any window belonging to the application.</summary>
        public event EventHandler? MouseEnter;
        /// <summary>The mouse has left any window belonging to the application.</summary>
        public event EventHandler? MouseLeave;

        public ILogger Logger { get; }

        public App()
        {
            AppPaths.CreateAll();
            this.Logger = LogUtil.Init();

            this.Activated += (sender, args) => this.IsActive = true;
            this.Deactivated += (sender, args) => this.IsActive = false;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppPaths.Log(this.Logger);

            BarActions actions = BarActions.FromFile(AppPaths.GetConfigFile("actions.json5", true));

            string? barFile = Options.Current.BarFile;
            if (barFile == null)
            {
                MorphicService morphicService =
                    new MorphicService("https://dev-morphic.morphiclite-oregondesignservices.org/");
                string? barJson = await morphicService.GetBar();
                barFile = AppPaths.GetCacheFile("last-bar.json5");
                await File.WriteAllTextAsync(barFile, barJson);
            }

            this.LoadBar(barFile);
        }

        public void LoadBar(string path)
        {
            BarData? bar = null;

            try
            {
                bar = BarData.Load(path);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                this.Logger.LogError(e, "Problem loading the bar.");
            }

            if (this.barWindow != null)
            {
                this.barWindow.Close();
                this.barWindow = null;
            }

            if (bar != null)
            {
                this.barWindow = new PrimaryBarWindow(bar);
                this.barWindow.Show();
                bar.ReloadRequired += this.OnBarOnReloadRequired;
            }
        }

        private void OnBarOnReloadRequired(object? sender, EventArgs args)
        {
            BarData? bar = sender as BarData;
            if (bar != null)
            {
                string source = bar.Source;
                if (this.barWindow != null)
                {
                    this.barWindow.IsClosing = true;
                    this.barWindow.Close();
                    this.barWindow = null;
                }

                bar.Dispose();
                this.LoadBar(source);
            }
        }

        // The mouse is over any window in mouseOverWindows
        private bool mouseOver = false;

        // The windows where the mouse-over status is needed.
        private readonly List<Window> mouseOverWindows = new List<Window>();
        private DispatcherTimer? mouseTimer;

        /// <summary>
        /// Register interest in observing the mouse-over state of a window.
        /// </summary>
        /// <param name="window"></param>
        public void AddMouseOverWindow(Window window)
        {
            this.mouseOverWindows.Add(window);
            window.MouseEnter += this.CheckMouseOver;
            window.MouseLeave += this.CheckMouseOver;
        }

        private void CheckMouseOver(object? sender, EventArgs e)
        {
            if (this.mouseOverWindows.Count == 0)
            {
                return;
            }

            bool isOver = false;
            IEnumerable<Window> windows = this.mouseOverWindows.Where(w => w.IsVisible && w.Opacity > 0);

            Point? cursor = null;

            // Window.IsMouseOver is false if the mouse is over the window border, check if that's the case.
            foreach (Window window in windows)
            {
                if (window.IsMouseOver)
                {
                    isOver = true;
                    break;
                }

                cursor ??= PresentationSource.FromVisual(window)?.CompositionTarget.TransformFromDevice
                                             .Transform(WindowMovement.GetCursorPos());

                if (cursor != null)
                {
                    Rect rc = window.GetRect();
                    rc.Inflate(10, 10);
                    if (rc.Contains(cursor.Value))
                    {
                        isOver = true;
                        if (this.mouseTimer == null)
                        {
                            // Keep an eye on the current position.
                            this.mouseTimer = new DispatcherTimer(DispatcherPriority.Input)
                            {
                                Interval = TimeSpan.FromMilliseconds(100),
                            };
                            this.mouseTimer.Tick += this.CheckMouseOver;
                            this.mouseTimer.Start();
                        }

                        break;
                    }
                }
            }

            if (!isOver)
            {
                this.mouseTimer?.Stop();
                this.mouseTimer = null;
            }

            if (this.mouseOver != isOver)
            {
                this.mouseOver = isOver;
                if (isOver)
                {
                    this.MouseEnter?.Invoke(sender, new EventArgs());
                }
                else
                {
                    this.MouseLeave?.Invoke(sender, new EventArgs());
                }
            }
        }

    }
}