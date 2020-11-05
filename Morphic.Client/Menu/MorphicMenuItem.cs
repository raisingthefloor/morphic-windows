namespace Morphic.Client.Menu
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using Config;

    public class MorphicMenuItem : MenuItem
    {
        /// <summary>Shell action to open when the item is clicked.</summary>
        public string Open { get; set; }

        public Type? Dialog { get; set; }

        /// <summary>Show the item only if one or more of these features are enabled.</summary>
        public Features Features
        {
            set
            {
                if (!value.AnyEnabled())
                {
                    this.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>Show the item only if all of these features are enabled.</summary>
        public Features AllFeatures
        {
            set
            {
                if (!value.IsEnabled())
                {
                    this.Visibility = Visibility.Collapsed;
                }
            }
        }

        public MorphicMenuItem()
        {
            this.Click += this.OnClick;
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is MorphicMenuItem item)
            {
                if (!string.IsNullOrEmpty(item.Open))
                {
                    Process.Start(new ProcessStartInfo(this.Open)
                    {
                        UseShellExecute = true
                    });
                }

                if (this.Dialog != null)
                {
                    App.Current.Dialogs.OpenDialog(this.Dialog!);
                }

            }
        }
    }

    /// <summary>
    /// A menu header item - a separator with text.
    /// </summary>
    public class MorphicMenuHeader : Separator
    {
        public string? Header { get; set; } = null;

        public override void EndInit()
        {
            base.EndInit();

            // Add the header label
            ControlTemplate template = new ControlTemplate(this.GetType());
            FrameworkElementFactory factory = new FrameworkElementFactory(typeof(Label));
            factory.SetValue(ContentControl.ContentProperty, this.Header);
            factory.SetValue(Label.FontWeightProperty, FontWeights.Bold);
            factory.SetValue(Label.ForegroundProperty, SystemColors.MenuTextBrush);
            factory.SetValue(Label.BackgroundProperty, SystemColors.MenuBarBrush);
            template.VisualTree = factory;
            this.Template = template;
        }
    }

}
