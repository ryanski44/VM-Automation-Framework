using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace JobManagerMonitor
{
    public static class Utility
    {
        public static void WriteTabs(StringBuilder sb, int count)
        {
            for (int i = 0; i < count; i++)
            {
                sb.Append("\t");
            }
        }

        internal class FieldOrPropertyMember
        {
            private FieldInfo fi;
            private PropertyInfo pi;

            public FieldOrPropertyMember(FieldInfo fi)
            {
                this.fi = fi;
            }

            public FieldOrPropertyMember(PropertyInfo pi)
            {
                this.pi = pi;
            }

            public object GetValue(object obj)
            {
                if (pi != null)
                {
                    return pi.GetValue(obj, null);
                }
                else if (fi != null)
                {
                    return fi.GetValue(obj);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            public string Name
            {
                get
                {
                    if (pi != null)
                    {
                        return pi.Name;
                    }
                    else if (fi != null)
                    {
                        return fi.Name;
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }

        public static string DumpObject(object o, int indentLevel, params Type[] recurseOn)
        {
            if (recurseOn == null)
            {
                recurseOn = new Type[0];
            }
            StringBuilder sb = new StringBuilder();
            WriteTabs(sb, indentLevel);

            if (o == null)
            {
                sb.AppendLine("NULL OBJECT");
                return sb.ToString();
            }

            Type t = o.GetType();
            
            sb.AppendLine(t.FullName + ":");
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(t))
            {
                System.Collections.IEnumerable list = (System.Collections.IEnumerable)o;
                foreach (object subObject in list)
                {
                    sb.AppendLine(DumpObject(subObject, indentLevel + 1, recurseOn));
                }
            }
            else
            {
                List<FieldOrPropertyMember> members = new List<FieldOrPropertyMember>();
                foreach (FieldInfo fi in t.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    members.Add(new FieldOrPropertyMember(fi));
                }
                foreach (PropertyInfo pi in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    members.Add(new FieldOrPropertyMember(pi));
                }
                foreach (FieldOrPropertyMember fi in members)
                {
                    try
                    {
                        object val = fi.GetValue(o);
                        if (val == null)
                        {
                            val = "NULL";
                        }
                        if (val is byte[])
                        {
                            StringBuilder sb2 = new StringBuilder();
                            byte[] array = (byte[])val;
                            int j = 0;
                            foreach (byte b in array)
                            {
                                if (j % 16 == 0)
                                {
                                    sb2.AppendLine();
                                    WriteTabs(sb2, indentLevel + 1);
                                }
                                sb2.Append(b.ToString("X2"));
                                sb2.Append(" ");
                                j++;
                            }
                            val = sb2.ToString();
                        }
                        bool recurse = false;
                        foreach (Type type in recurseOn)
                        {
                            if (type.IsAssignableFrom(val.GetType()))
                            {
                                recurse = true;
                                break;
                            }
                        }
                        
                        if (recurse)
                        {
                            WriteTabs(sb, indentLevel);
                            sb.AppendLine(fi.Name + ":");
                            sb.AppendLine(DumpObject(val, indentLevel + 1, recurseOn));
                        }
                        else
                        {
                            WriteTabs(sb, indentLevel);
                            sb.AppendLine(fi.Name + ":\t" + val.ToString());
                        }
                    }
                    catch (Exception)
                    {
                        //eat it
                    }
                }
            }
            //foreach (FieldOrPropertyMember pi in members)
            //{
            //    try
            //    {
            //        object val = pi.GetValue(o, null);
            //        if (val == null)
            //        {
            //            val = "NULL";
            //        }
            //        if (val is byte[])
            //        {
            //            StringBuilder sb2 = new StringBuilder();
            //            byte[] array = (byte[])val;
            //            int j = 0;
            //            foreach (byte b in array)
            //            {
            //                if (j % 16 == 0)
            //                {
            //                    sb2.AppendLine();
            //                    sb2.Append("\t\t");
            //                }
            //                sb2.Append(b.ToString("X2"));
            //                sb2.Append(" ");
            //                j++;
            //            }
            //            val = sb2.ToString();
            //        }
            //        bool recurse = false;
            //        foreach (Type type in recurseOn)
            //        {
            //            if (type.IsInstanceOfType(val))
            //            {
            //                recurse = true;
            //                break;
            //            }
            //        }
            //        if (recurse)
            //        {
            //            //sb.AppendLine("\t" + pi.Name + ":");
            //            sb.AppendLine(DumpObject(val, indentLevel + 1, recurseOn));
            //        }
            //        else
            //        {
            //            WriteTabs(sb, indentLevel);
            //            sb.AppendLine(pi.Name + ":\t" + val.ToString());
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        //eat it
            //    }
            //}
            return sb.ToString();
        }
    }
}
