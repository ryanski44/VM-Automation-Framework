using System;
using System.Collections.Generic;
using System.Text;
using System.Management;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace Wait
{
    class Program
    {
        static Dictionary<string, string>[] states = new Dictionary<string, string>[3];
        static void Main(string[] args)
        {
            try
            {
                int index = 0;

                Console.WriteLine("Waiting for all services to finish initializing");
                while (!Ready())
                {
                    string query = "select Name, State, Status from Win32_Service";

                    ManagementScope scope = new ManagementScope();

                    ManagementObjectSearcher searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
                    ManagementObjectCollection retObjectCollection = searcher.Get();

                    states[index] = new Dictionary<string, string>();

                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string name = mo["Name"].ToString();
                        string state = mo["State"].ToString();
                        string status = mo["Status"].ToString();
                        states[index][name] = state;
                        if (state == "Start Pending" || status == "Starting")
                        {
                            Console.WriteLine(name + "\t" + state + "\t" + status);
                        }
                        //Logger.Instance.LogString(name + "\t" + state + "\t" + status);
                    }

                    index++;
                    if (index > 2)
                    {
                        index = 0;
                    }
                    System.Threading.Thread.Sleep(500);
                }

                Console.WriteLine("Waiting for network to be up and running");
                bool networkReady = false;
                while (!networkReady)
                {
                    foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback && adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                        {
                            IPInterfaceProperties properties = adapter.GetIPProperties();
                            Console.WriteLine();
                            Console.WriteLine(adapter.Description);
                            Console.WriteLine(String.Empty.PadLeft(adapter.Description.Length, '='));
                            Console.WriteLine("  Interface type .......................... : {0}", adapter.NetworkInterfaceType);
                            Console.WriteLine("  Physical Address ........................ : {0}",
                                       adapter.GetPhysicalAddress().ToString());
                            Console.WriteLine("  Operational status ...................... : {0}",
                                adapter.OperationalStatus);
                            Console.WriteLine("  Receive Only ............................ : {0}",
                                adapter.IsReceiveOnly);

                            if (adapter.OperationalStatus == OperationalStatus.Up && !adapter.IsReceiveOnly)
                            {
                                if (adapter.Supports(NetworkInterfaceComponent.IPv4))
                                {
                                    IPv4InterfaceProperties ipv4 = properties.GetIPv4Properties();
                                    if (ipv4 != null)
                                    {
                                        if (properties.GatewayAddresses.Count > 0)
                                        {
                                            Console.WriteLine("  Gateways:");
                                            foreach (GatewayIPAddressInformation gateway in properties.GatewayAddresses)
                                            {
                                                foreach(byte addressByte in gateway.Address.GetAddressBytes())
                                                {
                                                    if (addressByte != 0)
                                                    {
                                                        networkReady = true;
                                                        break;
                                                    }
                                                }
                                                Console.WriteLine("    " + gateway.Address.ToString());
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    System.Threading.Thread.Sleep(1000);
                }

                Console.WriteLine("Done");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            try
            {
                if (args.Length > 0)
                {
                    Process p = new Process();
                    p.StartInfo.FileName = args[0];
                    List<string> arguments = new List<string>();
                    for (int i = 1; i < args.Length; i++)
                    {
                        arguments.Add(args[i]);
                    }
                    p.StartInfo.Arguments = String.Join(" ", arguments.ToArray());
                    p.Start();
                    return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static bool Ready()
        {
            foreach (Dictionary<string, string> baseState in states)
            {
                if (baseState == null)
                {
                    return false;
                }
                foreach (string service in baseState.Keys)
                {
                    foreach (Dictionary<string, string> state in states)
                    {
                        if (state == null)
                        {
                            return false;
                        }
                        if (!state.ContainsKey(service))
                        {
                            return false;
                        }
                        string baseValue = baseState[service];
                        string thisValue = state[service];
                        if (baseValue != thisValue)
                        {
                            return false;
                        }

                        if (thisValue == "Start Pending")
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }

    
}
