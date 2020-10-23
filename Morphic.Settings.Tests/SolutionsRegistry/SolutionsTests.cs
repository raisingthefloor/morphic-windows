namespace Morphic.Settings.Tests.SolutionsRegistry
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Core;
    using Microsoft.Extensions.DependencyInjection;
    using Settings.SolutionsRegistry;
    using Xunit;

    public class SolutionsTests
    {
        [Fact]
        public async Task ReadJsonTest()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddSolutionsRegistryServices();
            IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            Solutions solutions = Solutions.FromFile(serviceProvider,
                @"C:\gpii\lite\MorphicLiteClientWindows\Morphic.Client\solutions.json5");

            Preferences preferences = new Preferences();
            await solutions.CapturePreferences(preferences);

            return;

        }
    }
}
