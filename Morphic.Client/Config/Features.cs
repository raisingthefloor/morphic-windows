namespace Morphic.Client.Config
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Specifies what features are enabled for this build.
    /// </summary>
    public class Features
    {
        private readonly string featureFile =
            AppOptions.Current.Launch.FeaturesFile ?? AppPaths.GetAppFile("features");

        public Features()
        {
            this.LoadFeatures();
        }

        public string EditionName { get; protected set; } = "internal";

        [Feature("Community")]
        public bool Community { get; protected set; }

        [Feature("Basic")]
        public bool Basic { get; protected set; }

        /// <summary>
        /// Loads the features file, which is a list of features which point to one or more properties in this class.
        /// A line in the file that starts with "name=" specifies the name of the edition of the build (eg, "Basic").
        /// The [Feature] attribute is used to specify one or more features that set it to true.
        /// </summary>
        private void LoadFeatures()
        {
            List<string> featuresEnabled = File.ReadAllLines(this.featureFile)
                .Select(l => l.Trim().ToUpperInvariant())
                .Where(l => !l.StartsWith('#'))
                .ToList();

            string? editionLine = featuresEnabled.FirstOrDefault(l => l.StartsWith("NAME="));

            if (!string.IsNullOrEmpty(editionLine))
            {
                this.EditionName = editionLine.Substring(editionLine.IndexOf('=') + 1);
            }

            foreach (PropertyInfo property in this.GetType().GetProperties())
            {
                foreach (FeatureAttribute attr in property.GetCustomAttributes<FeatureAttribute>(true))
                {
                    string featureName = attr.Name.ToUpperInvariant();
                    if (featuresEnabled.Contains(featureName))
                    {
                        property.SetValue(this, true);
                    }
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    internal sealed class FeatureAttribute : Attribute
    {
        public string Name { get; }

        public FeatureAttribute(string name)
        {
            this.Name = name;
        }
    }
}
