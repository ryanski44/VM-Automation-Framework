using System;
using System.Collections.Generic;
using System.Text;
using Vim25Api;
using System.Net;

namespace JobManagerService
{
    public enum DataStores
    {
        datastore1,
        datastore2,
        datastore3,
        datastore4
    }

    public class VimHelper
    {
        private static VimService _service;
        private static ManagedObjectReference _serviceMOB;
        private static ServiceContent _serviceContent;
        private static ManagedObjectReference _dataCenterMOB;
        private static List<string> cachedVMPaths;

        private static bool ValidateRemoteCertificate(
           object sender,
           System.Security.Cryptography.X509Certificates.X509Certificate certificate,
           System.Security.Cryptography.X509Certificates.X509Chain chain,
           System.Net.Security.SslPolicyErrors policyErrors)
        {

            // allow any old dodgy certificate...
            return true;
        }

        public static VimService ServiceInstance
        {
            get
            {
                if (_service == null)
                {
                    _service = new VimService();
                    _service.Url = "https://" + AppConfig.VSphereHost + "/sdk/vimService";
                    _service.CookieContainer = new CookieContainer();
                }
                return _service;
            }
        }

        public static ManagedObjectReference DataCenterMOB
        {
            get
            {
                if (VimHelper.CurrentSession == null)
                {
                    VimHelper.Login();
                    _dataCenterMOB = null;
                }
                if (_dataCenterMOB == null)
                {
                    _dataCenterMOB = GetMoRefProp(ServiceContent.rootFolder, null);
                }
                return _dataCenterMOB;
            }
        }

        public static ManagedObjectReference ServiceMOB
        {
            get
            {
                if (_serviceMOB == null)
                {
                    // hack to ignore bad certificates
                    ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(ValidateRemoteCertificate);

                    _serviceMOB = new ManagedObjectReference();
                    _serviceMOB.type = "ServiceInstance";
                    _serviceMOB.Value = "ServiceInstance";
                }
                return _serviceMOB;
            }
        }

        public static void Login()
        {
            if (ServiceContent.sessionManager != null)
            {
                ServiceInstance.Login(ServiceContent.sessionManager, AppConfig.VSphereUser, AppConfig.VSpherePass, null);
            }
        }

        public static UserSession CurrentSession
        {
            get
            {
                return GetDynamicProperty<UserSession>(ServiceContent.sessionManager, "currentSession");
            }
        }

        public static void Logout()
        {
            if (ServiceContent.sessionManager != null)
            {
                ServiceInstance.Logout(ServiceContent.sessionManager);
            }
        }

        public static ServiceContent ServiceContent
        {
            get
            {
                if (_serviceContent == null)
                {
                    _serviceContent = ServiceInstance.RetrieveServiceContent(ServiceMOB);
                }
                return _serviceContent;
            }
        }

        /// <summary>
        /// Get a MORef from the property returned.
        /// </summary>
        /// <param name="objMor">Object to get a reference property from</param>
        /// <param name="propName">name of the property that is the MORef</param>
        /// <returns>the MORef for that property.</returns>
        public static ManagedObjectReference GetMoRefProp(ManagedObjectReference objMor, string propName)
        {
            if (objMor == null)
            {
                throw new Exception("Need an Object Reference to get Contents from.");
            }

            // If no property specified, assume childEntity
            if (propName == null)
            {
                propName = "childEntity";
            }

            ObjectContent[] objcontent =
               GetObjectProperties(
                  null, objMor, new string[] { propName }
               );

            ManagedObjectReference propmor = null;
            if (objcontent.Length > 0 && objcontent[0].propSet.Length > 0)
            {
                object o = objcontent[0].propSet[0].val;
                if(o is ManagedObjectReference)
                {
                    propmor = (ManagedObjectReference)o;
                }
                else if (o is ManagedObjectReference[])
                {
                    propmor = ((ManagedObjectReference[])o)[0];
                }
            }
            else
            {
                throw new Exception("Did not find first " + propName + " in " + objMor.type);
            }

            return propmor;
        }

