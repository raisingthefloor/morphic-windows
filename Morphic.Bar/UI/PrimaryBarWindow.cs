namespace Morphic.Bar.UI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AppBarWindow;
    using Bar;

    public sealed class PrimaryBarWindow : BarWindow
    {
        private SecondaryBarWindow? secondaryWindow;
        private ExpanderWindow? expanderWindow;

        public event EventHandler? ExpandedChange;

        public override bool IsExpanded
        {
            get => base.IsExpanded;
            set
            {
                base.IsExpanded = value;
                this.OnExpandedChange();
            }
        }

        public PrimaryBarWindow(BarData barData) : base(barData)
        {
#if TESTING
            // Accept bar files to be dropped.
            this.AllowDrop = true;
            this.Drop += (sender, args) =>
            {
                if (args.Data.GetDataPresent(DataFormats.FileDrop) &&
                    args.Data.GetData(DataFormats.FileDrop) is string[] files)
                {
                    string file = files.FirstOrDefault();
                    this.SetBarSource(file);
                }
            };
#endif
            this.BarChanged += this.OnBarChanged;
            this.Bar = barData;
        }

        private void OnBarChanged(object? sender, EventArgs args)
        {
            if (this.Bar.SecondaryItems.Any())
            {
                if (this.secondaryWindow == null)
                {
                    this.secondaryWindow = new SecondaryBarWindow(this, this.Bar);
                    this.expanderWindow = new ExpanderWindow(this, this.secondaryWindow);

                    this.Loaded += (s, a) => this.secondaryWindow.Show();
                    this.secondaryWindow.Loaded += (s, a) => this.expanderWindow.Show();

                    this.expanderWindow.Changed += (s, a) => this.IsExpanded = this.expanderWindow.IsExpanded;
                }

                this.secondaryWindow.OnBarChanged();
            }
            else
            {
                this.secondaryWindow?.Close();
                this.expanderWindow?.Close();
                this.expanderWindow = null;
                this.secondaryWindow = null;
            }
        }

        protected override void SetInitialPosition(Size size)
        {
            this.AppBar.ApplyAppBar(this.Bar.Position.DockEdge);
            if (this.Bar.Position.DockEdge == Edge.None)
            {
                Rect workArea = SystemParameters.WorkArea;
                Point pos = this.Bar.Position.Primary.GetPosition(workArea, size);
                this.Left = pos.X;
                this.Top = pos.Y;
            }
        }

        private void ToggleSecondaryBar(bool open)
        {
            if (this.secondaryWindow != null)
            {
                this.OnExpandedChange();

            }
        }

#if TESTING
        private string barFile = string.Empty;

        /// <summary>
        /// Set the source of the json data, and loads it.
        /// </summary>
        /// <param name="path"></param>
        private async void SetBarSource(string path)
        {
            try
            {
                this.Bar = BarData.FromFile(path)!;
                this.barFile = path;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.ToString());

                this.BarControl.RemoveItems();
                this.BarControl.AddItem(new BarButton()
                {
                    Theme = new BarItemTheme()
                    {
                        TextColor = Colors.DarkRed,
                        Background = Colors.White
                    },
                    Text = e.Message,
                    ToolTip = e.Message,
                    ToolTipInfo = e.ToString()
                });
            }

            // Monitor the file for changes (not using FileSystemWatcher because it doesn't work on network mounts)
            FileInfo lastInfo = new FileInfo(path);
            while (this.barFile == path)
            {
                await Task.Delay(500);
                FileInfo info = new FileInfo(path);
                bool changed = info.Length != lastInfo.Length ||
                               info.CreationTime != lastInfo.CreationTime ||
                               info.LastWriteTime != lastInfo.LastWriteTime;
                if (changed)
                {
                    this.SetBarSource(path);
                    break;
                }
            }
        }
#endif

        private void OnExpandedChange()
        {
            if (this.IsExpanded)
            {
                this.secondaryWindow?.Show();
            }
            else
            {
                this.secondaryWindow?.Hide();
            }
            
            this.ExpandedChange?.Invoke(this, EventArgs.Empty);
        }
    }
}
