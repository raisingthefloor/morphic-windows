using System;
using System.Collections;
using System.Collections.Specialized;
using System.Net;
using System.Windows.Input;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace MorphicWin
{
    public class StringDictionarySetting : StringDictionary, IXmlSerializable
    {
        public XmlSchema GetSchema()
        {
            return null!;
        }

        public void ReadXml(XmlReader reader)
        {
            var endName = GetType().Name;
            while (reader.NodeType != XmlNodeType.EndElement || reader.LocalName != endName)
            {
                reader.Read();
                if (reader.LocalName == "Entry")
                {
                    Add(reader["Key"], reader["Value"]);
                }
            }
        }

        public void WriteXml(XmlWriter writer)
        {
            foreach (var pair in this)
            {
                if (pair is DictionaryEntry entry)
                {
                    writer.WriteStartElement("Entry");
                    writer.WriteAttributeString("Key", entry.Key as string);
                    writer.WriteAttributeString("Value", entry.Value as string);
                    writer.WriteEndElement();
                }
            }
        }
    }
}
