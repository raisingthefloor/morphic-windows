using Microsoft.Extensions.DependencyInjection;
using Morphic.Core;
using Morphic.Settings;
using System;
using System.Threading.Tasks;

namespace Morphic.ManualTesterCLI
{
    public class RegistryManager
    {
        private SettingsManager manager;

        public RegistryManager()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            services.AddSingleton<SettingsManager>();
            var serviceprovider = services.BuildServiceProvider();
            manager = serviceprovider.GetRequiredService<SettingsManager>();
        }

        public async Task<bool> Load(string filepath)
        {
            await manager.Populate(filepath);
            return manager.SolutionsById.Count != 0;
        }

        public void List()
        {
            foreach(var solution in manager.SolutionsById.Values)
            {
                Console.WriteLine(solution.Id + ":");
                foreach(var setting in solution.SettingsByName.Values)
                {
                    Console.WriteLine("\t" + setting.Name + " [" + setting.Kind.ToString() + "]");
                }
            }
        }

        public void ListSolutions()
        {
            foreach(var solution in manager.SolutionsById.Values)
            {
                Console.WriteLine(solution.Id);
            }
        }

        public void ListSpecific(string solution)
        {
            foreach(var sol in manager.SolutionsById.Values)
            {
                if(sol.Id == solution)
                {
                    Console.WriteLine(sol.Id + ":");
                    foreach(var setting in sol.SettingsByName.Values)
                    {
                        Console.WriteLine("\t" + setting.Name + " [" + setting.Kind.ToString() + "]");
                    }
                    return;
                }
            }
            Console.WriteLine("[ERROR]: Solution not found. Please provide list command with a solution in the registry or no parameter to list all settings.");
        }

        public void Info(string solution, string preference)
        {
            //not sure what to do with this command without debugdescription
            var setting = manager.Get(new Preferences.Key(solution, preference));
            if(setting != null)
            {
                Console.WriteLine(setting.ToString());
            }
        }

#nullable enable
        public async Task Get(string? solution = null)
        {
            bool allSolutions = (solution == null);
            foreach(var sol in manager.SolutionsById.Values)
            {
                if(allSolutions || solution == sol.Id)
                {
                    Console.WriteLine(sol.Id + ":");
                    foreach(var setting in sol.SettingsByName.Values)
                    {
                        Console.Write("\t" + setting.Name);
                        await Get(sol.Id, setting.Name);
                    }
                }
            }
        }
#nullable disable

        public async Task Get(string solution, string preference)
        {
            var value = await manager.Capture(new Preferences.Key(solution, preference));
            if (value == null) return;
            var type = manager.Get(new Preferences.Key(solution, preference)).Kind;
            Console.WriteLine("[" + type.ToString() + "] value: " + value);
        }

        public async Task Set(string solution, string preference, string value)
        {
            var success = await manager.Apply(new Preferences.Key(solution, preference), value);
            if(success)
            {
                Console.WriteLine("Value applied successfully!");
            }
            else
            {
                Console.WriteLine("Value application failed.");
            }
        }
    }
}
