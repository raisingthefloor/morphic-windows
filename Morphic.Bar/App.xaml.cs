using System.Windows;

namespace Morphic.Bar
{
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
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
    }
}