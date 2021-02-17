﻿using Microsoft.Extensions.DependencyInjection;
using Morphic.Settings.SolutionsRegistry;
using System;
using System.IO;
using System.Reflection;
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
            services.AddSolutionsRegistryServices();
            provider = services.BuildServiceProvider();
        }

        public bool Load(string filepath)
        {
            string AppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            solutions = Solutions.FromFile(provider, Path.GetFullPath(filepath, AppDir));
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
                        Console.Write("\t" + setting.Id);
                        await Get(sol.SolutionId, setting.Id);
                    }
                }
            }
        }

        public async Task Get(string solution, string preference)
        {
            try
            {
                var setting = solutions.GetSetting(solution, preference);
                var type = setting.DataType;
                if(type == Settings.SettingsHandlers.SettingType.Unknown)
                {
                    Console.WriteLine("[UNKNOWN DATA TYPE]");
                    return;
                }
                object? value = await setting.GetValue();
                if (value == null)
                {
                    Console.WriteLine("[NO DATA RETURNED]");
                    return;
                }
                Console.WriteLine("[" + type.ToString() + "] value: " + value.ToString());
            }
            catch
            {
                Console.WriteLine("[DATA READ FAILURE]");
            }
        }
#nullable disable

        public async Task Set(string solution, string preference, string value)
        {
            try
            {
                var setting = solutions.GetSetting(solution, preference);
                bool success = false;
                switch(setting.DataType)
                {
                    case Settings.SettingsHandlers.SettingType.Bool:
                        if (value.ToLower() == "0" || value.ToLower() == "false") success = await setting.SetValue(false);
                        else if (value.ToLower() == "1" || value.ToLower() == "true") success = await setting.SetValue(true);
                        break;
                    case Settings.SettingsHandlers.SettingType.Int:
                        success = await setting.SetValue(int.Parse(value));
                        break;
                    case Settings.SettingsHandlers.SettingType.Real:
                        success = await setting.SetValue(double.Parse(value));
                        break;
                    case Settings.SettingsHandlers.SettingType.String:
                        success = await setting.SetValue(value);
                        break;
                }
                if (success)
                {
                    Console.WriteLine("Value applied successfully!");
                }
                else
                {
                    Console.WriteLine("[ERROR]: Value application failed.");
                }
            }
            catch
            {
                Console.WriteLine("[ERROR]: Value application encountered an error.");
            }
        }
    }
}
