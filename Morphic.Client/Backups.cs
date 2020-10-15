namespace Morphic.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Core;
    using Microsoft.Extensions.Logging;
    using Service;
    using Settings;
    using Path = System.IO.Path;

    public class Backups
    {
        private readonly Session session;
        private readonly ILogger<Backups> logger;
        private readonly IServiceProvider serviceProvider;

        public static string BackupDirectory => AppPaths.GetConfigDir("backups");
        private static readonly string BackupExtension = ".preferences";

        public Backups(Session session, ILogger<Backups> logger, IServiceProvider serviceProvider)
        {
            this.session = session;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
        }


        /// <summary>
        /// Stores some preferences to a file, for a backup.
        /// </summary>
        /// <param name="description">Short description for display (one or two words, file-safe characters)</param>
        /// <param name="preferences">The preferences to store - null to capture them.</param>
        public async Task Store(Preferences? preferences = null)
        {
            this.logger.LogInformation("Making backup");
            if (preferences == null)
            {
                preferences = new Preferences();
                CaptureSession captureSession = new CaptureSession(App.Current.Session.SettingsManager, preferences);
                captureSession.AddAllSolutions();
                await captureSession.Run();
            }

            string json = JsonSerializer.Serialize(preferences);
            string filename = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss") + BackupExtension;
            string path = Path.Combine(BackupDirectory, filename);

            Directory.CreateDirectory(BackupDirectory);
            await File.WriteAllTextAsync(path, json);

            this.logger.LogInformation($"Stored backup to {path}");
        }

        /// <summary>
        /// Gets the list of backup files.
        /// </summary>
        /// <returns>filename:date</returns>
        public IDictionary<string, string> GetBackups()
        {
            Dictionary<string, string> backups = new Dictionary<string, string>();

            if (Directory.Exists(BackupDirectory))
            {
                foreach (string path in Directory.EnumerateFiles(BackupDirectory, "*" + BackupExtension)
                    .OrderBy(f => f))
                {
                    // Get the date from the filename.
                    string dateString = Path.ChangeExtension(Path.GetFileName(path), null);
                    if (DateTime.TryParse(dateString.Replace('_', ' ').Replace('.', ':'), out DateTime date))
                    {
                        backups.Add(path, date.ToString("g"));
                    }
                }
            }

            return backups;
        }

        /// <summary>
        /// Applies a back-up.
        /// </summary>
        /// <param name="path">The backup file.</param>
        public async Task Apply(string path)
        {
            string json = await File.ReadAllTextAsync(path);
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            this.session.Preferences = JsonSerializer.Deserialize<Preferences>(json, options);
            await session.ApplyAllPreferences();
        }
    }
}
