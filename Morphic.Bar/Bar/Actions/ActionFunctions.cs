// ActionFunctions.cs: Handles the internal functions for bar items.
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

    [HasActionFunctions]
    public class Functions
    {
        [ActionFunction("screenshot")]
        public static Task<bool> Screenshot(ActionArgs args)
        {
            MessageBox.Show("screen shot");
            return Task.FromResult(true);
        }

        [ActionFunction("screen-zoom")]
        public static Task<bool> ScreenZoom(ActionArgs args)
        {
            MessageBox.Show("screen zoom");
            return Task.FromResult(true);
        }

        [ActionFunction("menu", "key=Morphic")]
        public static Task<bool> ShowMenu(ActionArgs args)
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
        private readonly Dictionary<string, ActionFunctionAttribute> all;

        public delegate Task<bool> ActionFunction(ActionArgs args);

        protected ActionFunctions()
        {
            this.all = ActionFunctions.FindAllFunctions()
                .ToDictionary(attr => attr.FunctionName.ToLowerInvariant(), attr => attr);
        }

        /// <summary>
        /// Gets the methods that handle the built-in functions.
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<ActionFunctionAttribute> FindAllFunctions()
        {
            // Get all public static methods in all public classes in this assembly, which both have the ActionFunction
            // attribute
            IEnumerable<MethodInfo> methods = typeof(ActionFunctions).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsPublic && t.GetCustomAttributes<HasActionFunctionsAttribute>().Any())
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static));

            // Add the methods decorated with [ActionFunction]
            foreach (MethodInfo method in methods)
            {
                ActionFunctionAttribute? attr = method.GetCustomAttribute<ActionFunctionAttribute>();
                if (attr != null)
                {
                    attr.SetFunction((ActionFunction)method.CreateDelegate(typeof(ActionFunction)));
                    yield return attr;
                }
            }
        }

        /// <summary>
        /// Invokes a built-in function.
        /// </summary>
        /// <param name="functionName">The function name.</param>
        /// <param name="functionArgs">The parameters.</param>
        /// <param name="source">The button id, for multi-button bar items.</param>
        /// <returns></returns>
        public Task<bool> InvokeFunction(string functionName, string[] functionArgs, string? source = null)
        {
            App.Current.Logger.LogDebug($"Invoking built-in function '{functionName}'");

            Task<bool> result;
            try
            {
                if (this.all.TryGetValue(functionName.ToLowerInvariant(),
                    out ActionFunctionAttribute? functionAttribute))
                {
                    try
                    {
                        ActionArgs args = new ActionArgs(functionAttribute, functionArgs);
                        args.Source = source;
                        result = functionAttribute.Function(args);
                    }
                    catch (Exception e) when (!(e is ActionException || e is OutOfMemoryException))
                    {
                        throw new ActionException(e.Message, e);
                    }
                }
                else
                {
                    throw new ActionException($"No internal function found for '{functionName}");
                }
            }
            catch (ActionException e)
            {
                App.Current.Logger.LogWarning(e,
                    $"ActionFunction error calling {functionName}({string.Join(", ", functionArgs)})");

                if (e.UserMessage != null)
                {
                    MessageBox.Show($"There was a problem performing the '{functionName}' action:\n\n{e.UserMessage}",
                        "Morphic Community Bar", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }

                result = Task.FromResult(false);
            }

            return result;
        }
    }

    /// <summary>
    /// Marks a method (or a class containing such methods) that's a built-in function for bar actions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ActionFunctionAttribute : Attribute
    {
        public string FunctionName { get; }
        public string[] ParameterNames { get; }
        public ActionFunctions.ActionFunction Function { get; private set; } = null!;

        /// <summary>
        /// Defines an internal function for the bar.
        /// </summary>
        /// <param name="functionName">Name of the function.</param>.
        /// <param name="paramNames">Name of each parameter, if any. For optional parameters, use "name=default".</param>
        public ActionFunctionAttribute(string functionName, params string[] paramNames)
        {
            this.ParameterNames = paramNames;
            this.FunctionName = functionName;
        }

        public void SetFunction(ActionFunctions.ActionFunction actionFunction)
        {
            this.Function = actionFunction;
        }

        /// <summary>
        /// Create a dictionary of the argument values with their names.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns></returns>
        /// <exception cref="ActionException"></exception>
        public Dictionary<string, string> GetNamedArguments(string[] values)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            for (int n = 0; n < this.ParameterNames.Length; n++)
            {
                string[] split = this.ParameterNames[n].Split('=', 2);
                string name = split[0];
                string? defaultValue = split.Length > 1 ? split[1] : null;

                string? value = n >= values.Length ? defaultValue : values[0];
                if (value == null)
                {
                    throw new ActionException($"Action function {this.FunctionName} invoked without parameter {name}");
                }

                result.Add(name, value);
            }

            return result;
        }
    }

    /// <summary>
    /// Identifies a class having action functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class HasActionFunctionsAttribute : Attribute
    {
    }

    public class ActionArgs
    {
        public string FunctionName { get; }
        public string[] ArgumentsArray { get; }
        public Dictionary<string, string> Arguments { get; }
        /// <summary>The button id, for multi-button bar items.</summary>
        public string? Source { get; set; }

        /// <summary>
        /// Gets an argument value by its name, or an empty string if there's no such argument.
        /// </summary>
        /// <param name="argumentName"></param>
        public string this[string argumentName] => this.Arguments.TryGetValue(argumentName, out string? value)
                ? value
                : string.Empty;

        public ActionArgs(ActionFunctionAttribute functionAttribute, string[] args)
        {
            this.FunctionName = functionAttribute.FunctionName;
            this.ArgumentsArray = args;

            this.Arguments = functionAttribute.GetNamedArguments(args);

        }
    }

    public class ActionException : ApplicationException
    {
        /// <summary>
        /// The message displayed to the user. null to not display a message.
        /// </summary>
        public string? UserMessage { get; set; }

        public ActionException(string? userMessage)
            : this(userMessage, userMessage, null)
        {
        }
        public ActionException(string? userMessage, Exception innerException)
            : this(userMessage, userMessage, innerException)
        {
        }

        public ActionException(string? userMessage, string? internalMessage = null, Exception? innerException = null)
            : base(internalMessage ?? userMessage ?? innerException?.Message, innerException)
        {
            this.UserMessage = userMessage;
        }
    }

}
