using System;
using System.Collections.Generic;
using System.Text;

namespace MorphicSettings
{

    /// <summary>
    /// Interface for registry manipulation
    /// </summary>
    public interface IRegistry
    {

        /// <summary>
        /// Get a value from the registry
        /// </summary>
        /// <param name="keyName">The full registry key</param>
        /// <param name="valueName">The name of the value within the key</param>
        /// <returns>The value, or <code>null</code> if nothing is found</returns>
        public object? GetValue(string keyName, string valueName, object? defaultValue);

        /// <summary>
        /// Set a value in the registry
        /// </summary>
        /// <param name="keyName">The full registry key</param>
        /// <param name="valueName">The name of the value within the key</param>
        /// <param name="value">The value to set</param>
        /// <returns></returns>
        public bool SetValue(string keyName, string valueName, object? value, Microsoft.Win32.RegistryValueKind valueKind);
    }
}
