namespace Morphic.Settings.SettingsHandlers.XML
{
    using Morphic.Core;
    using SolutionsRegistry;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Xml;

    [SrService]
    class XMLSettingsHandler : SettingsHandler
    {
        private readonly IServiceProvider serviceProvider;

        public XMLSettingsHandler(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public override async Task<(IMorphicResult, Values)> GetAsync(SettingGroup settingGroup, IEnumerable<Setting> settings)
        {
            var success = true;

            Values values = new Values();

            try
            {
                XMLSettingGroup? xmlSettingGroup = settingGroup as XMLSettingGroup;
                if (xmlSettingGroup == null)
                {
                    success = false;
                }
                else if (!System.IO.File.Exists(Environment.ExpandEnvironmentVariables(xmlSettingGroup.Path)))
                {
                    success = false;
                }
                else
                {
                    XmlDocument doc = new XmlDocument();
                    string path = Environment.ExpandEnvironmentVariables(xmlSettingGroup.Path);
                    doc.Load(path);
                    foreach (Setting setting in settings)
                    {
                        XmlNode node = doc.SelectSingleNode(MakeXPath(setting));
                        if (node != null)
                        {
                            string text = node.InnerText;
                            if (text != null)
                            {
                                switch (setting.DataType)
                                {
                                    case SettingType.Bool:
                                        values.Add(setting, text.ToLower() == "true", Values.ValueType.UserSetting);
                                        break;
                                    case SettingType.Int:
                                        values.Add(setting, int.Parse(text), Values.ValueType.UserSetting);
                                        break;
                                    case SettingType.Real:
                                        values.Add(setting, double.Parse(text), Values.ValueType.UserSetting);
                                        break;
                                    case SettingType.String:
                                        values.Add(setting, text, Values.ValueType.UserSetting);
                                        break;
                                }
                            }
                            else
                            {
                                values.Add(setting, null, Values.ValueType.NotFound);
                            }
                        }
                        else
                        {
                            values.Add(setting, null, Values.ValueType.NotFound);
                        }
                    }
                }
            }
            catch
            {
                success = false;
            }

            return (success ? IMorphicResult.SuccessResult : IMorphicResult.ErrorResult, values);
        }

        public override async Task<IMorphicResult> SetAsync(SettingGroup settingGroup, Values values)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                string path = Environment.ExpandEnvironmentVariables(settingGroup.Path);
                doc.Load(path);
                foreach ((Setting setting, object? value) in values)
                {
                    XmlNode node = doc.SelectSingleNode(MakeXPath(setting));
                    if(node == null)
                    {
                        node = CreateByXpath(doc, doc, MakeXPath(setting));
                    }
                    node.InnerText = value.ToString();
                    if (setting.DataType == SettingType.Bool)
                    {
                        node.InnerText = node.InnerText.ToLower();  //xml uses lower case
                    }
                }
                doc.Save(path);
                return IMorphicResult.SuccessResult;
            }
            catch
            {
                return IMorphicResult.ErrorResult;
            }
        }

        //formats the name of a setting into an xpath for text access.
        private string MakeXPath(Setting setting)
        {
            return "/" + setting.Name.Replace('.', '/');// + "/text()";
        }

        private XmlNode CreateByXpath(XmlDocument doc, XmlNode parent, string xpath)
        {
            string[] pieces = xpath.Trim('/').Split('/');
            string nextNode = pieces.First();
            if (string.IsNullOrEmpty(nextNode))
            {
                return parent;
            }
            XmlNode node = parent.SelectSingleNode(nextNode);
            if (node == null)
            {
                node = parent.AppendChild(doc.CreateElement(nextNode));
            }
            string pathRemainder = String.Join('/', pieces.Skip(1).ToArray());
            return CreateByXpath(doc, node, pathRemainder);
        }
    }
}
