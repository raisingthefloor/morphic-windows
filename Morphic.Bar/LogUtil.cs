// LogUtil.cs: Logging
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
    using System.Diagnostics;
    using System.Reflection;
    using Microsoft.Extensions.Logging;
    using Logging = Microsoft.Extensions.Logging;

    public class LogUtil
    {
        public static ILoggerFactory LoggerFactory { get; }

        static LogUtil()
        {
            LoggerFactory = Logging.LoggerFactory.Create(builder =>
            {
                string logFile = Environment.GetEnvironmentVariable("MORPHIC_LOGFILE")
                    ?? AppPaths.GetConfigFile("morphic.bar.log");
                builder.AddFile(logFile, (config) =>
                {
                    config.Append = true;
                    config.MaxRollingFiles = 3;
                    config.FileSizeLimitBytes = 1000000;
                });

                string debugEnv = Environment.GetEnvironmentVariable("MORPHIC_LOGLEVEL") ?? string.Empty;
                if (Enum.TryParse(debugEnv, out LogLevel level))
                {
                    builder.SetMinimumLevel(level);
                    if (level <= LogLevel.Debug)
                    {
                        builder.AddConsole();
                    }
                }
            });
        }

        public static ILogger Init()
        {
            ILogger logger = LogUtil.LoggerFactory.CreateLogger("General");
            logger.LogInformation("Started {Version}",
                FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion);

            System.AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                logger.LogCritical(args.ExceptionObject as Exception, "Unhandled exception");
            
            return logger;
        }
    }
}
