using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Schema;

namespace JobManagerInterfaces
{
    public class XMLHelper
    {
        public static DirectoryInfo ASSEMBLY_DIR = (new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location)).Directory;

        public static XmlDocumentNsmgrCombo ReadXML(string xmlFilePath)
        {
            // Create the XmlSchemaSet class.
            XmlSchemaSet sc = new XmlSchemaSet();

            // Add the schema to the collection.
            sc.Add("http://www.rbadams.com/Automation/JobCollection", Path.Combine(ASSEMBLY_DIR.FullName, @"JobCollection.xsd"));
            sc.Add("http://www.rbadams.com/Automation/Job", Path.Combine(ASSEMBLY_DIR.FullName, @"Job.xsd"));
            sc.Add("http://www.rbadams.com/Automation/ISO", Path.Combine(ASSEMBLY_DIR.FullName, @"ISO.xsd"));
            sc.Add("http://www.rbadams.com/Automation/Package", Path.Combine(ASSEMBLY_DIR.FullName, @"Package.xsd"));
            sc.Add("http://www.rbadams.com/Automation/Sequence", Path.Combine(ASSEMBLY_DIR.FullName, @"Sequence.xsd"));

            // Set the validation settings.
            XmlReaderSettings rs = new XmlReaderSettings();
            rs.ValidationType = ValidationType.Schema;
            rs.Schemas = sc;
            //rs.ValidationEventHandler += new ValidationEventHandler(ValidationCallBack);
            // (Un)comment this to toggle validation warnings
            rs.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;

            //TODO, error out on validation errors?
            rs.ValidationEventHandler += new ValidationEventHandler(delegate(object sender, ValidationEventArgs e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.Exception.ToString());
                Console.WriteLine(e.Severity.ToString());
            });

            using (FileStream fs = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read))
            {
                // Create the XmlReader object
                using (XmlReader vr = XmlReader.Create(fs, rs))
                {
                    XmlDocument doc = new XmlDocument();
                    // if we get past doc.Load, we know document is valid
                    doc.Load(vr);

                    // Initialize a namespace manager
                    XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);

                    // Find the root node
                    XmlNode root = doc.DocumentElement;
                    
                    // Look in the root node for namespace declarations and add these to the
                    // namespace manager
                    foreach (XmlAttribute attrib in root.Attributes)
                    {
                        if (attrib.Prefix == "xmlns")
                        {
                            nsmgr.AddNamespace(attrib.LocalName, attrib.Value);

                        }
                        if (attrib.Name == "xmlns")
                        {
                            //local namespace
                            nsmgr.AddNamespace("lns", attrib.Value);
                        }
                    }

                    return new XmlDocumentNsmgrCombo(doc, nsmgr, root);
                }
            }
        }
    }

    public class XmlDocumentNsmgrCombo
    {
        public XmlDocument Doc;
        public XmlNamespaceManager Nsmgr;
        public XmlNode Root;

        public XmlDocumentNsmgrCombo(XmlDocument doc, XmlNamespaceManager nsmgr, XmlNode root)
        {
            this.Doc = doc;
            this.Nsmgr = nsmgr;
            this.Root = root;
        }

        public string GetChildNodeValue(XmlNode node, string childNodeName)
        {
            return node.SelectSingleNode("lns:" + childNodeName, Nsmgr).InnerText;
        }

        public XmlNode GetChildNode(XmlNode node, string childNodeName)
        {
            return node.SelectSingleNode("lns:" + childNodeName, Nsmgr);
        }
    }
}
