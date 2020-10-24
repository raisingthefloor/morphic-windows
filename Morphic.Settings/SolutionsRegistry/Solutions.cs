namespace Morphic.Settings.SolutionsRegistry
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;
    using Core;
    using Newtonsoft.Json;
    using SettingsHandlers;

    [JsonObject(MemberSerialization.OptIn)]
    public class Solutions
    {
        public IServiceProvider ServiceProvider { get; }

        public Dictionary<string, Solution> All { get; private set; } = new Dictionary<string, Solution>();

        private Solutions(IServiceProvider serviceProvider)
        {
            this.ServiceProvider = serviceProvider;
        }

        private static readonly Dictionary<Type, Type> Handlers = new Dictionary<Type, Type>();
        public static Type GetSettingHandlerType(Type settingType)
        {
            return Handlers[settingType];
        }

        public static void AddSettingsHandler(Type settingsHandlerType, Type settingType)
        {
            Handlers.Add(settingType, settingsHandlerType);
        }

        /// <summary>Loads a solutions file.</summary>
        public static Solutions FromFile(IServiceProvider serviceProvider, string path)
        {
            JsonSerializerSettings? settings = new JsonSerializerSettings()
            {
                Error = (sender, args) =>
                {
                    Console.WriteLine(args.ErrorContext.Path);
                    Console.WriteLine(args.ErrorContext.Error.ToString());
                },
                SerializationBinder = new TypeResolver(),
                MetadataPropertyHandling = MetadataPropertyHandling.ReadAhead,
                ContractResolver = new SolutionsRegistryContractResolver(serviceProvider)
            };

            JsonSerializer serializer = JsonSerializer.Create(settings);
            SolutionsTextReader reader = new SolutionsTextReader(File.OpenText(path));

            Solutions solutions = new Solutions(serviceProvider);
            serializer.Populate(reader, solutions.All);

            foreach ((string solutionId, Solution solution) in solutions.All)
            {
                solution.Deserialized(serviceProvider, solutions, solutionId);
            }

            return solutions;
        }

        public Solution GetSolution(string solutionId)
        {
            if (this.All.TryGetValue(solutionId, out Solution? solution))
            {
                return solution;
            }

            throw new KeyNotFoundException($"Solution '{solutionId}' does not exist.");
        }

        public Setting GetSetting(string settingPath)
        {
            (string? solutionId, string? settingId) = ParseSettingPath(settingPath);
            return this.GetSolution(solutionId).GetSetting(settingId);
        }

        public Setting GetSetting(string solutionId, string settingId)
        {
            return this.GetSolution(solutionId).GetSetting(settingId);
        }

        public Setting GetSetting(SettingId settingId)
        {
            return this.GetSolution(settingId.Solution).GetSetting(settingId.Setting);
        }

        private static (string solutionId, string settingId) ParseSettingPath(string settingPath)
        {
            string[] parts = settingPath.Split('/', 2);
            if (parts.Length != 2)
            {
                throw new ArgumentException($"'{settingPath}' is not a valid setting path.", nameof(settingPath));
            }

            return (parts[0], parts[1]);
        }

        public async Task CapturePreferences(Preferences preferences, bool async = false)
        {
            List<Task> tasks = new List<Task>();
            preferences.Default ??= new Dictionary<string, SolutionPreferences>();
            foreach ((string? solutionId, Solution? solution) in this.All)
            {
                if (!preferences.Default.TryGetValue(solutionId, out SolutionPreferences? solutionPreferences))
                {
                    solutionPreferences = new SolutionPreferences();
                    preferences.Default.Add(solutionId, solutionPreferences);
                }

                Task capture = solution.Capture(solutionPreferences);
                if (async)
                {
                    tasks.Add(capture);
                }
                else
                {
                    await capture;
                }
            }

            if (async)
            {
                await Task.WhenAll(tasks);
            }
        }

        public async Task ApplyPreferences(Preferences preferences, bool captureCurrent = false, bool async = false)
        {
            if (preferences.Default == null)
            {
                return;
            }

            foreach ((string solutionId, SolutionPreferences solutionPreferences) in preferences.Default)
            {
                if (this.All.TryGetValue(solutionId, out Solution? solution))
                {
                    if (captureCurrent)
                    {
                        solutionPreferences.Previous ??= new Dictionary<string, object?>();
                    }

                    await solution.Apply(solutionPreferences);
                }
            }
        }

        public async Task RestorePreferences(Preferences preferences, bool async = false)
        {

        }
    }

    /// <summary>
    /// Something is wrong with the solutions registry.
    /// </summary>
    public class SolutionsRegistryException : Exception
    {
        public SolutionsRegistryException()
        {
        }

        public SolutionsRegistryException(string message) : base(message)
        {
        }

        public SolutionsRegistryException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
