using Microsoft.Extensions.DependencyInjection;
using Morphic.Core;
using Morphic.Settings;
using Morphic.Settings.SettingsHandlers;
using Morphic.Settings.SolutionsRegistry;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Morphic.ManualTesterCLI
{
    public class RegistryManager
    {
        private ServiceProvider provider;
        private Solutions solutions;

        public RegistryManager()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IServiceProvider>(provider => provider);
            var serviceprovider = services.BuildServiceProvider();
        }

        public bool Load(string filepath)
        {
            solutions = Solutions.FromFile(provider, filepath);
            return solutions.All.Count != 0;
        }

        public void List()
        {
            foreach (var solution in solutions.All.Values)
            {
                Console.WriteLine(solution.SolutionId + ":");
                foreach (var setting in solution.AllSettings.Values)
                {
                    Console.WriteLine("\t" + setting.Name + " [" + setting.DataType.ToString() + "]");
                }
            }
        }

        public void ListSolutions()
        {
            foreach (var solution in solutions.All.Values)
            {
                Console.WriteLine(solution.SolutionId);
            }
        }

        public void ListSpecific(string solutionId)
        {
            foreach (var solution in solutions.All.Values)
            {
                if (solution.SolutionId == solutionId)
                {
                    Console.WriteLine(solution.SolutionId + ":");
                    foreach (var setting in solution.AllSettings.Values)
                    {
                        Console.WriteLine("\t" + setting.Name + " [" + setting.DataType.ToString() + "]");
                    }
                    return;
                }
            }
            Console.WriteLine("[ERROR]: Solution not found. Please provide list command with a solution in the registry or no parameter to list all settings.");
        }

        public void Info(string solution, string preference)
        {
            //not sure what to do with this command without debugdescription
            var setting = solutions.GetSolution(solution).GetSetting(preference);
            if (setting != null)
            {
                Console.WriteLine(setting.ToString());
            }
        }

#nullable enable
        public async Task Get(string? solution = null)
        {
            bool allSolutions = (solution == null);
            foreach (var sol in solutions.All.Values)
            {
                if (allSolutions || solution == sol.SolutionId)
                {
                    Console.WriteLine(sol.SolutionId + ":");
                    foreach (var setting in sol.AllSettings.Values)
                    {
                        Console.Write("\t" + setting.Name);
                        await Get(sol.SolutionId, setting.Name);
                    }
                }
            }
        }
#nullable disable

        public async Task Get(string solution, string preference)
        {
            var value = await solutions.GetSolution(solution).GetSetting(preference).GetValue();
            if (value == null) return;
            var type = solutions.GetSolution(solution).GetSetting(preference).DataType;
            Console.WriteLine("[" + type.ToString() + "] value: " + value);
        }

        public async Task Set(string solution, string preference, string value)
        {
            var success = await solutions.GetSolution(solution).GetSetting(preference).SetValue(value);
            if (success)
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
