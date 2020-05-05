using System;
using Microsoft.Win32;

namespace MorphicSettings
{
    /// <summary>
    /// Concrete <code>IRegistry</code> implementation that gets/sets values from the windows registry
    /// </summary>
    public class WindowsRegistry : IRegistry
    {
        public object? GetValue(string keyName, string valueName, object? defaultValue)
        {
            return Registry.GetValue(keyName, valueName, defaultValue);
        }

        public bool SetValue(string keyName, string valueName, object? value, RegistryValueKind valueKind)
        {
            Registry.SetValue(keyName, valueName, value, valueKind);
            return true;
        }
    }
}