        /// <summary>
        /// Retrieve contents for a single object based on the property collector
        /// registered with the service. 
        /// </summary>
        /// <param name="collector">Property collector registered with service</param>
        /// <param name="mobj">Managed Object Reference to get contents for</param>
        /// <param name="properties">names of properties of object to retrieve</param>
        /// <returns>retrieved object contents</returns>
        public static ObjectContent[] GetObjectProperties(
           ManagedObjectReference collector,
           ManagedObjectReference mobj, string[] properties
        )
        {
            if (mobj == null)
            {
                return null;
            }

            ManagedObjectReference usecoll = collector;
            if (usecoll == null)
            {
                usecoll = ServiceContent.propertyCollector;
            }

            PropertyFilterSpec spec = new PropertyFilterSpec();
            spec.propSet = new PropertySpec[] { new PropertySpec() };
            spec.propSet[0].all = properties == null || properties.Length == 0;
            spec.propSet[0].allSpecified = spec.propSet[0].all;
            spec.propSet[0].type = mobj.type;
            spec.propSet[0].pathSet = properties;

            spec.objectSet = new ObjectSpec[] { new ObjectSpec() };
            spec.objectSet[0].obj = mobj;
            spec.objectSet[0].skip = false;

            return ServiceInstance.RetrieveProperties(usecoll, new PropertyFilterSpec[] { spec });
        }

        public static T GetDynamicProperty<T>(ManagedObjectReference mor, String propertyName)
        {
            ObjectContent[] objContent = GetObjectProperties(null, mor,
                  new String[] { propertyName });

            Object propertyValue = null;
            if (objContent != null)
            {
                DynamicProperty[] dynamicProperty = objContent[0].propSet;
                if (dynamicProperty != null)
                {
                    Object dynamicPropertyVal = dynamicProperty[0].val;
                    String dynamicPropertyName = dynamicPropertyVal.GetType().FullName;
                    propertyValue = dynamicPropertyVal;
                }
            }
            return (T)propertyValue;
        }

        public static T[] GetDynamicPropertyArray<T>(ManagedObjectReference mor, String propertyName)
        {
            ObjectContent[] objContent = GetObjectProperties(null, mor,
                  new String[] { propertyName });

            List<T> propertyValues = new List<T>();
            if (objContent != null)
            {
                DynamicProperty[] dynamicProperty = objContent[0].propSet;
                if (dynamicProperty != null)
                {
                    Object[] dynamicPropertyVal = (object[])dynamicProperty[0].val;
                    for (int i = 0; i < dynamicPropertyVal.Length; i++)
                    {
                        String dynamicPropertyName = dynamicPropertyVal[i].GetType().FullName;
                        propertyValues.Add((T)dynamicPropertyVal[i]);
                    }
                }
            }
            return propertyValues.ToArray();
        }

        public static List<string> AllVMPaths
        {
            get
            {
                if (cachedVMPaths != null)
                {
                    return cachedVMPaths;
                }
                else
                {
                    if (CurrentSession == null)
                    {
                        Login();
                    }
                    ManagedObjectReference root = ServiceContent.rootFolder;
                    ManagedObjectReference dataCenter = GetDynamicPropertyArray<ManagedObjectReference>(root, "childEntity")[0];
                    ManagedObjectReference vmFolder = GetDynamicProperty<ManagedObjectReference>(dataCenter, "vmFolder");
                    List<ManagedObjectReference> allVMs = ChildVMs(vmFolder);
                    List<string> paths = new List<string>();
                    foreach (ManagedObjectReference vm in allVMs)
                    {
                        paths.Add(GetVMPath(vm));
                    }
                    cachedVMPaths = paths;
                    return paths;
                }
            }
        }

        public static List<ManagedObjectReference> ChildVMs(ManagedObjectReference parent)
        {
            List<ManagedObjectReference> children = new List<ManagedObjectReference>();
            foreach (ManagedObjectReference mor in GetDynamicPropertyArray<ManagedObjectReference>(parent, "childEntity"))
            {
                if (mor.type == "Folder")
                {
                    children.AddRange(ChildVMs(mor));
                }
                else if (mor.type == "VirtualMachine")
                {
                    children.Add(mor);
                }
                else if (mor.type == "VirtualApp")
                {
                    //ignore
                }
            }
            return children;
        }

        public static string GetVMPath(ManagedObjectReference vm)
        {
            Stack<string> path = new Stack<string>();
            path.Push(GetDynamicProperty<string>(vm, "name"));
            ManagedObjectReference parent = GetDynamicProperty<ManagedObjectReference>(vm, "parent");
            while (parent.type != "Datacenter")
            {
                path.Push(GetDynamicProperty<string>(parent, "name"));
                parent = GetDynamicProperty<ManagedObjectReference>(parent, "parent");
            }
            path.Push(GetDynamicProperty<string>(parent, "name"));
            return String.Join("/", path.ToArray());
        }
    }
}
