using Morphic.Settings;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Morphic.ManualTester
{
    /// <summary>
    /// Interaction logic for SolutionHeader.xaml
    /// </summary>
    public partial class SolutionHeader : UserControl
    {
        public IServiceProvider ServiceProvider { get; private set; } = null!;
        public Solution solution;
        public Boolean expanded = false;
        public SolutionHeader(IServiceProvider sp, Solution solution)
        {
            InitializeComponent();
            ServiceProvider = sp;
            this.solution = solution;
            SolutionName.Text = solution.Id;
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            if(expanded)
            {
                ControlStack.Children.Clear();
                ExpandButton.Content = "Expand";
            }
            else
            {
                foreach(var setting in solution.Settings)
                {
                    switch (setting.Kind)
                    {
                        case Setting.ValueKind.Boolean:
                            ControlStack.Children.Add(new ManualControlBoolean(ServiceProvider, solution.Id, setting));
                            break;
                        case Setting.ValueKind.Double:
                            ControlStack.Children.Add(new ManualControlDouble(ServiceProvider, solution.Id, setting));
                            break;
                        case Setting.ValueKind.Integer:
                            ControlStack.Children.Add(new ManualControlInteger(ServiceProvider, solution.Id, setting));
                            break;
                        case Setting.ValueKind.String:
                            ControlStack.Children.Add(new ManualControlString(ServiceProvider, solution.Id, setting));
                            break;
                    }
                }
                ExpandButton.Content = "Collapse";
            }
            expanded = !expanded;
        }
    }
}
