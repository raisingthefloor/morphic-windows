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
        public SettingsManager manager;
        public Solution solution;
        private MainWindow window;
        public SolutionHeader(MainWindow window, SettingsManager manager, Solution solution)
        {
            InitializeComponent();
            this.window = window;
            this.manager = manager;
            this.solution = solution;
            ControlStack.Header = solution.Id;
            foreach (var setting in solution.Settings)
            {
                switch (setting.Kind)
                {
                    case Setting.ValueKind.Boolean:
                        ControlStack.Items.Add(new ManualControlBoolean(window, manager, solution.Id, setting));
                        break;
                    case Setting.ValueKind.Double:
                        ControlStack.Items.Add(new ManualControlDouble(window, manager, solution.Id, setting));
                        break;
                    case Setting.ValueKind.Integer:
                        ControlStack.Items.Add(new ManualControlInteger(window, manager, solution.Id, setting));
                        break;
                    case Setting.ValueKind.String:
                        ControlStack.Items.Add(new ManualControlString(window, manager, solution.Id, setting));
                        break;
                }
            }
        }

        public void ApplyAllSettings()
        {
            foreach(var element in ControlStack.Items)
            {
                try
                {
                    var ctrl = (ManualControlBoolean?)element;
                    if (ctrl != null) ctrl.ApplySetting();
                }
                catch { }
                try
                {
                    var ctrl = (ManualControlInteger?)element;
                    if (ctrl != null) ctrl.ApplySetting();
                }
                catch { }
                try
                {
                    var ctrl = (ManualControlDouble?)element;
                    if (ctrl != null) ctrl.ApplySetting();
                }
                catch { }
                try
                {
                    var ctrl = (ManualControlString?)element;
                    if (ctrl != null) ctrl.ApplySetting();
                }
                catch { }
            }
        }
    }
}
