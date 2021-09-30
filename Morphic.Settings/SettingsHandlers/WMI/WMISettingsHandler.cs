namespace Morphic.Settings.SettingsHandlers.WMI
{
    using Morphic.Core;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Management;
    using System.Linq;
    using Morphic.Settings.SolutionsRegistry;

    [SrService]
    class WMISettingsHandler : SettingsHandler
    {
        private readonly IServiceProvider serviceProvider;

        public WMISettingsHandler(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public override async Task<(IMorphicResult, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            var success = true;
            Values values = new Values();

            var wmiSettingGroup = settingGroup as WMISettingGroup;
            if (wmiSettingGroup == null)
            {
                success = false;
            }
            else if (!WMIConstants.namespaceWhitelist.Contains(wmiSettingGroup.wmiNamespace))
            {
                success = false;
            }
            else if (wmiSettingGroup.getClassName == "" || wmiSettingGroup.getProperty == "")
            {
                success = false;
            }
            else if (!WMIConstants.getClassWhitelist.Contains(wmiSettingGroup.getClassName))
            {
                success = false;
            }
            else
            {
                try
                {
                    ManagementScope scope = new ManagementScope(wmiSettingGroup.wmiNamespace);
                    SelectQuery query = new SelectQuery("SELECT " + wmiSettingGroup.getProperty + " FROM " + wmiSettingGroup.getClassName + " " + wmiSettingGroup.getExtra);

                    ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, query);
                    ManagementObjectCollection objectCollection = searcher.Get();
                    var firstObject = objectCollection.OfType<ManagementObject>().FirstOrDefault();
                    var selectedItem = firstObject.Properties.OfType<PropertyData>().FirstOrDefault();

                    foreach (Setting setting in settings)
                    {
                        values.Add(setting, selectedItem.Value, (selectedItem.Value == null) ? Values.ValueType.NotFound : Values.ValueType.UserSetting);
                    }
                }
                catch
                {
                    success = false;
                }
            }
            return (success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult, values);
        }

        public override async Task<IMorphicResult> SetAsync(SettingGroup settingGroup, Values values)
        {
            var success = true;

            var wmiSettingGroup = settingGroup as WMISettingGroup;
            if (wmiSettingGroup == null)
            {
                success = false;
            }
            else if (!WMIConstants.namespaceWhitelist.Contains(wmiSettingGroup.wmiNamespace))
            {
                success = false;
            }
            else if (!WMIConstants.methodWhitelist.ContainsKey(wmiSettingGroup.setClassName) || !WMIConstants.methodWhitelist[wmiSettingGroup.setClassName].Contains(wmiSettingGroup.setMethod))
            {
                success = false;
            }
            else if (wmiSettingGroup.setParams == null)
            {
                success = false;
            }
            else
            {
                var retType = wmiSettingGroup.setReturnVal[0] as string;
                var successVal = wmiSettingGroup.setReturnVal[1];
                if (retType == null || !WMIConstants.RetTypes.ContainsKey(retType))
                {
                    success = false;
                }
                else
                {
                    try
                    {
                        ManagementScope s = new ManagementScope(wmiSettingGroup.wmiNamespace);
                        SelectQuery q = new SelectQuery(wmiSettingGroup.setClassName);
                        ManagementObjectSearcher mos = new ManagementObjectSearcher(s, q);
                        ManagementObjectCollection moc = mos.Get();

                        var firstObject = moc.OfType<ManagementObject>().FirstOrDefault();
                        var result = firstObject.InvokeMethod(wmiSettingGroup.setMethod, wmiSettingGroup.setParams) ?? GetDefault(WMIConstants.RetTypes[retType]);
                        var converted = Convert.ChangeType(successVal, WMIConstants.RetTypes[retType]);
                        if (converted.Equals(result) == false)
                        {
                            success = false;
                        }
                    }
                    catch
                    {
                        success = false;
                    }
                }
            }
            return success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult;
        }

        public static object? GetDefault(Type type)
        {
            if(type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }
    }

    class WMIConstants
    {
        public static readonly Dictionary<string, Type> RetTypes = new Dictionary<string, Type>
        {
            { "int", typeof(int) },
            { "uint", typeof(uint) },
            { "string", typeof(string) }
        };

        public static readonly List<String> namespaceWhitelist = new List<string>
        {
            "root\\WMI"
        };

        public static readonly List<String> getClassWhitelist = new List<string>
        {
            "WmiMonitorBrightness"
        };

        public static readonly Dictionary<String, List<String>> methodWhitelist = new Dictionary<string, List<string>>
        {
            { "WmiMonitorBrightnessMethods" , new List<string> { "WmiSetBrightness" } }
        };
    }
}
