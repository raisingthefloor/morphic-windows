namespace Morphic.Settings.Tests.SolutionsRegistry
{
    using System;
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
            var capturePreferencesResult = await solutions.CapturePreferencesAsync(preferences);
            if (capturePreferencesResult.IsError == true)
            {
                throw new Exception("CapturePreferences failed");
            }

            return;

        }
    }
}
