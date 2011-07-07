using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace JobManagerInterfaces
{
    public class XMLSerializable
    {
        public string ToXML()
        {
            return ToXML(false);
        }

        public string ToXML(bool pretty)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                XmlSerializer xs = new XmlSerializer(this.GetType());
                using (XmlTextWriter tw = new XmlTextWriter(ms, Encoding.UTF8))
                {
                    if (pretty)
                    {
                        tw.Formatting = Formatting.Indented;
                        tw.Indentation = 2;
                    }
                    xs.Serialize(tw, this);
                    ms.Position = 0;
                    using (TextReader tr = new StreamReader(ms, Encoding.UTF8))
                    {
                        return tr.ReadToEnd();
                    }
                }
            }
        }

        public static T FromXML<T>(string xmlContent)
        {
            XmlSerializer xs = new XmlSerializer(typeof(T));
            StringReader sr = new StringReader(xmlContent);
            try
            {
                return (T)xs.Deserialize(sr);
            }
            finally
            {
                sr.Close();
            }
        }

        public static void ToXMLFile(object o, string fileName)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                XmlSerializer xs = new XmlSerializer(o.GetType());
                XmlWriterSettings writerSettings = new XmlWriterSettings();
                writerSettings.CheckCharacters = true;
                writerSettings.ConformanceLevel = ConformanceLevel.Document;
                writerSettings.Encoding = Encoding.UTF8;
                XmlWriter tw = XmlWriter.Create(fs, writerSettings);
                xs.Serialize(tw, o);
                tw.Flush();
            }
        }

        public static T FromXMLFile<T>(string fileName)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                XmlSerializer xs = new XmlSerializer(typeof(T));
                return (T)xs.Deserialize(fs);
            }
        }
    }
}
