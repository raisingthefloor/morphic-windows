// AppPaths.cs
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar
{
    using System;
    using System.IO;
    using System.Reflection;

    public class AppPaths
    {
        public static string AppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";

        public static string ConfigDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Morphic.Bar");

        public static string DefaultConfigDir = Path.Combine(AppPaths.AppDir, "DefaultConfig");
        public static string CacheDir = Path.Combine(AppPaths.ConfigDir, "cache");

        public static void CreateAll()
        {
            Directory.CreateDirectory(AppPaths.ConfigDir);
            Directory.CreateDirectory(AppPaths.CacheDir);
        }
        
        /// <summary>
        /// Gets a path to a file that is relative to the executable.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static string GetAppFile(string filename)
        {
            return Path.GetFullPath(filename, AppPaths.AppDir);
        }

        public static string GetCacheFile(string filename)
        {
            return Path.GetFullPath(filename, AppPaths.AppDir);
        }

        /// <summary>
        /// Gets the path to a file in the configuration directory.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="useDefault">true to return a path in the app directory, if there isn't one in the config directory.</param>
        /// <param name="copyDefault">true to copy the default file to the config directory, if the user-specific one doesn't exist.</param>
        /// <returns></returns>
        public static string GetConfigFile(string filename, bool useDefault = false, bool copyDefault = false)
        {
            string path = Path.GetFullPath(filename, AppPaths.ConfigDir);
            if (useDefault || copyDefault)
            {
                if (!File.Exists(path))
                {
                    string defaultPath = Path.GetFullPath(filename, AppPaths.DefaultConfigDir);
                    if (File.Exists(defaultPath))
                    {
                        if (copyDefault)
                        {
                            File.Copy(defaultPath, path);
                        }
                        else
                        {
                            path = defaultPath;
                        }
                    }
                }
            }

            return path;
        }
    }
}
