using System;
using System.Collections.Generic;
using System.Text;

namespace JobManagerInterfaces.XML
{
    public partial class Job
    {
        public static Job FromXML(string xmlPath)
        {
            return XMLSerializable.FromXMLFile<Job>(xmlPath);
        }
    }

    public partial class ISO
    {
        public static ISO FromXML(string xmlPath)
        {
            return XMLSerializable.FromXMLFile<ISO>(xmlPath);
        }
    }

    public partial class JobCollection
    {
        public static JobCollection FromXML(string xmlPath)
        {
            return XMLSerializable.FromXMLFile<JobCollection>(xmlPath);
        }
    }

    public partial class Package
    {
        public static Package FromXML(string xmlPath)
        {
            return XMLSerializable.FromXMLFile<Package>(xmlPath);
        }
    }

    public partial class Sequence
    {
        public static Sequence FromXML(string xmlPath)
        {
            return XMLSerializable.FromXMLFile<Sequence>(xmlPath);
        }
    }

    public partial class ConfigurationType
    {
        public static ConfigurationType FromXML(string xmlPath)
        {
            return XMLSerializable.FromXMLFile<ConfigurationType>(xmlPath);
        }
    }
}
