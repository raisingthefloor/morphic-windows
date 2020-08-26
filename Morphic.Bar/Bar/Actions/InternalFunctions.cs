// InternalFunctions.cs: Handles the internal functions for bar items.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Bar.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Windows.Controls;
    using System.Windows.Forms;
    using Microsoft.Extensions.Logging;

    [HasInternalFunctions]
    public class Functions
    {
        [InternalFunction("screenshot")]
        public static Task<bool> Screenshot(FunctionArgs args)
        {
            MessageBox.Show("screen shot");
            return Task.FromResult(true);
        }

        [InternalFunction("menu", "key=Morphic")]
        public static Task<bool> ShowMenu(FunctionArgs args)
        {
            string menuKey = args["key"] + "Menu";
            if (App.Current.Resources[menuKey] is ContextMenu menu)
            {
                menu.IsOpen = true;
            }

            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Handles the invocation of internal functions, used by the InternalAction class.
    ///
    /// The functions are public static methods decorated with [InternalFunction("fname")], in any class in this
    /// assembly (which also has the HasInternalFunctions attribute).
    /// </summary>
    public class InternalFunctions
    {
        /// <summary>Default singleton instance.</summary>
        public static InternalFunctions Default = new InternalFunctions();

        /// <summary>All internal functions.</summary>
        private readonly Dictionary<string, InternalFunctionAttribute> all;

        public delegate Task<bool> InternalFunction(FunctionArgs args);

        protected InternalFunctions()
        {
            this.all = InternalFunctions.FindAllFunctions()
                .ToDictionary(attr => attr.FunctionName.ToLowerInvariant(), attr => attr);
        }

        /// <summary>
        /// Gets the methods that handle the built-in functions.
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<InternalFunctionAttribute> FindAllFunctions()
        {
            // Get all public static methods in all public classes in this assembly, which both have the InternalFunction
            // attribute
            IEnumerable<MethodInfo> methods = typeof(InternalFunctions).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic && t.GetCustomAttributes<HasInternalFunctionsAttribute>().Any())
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static));

            // Add the methods decorated with [InternalFunction]
            foreach (MethodInfo method in methods)
            {
                InternalFunctionAttribute? attr = method.GetCustomAttribute<InternalFunctionAttribute>();
                if (attr != null)
                {
                    attr.SetFunction((InternalFunction)method.CreateDelegate(typeof(InternalFunction)));
                    yield return attr;
                }
            }
        }

        /// <summary>
        /// Invokes a built-in function.
        /// </summary>
        /// <param name="functionName">The function name.</param>
        /// <param name="functionArgs">The parameters.</param>
        /// <returns></returns>
        public Task<bool> InvokeFunction(string functionName, Dictionary<string, string> functionArgs)
        {
            App.Current.Logger.LogDebug($"Invoking built-in function '{functionName}'");

            Task<bool> result;

            if (this.all.TryGetValue(functionName.ToLowerInvariant(),
                out InternalFunctionAttribute? functionAttribute))
            {
                FunctionArgs args = new FunctionArgs(functionAttribute, functionArgs);
                result = functionAttribute.Function(args);
            }
            else
            {
                throw new ActionException($"No internal function found for '{functionName}");
            }

            return result;
        }
    }

    /// <summary>
    /// Marks a method (or a class containing such methods) that's a built-in function for bar actions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class InternalFunctionAttribute : Attribute
    {
        public string FunctionName { get; }
        public string[] RequiredArguments { get; }
        public InternalFunctions.InternalFunction Function { get; private set; } = null!;

        /// <summary>
        /// Defines an internal function for the bar.
        /// </summary>
        /// <param name="functionName">Name of the function.</param>.
        /// <param name="requiredArgs">
        /// Name of each required argument, if any. For optional parameters, use "name=default".
        /// </param>
        public InternalFunctionAttribute(string functionName, params string[] requiredArgs)
        {
            this.RequiredArguments = requiredArgs;
            this.FunctionName = functionName;
        }

        public void SetFunction(InternalFunctions.InternalFunction internalFunction)
        {
            this.Function = internalFunction;
        }

        /// <summary>
        /// Checks a given arguments dictionary for require values, and adding the value for those that are missing.
        /// </summary>
        /// <param name="arguments">The arguments (gets modified).</param>
        /// <exception cref="ActionException"></exception>
        public void CheckRequiredArguments(Dictionary<string, string> arguments)
        {
            foreach (string required in this.RequiredArguments)
            {
                string[] split = required.Split('=', 2);
                string name = split[0];

                if (!arguments.ContainsKey(name))
                {
                    string? defaultValue = split.Length > 1 ? split[1] : null;
                    if (defaultValue == null)
                    {
                        throw new ActionException(
                            $"Internal function {this.FunctionName} invoked without parameter {name}");
                    }

                    arguments.Add(name, defaultValue);
                }
            }
        }
    }

    /// <summary>
    /// Identifies a class having internal functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HasInternalFunctionsAttribute : Attribute
    {
    }

    public class FunctionArgs
    {
        public string FunctionName { get; }
        public Dictionary<string, string> Arguments { get; }

        /// <summary>
        /// Gets an argument value by its name, or an empty string if there's no such argument.
        /// </summary>
        /// <param name="argumentName"></param>
        public string this[string argumentName] => this.Arguments.TryGetValue(argumentName, out string? value)
                ? value
                : string.Empty;

        /// <summary>
        /// Creates arguments for a function.
        /// </summary>
        /// <param name="functionAttribute">The function attribute of the method that handles the internal function.</param>
        /// <param name="args">The arguments.</param>
        public FunctionArgs(InternalFunctionAttribute functionAttribute, Dictionary<string, string> args)
        {
            this.FunctionName = functionAttribute.FunctionName;
            this.Arguments = args.ToDictionary(kv => kv.Key, kv => kv.Value);

            functionAttribute.CheckRequiredArguments(this.Arguments);
        }

    }

}
