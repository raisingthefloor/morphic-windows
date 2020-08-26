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
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Extensions.Logging;

    public class AppPaths
    {
        public static string AppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";

        public static string ConfigDir = Environment.GetEnvironmentVariable("MORPHIC_CONFIGDIR") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MorphicCommunity");

        public static string DefaultConfigDir = Path.Combine(AppPaths.AppDir, "DefaultConfig");
        public static string AssetsDir = Path.Combine(AppPaths.AppDir, "Assets");
        public static string CacheDir = Path.Combine(AppPaths.ConfigDir, "cache");

        public static void CreateAll()
        {
            Directory.CreateDirectory(AppPaths.ConfigDir);
            Directory.CreateDirectory(AppPaths.CacheDir);
        }

        public static void Log(ILogger logger)
        {
            foreach (FieldInfo fieldInfo in typeof(AppPaths).GetFields(BindingFlags.Static | BindingFlags.Public))
            {
                logger.LogInformation("Path for {pathName}: {path}", fieldInfo.Name, fieldInfo.GetValue(null));
            }
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

        /// <summary>
        /// Gets a path to a file in the assets directory.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static string GetAssetFile(string filename)
        {
            return Path.GetFullPath(filename, AppPaths.AssetsDir);
        }

        /// <summary>
        /// Gets a cache file. The filename is generated based on the source of the file (like the URL), so the same
        /// file is returned for subsequent requests.
        /// </summary>
        /// <param name="sourceName">The original source of the file.</param>
        /// <param name="extension">An extension to add onto the result</param>
        /// <returns>A path to a file in the cache directory.</returns>
        public static string GetCacheFile(string sourceName, string? extension = null)
        {
            // URLs and file paths can't be expressed in a filename, so use a hash instead.
            using MD5 md5 = MD5.Create();
            string filename = new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(sourceName))).ToString();
            
            extension = extension?.TrimStart('.');
            if (!string.IsNullOrEmpty(extension))
            {
                filename += "." + extension;
            }
            return Path.GetFullPath(filename, AppPaths.CacheDir);
        }

        /// <summary><see cref="GetCacheFile(string,string?)"/></summary>
        /// <param name="sourceName">The original source of the file.</param>
        /// <param name="extension">An extension to add onto the result</param>
        /// <param name="exists">true if the file exists.</param>
        /// <returns>A path to a file in the cache directory.</returns>
        public static string GetCacheFile(string sourceName, string? extension, out bool exists)
        {
            string path = AppPaths.GetCacheFile(sourceName, extension);
            exists = File.Exists(path);
            return path;
        }

        /// <summary><see cref="GetCacheFile(string,string?)"/></summary>
        /// <param name="source">The original source of the file.</param>
        /// <param name="exists">true if the file exists.</param>
        /// <returns>A path to a file in the cache directory.</returns>
        public static string GetCacheFile(Uri source, out bool exists)
        {
            return AppPaths.GetCacheFile(source.ToString(), "", out exists);
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
