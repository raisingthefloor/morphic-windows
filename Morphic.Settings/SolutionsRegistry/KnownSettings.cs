namespace Morphic.Settings.SolutionsRegistry
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public static class KnownSettings
    {

    }

    public class SettingIdentifier
    {
        public string SolutionId { get; }
        public string SettingId { get; }

        public SettingIdentifier(string solutionId, string settingId)
        {
            this.SolutionId = solutionId;
            this.SettingId = settingId;
        }

        public override string ToString()
        {
            return $"{this.SolutionId}/{this.SettingId}";
        }
    }
}
