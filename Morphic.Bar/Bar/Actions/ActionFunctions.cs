// ActionFunctions.cs: Handles the internal functions for bar items.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.ActionFunctions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Extensions.Logging;

    [ActionFunction]
    public class Functions
    {
        [ActionFunction("screenshot")]
        public static Task<bool> Screenshot(ActionArgs args)
        {
            MessageBox.Show("screen shot");
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Handles the invocation of internal functions, used by the BarInternalAction class.
    ///
    /// The functions are public static methods decorated with [ActionFunction("fname")], in any class in this
    /// assembly (which also has the ActionFunction attribute).
    /// </summary>
    public class ActionFunctions
    {
        /// <summary>Default singleton instance.</summary>
        public static ActionFunctions Default = new ActionFunctions();

        /// <summary>All action functions.</summary>
        private readonly Dictionary<string, ActionFunction> all;

        public delegate Task<bool> ActionFunction(ActionArgs args);

        protected ActionFunctions()
        {
            this.all = ActionFunctions.FindAllFunctions();
        }

        /// <summary>
        /// Gets the methods that handle the built-in functions.
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, ActionFunction> FindAllFunctions()
        {
            // Get all public static methods in all public classes in this assembly, which both have the ActionFunction
            // attribute
            IEnumerable<MethodInfo> methods = typeof(ActionFunctions).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic && t.GetCustomAttributes<ActionFunctionAttribute>().Any())
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static));

            Dictionary<string, ActionFunction> functions = new Dictionary<string, ActionFunction>();

            // Add the methods decorated with [ActionFunction]
            foreach (MethodInfo method in methods)
            {
                ActionFunctionAttribute? attr = method.GetCustomAttribute<ActionFunctionAttribute>();
                if (attr?.FunctionName != null)
                {
                    functions.Add(attr.FunctionName,
                        (ActionFunction)method.CreateDelegate(typeof(ActionFunction)));
                }
            }

            return functions;
        }

        /// <summary>
        /// Invokes a built-in function.
        /// </summary>
        /// <param name="functionName">The function name.</param>
        /// <param name="functionArgs">The parameters</param>
        /// <returns></returns>
        public Task<bool> InvokeFunction(string functionName, string[] functionArgs)
        {
            App.Current.Logger.LogDebug($"Invoking built-in function '{functionName}'");

            Task<bool> result;
            if (this.all.TryGetValue(functionName, out ActionFunction? actionFunction))
            {
                ActionArgs args = new ActionArgs(functionName, functionArgs);
                result = actionFunction(args);
            }
            else
            {
                App.Current.Logger.LogWarning($"No function found for '{functionName}");
                result = Task.FromResult(false);
            }

            return result;
        }
    }

    /// <summary>
    /// Marks a method (or a class containing such methods) that's a built-in function for bar actions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ActionFunctionAttribute : Attribute
    {
        public string? FunctionName { get; set; }

        public ActionFunctionAttribute(string? functionName = null)
        {
            this.FunctionName = functionName;
        }
    }

    public class ActionArgs
    {
        public ActionArgs(string functionName, string[] functionArgs)
        {
            this.FunctionName = functionName;
            this.FunctionArgs = functionArgs;
        }

        public string FunctionName { get; }
        public string[] FunctionArgs { get; }
    }

}
