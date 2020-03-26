using System;
using Microsoft.Extensions.Logging;

namespace MorphicSettings
{
    public class Settings
    {

        public Settings(ILogger<Settings> logger)
        {
            this.logger = logger;
        }

        private readonly ILogger<Settings> logger;

        public bool Apply(string solution, string preference, object? value)
        {
            if (solution == "com.microsoft.windows.display")
            {
                if (preference == "zoom")
                {
                    if (value is string stringValue)
                    {
                        if (Enum.TryParse<Display.ZoomLevel>(stringValue, true, out var level))
                        {
                            var success = Display.Main.SetZoomLevel(level);
                            if (!success)
                            {
                                logger.LogError("Failed to set display zoom level");
                            }
                            return success;
                        }
                        else
                        {
                            logger.LogError("Provided zoom level is invalid");
                        }
                    }
                    else
                    {
                        logger.LogError("Provided zoom level is not a string");
                    }
                }
            }
            return false;
        }
    }
}
