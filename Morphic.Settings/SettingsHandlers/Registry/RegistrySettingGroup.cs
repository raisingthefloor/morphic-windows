namespace Morphic.Settings.SettingsHandlers.Registry
{
    using System;
    using Resolvers;
    using SolutionsRegistry;

    [SettingsHandlerType("registry", typeof(RegistrySettingsHandler))]
    public class RegistrySettingGroup : SettingGroup
    {
        public ResolvingString RootKeyName { get; private set; } = null!;
        public ResolvingString KeyPath { get; private set; } = null!;

        public override void Deserialized(IServiceProvider serviceProvider, Solution solution)
        {
            base.Deserialized(serviceProvider, solution);

            // Parse the path: <rootkey>\<path>\<value>
            string path = this.Path.ToString().Replace('/', '\\');
            string[] parts = path.Split("\\", 2);
            if (parts.Length != 2)
            {
                throw new SolutionsRegistryException($"'{this.Path}' is not a valid registry key path.");
            }

            this.RootKeyName = parts[0];
            this.KeyPath = parts[1];
        }
    }
}
