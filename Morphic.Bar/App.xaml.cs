using System.Windows;

namespace Morphic.Bar
{
    using System.IO;
    using System.Reflection;
    using Bar;
    using UI;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private BarWindow? barWindow;

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


    }
}