using System.Windows;

namespace Morphic.Bar
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Navigation;
    using System.Windows.Threading;
    using Bar;
    using UI;
    using UI.AppBarWindow;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;

        private BarWindow? barWindow;

        /// <summary>
        /// true if the current application is active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>The mouse has entered any window belonging to the application.</summary>
        public event EventHandler? MouseEnter;
        /// <summary>The mouse has left any window belonging to the application.</summary>
        public event EventHandler? MouseLeave;

        public App()
        {
            this.Activated += (sender, args) => this.IsActive = true;
            this.Deactivated += (sender, args) => this.IsActive = false;
        }

        /// <summary>
        /// Gets a path to a file that is relative to the executable.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static string GetFile(string filename)
        {
            return Path.GetFullPath(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), filename));
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.LoadBar(App.GetFile("test-bar.json5"));
        }

        public void LoadBar(string path)
        {
            BarData? bar = BarData.FromFile(path);
            if (this.barWindow != null)
            {
                this.barWindow.Close();
                this.barWindow = null;
            }

            this.barWindow = new PrimaryBarWindow(bar);
            this.barWindow.Show();
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