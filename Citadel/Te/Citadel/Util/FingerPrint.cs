using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Te.Citadel.Util
{
    internal class FingerPrint
    {
        private static string s_fingerPrint;

        public static string Value
        {
            get
            {
                if(string.IsNullOrEmpty(s_fingerPrint))
                {
                    s_fingerPrint = GetHash("CPU >> " + CpuID + "\nBIOS >> " +
                BiosID + "\nBASE >> " + BaseboardID
                +//"\nDISK >> "+ diskId() + "\nVIDEO >> " +
                VideoCardID + "\nMAC >> " + MacID
                                         );
                }

                return s_fingerPrint;
            }
        }

        private static string GetHash(string s)
        {
            using(SHA256 sec = new SHA256CryptoServiceProvider())
            {
                byte[] bt = sec.ComputeHash(Encoding.UTF8.GetBytes(s));
                return BitConverter.ToString(bt).Replace("-", "");
            }
        }

        #region Original Device ID Getting Code

        //Return a hardware identifier
        private static string GetIdentifier(string wmiClass, string wmiProperty, string wmiMustBeTrue)
        {
            string result = "";
            var mc = new ManagementClass(wmiClass);
            var moc = mc.GetInstances();
            foreach(var mo in moc)
            {
                if(mo[wmiMustBeTrue].ToString() == "True")
                {
                    //Only get the first one
                    if(result == "")
                    {
                        try
                        {
                            result = mo[wmiProperty].ToString();
                            break;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            return result;
        }

        //Return a hardware identifier
        private static string GetIdentifier(string wmiClass, string wmiProperty)
        {
            string result = "";
            var mc = new ManagementClass(wmiClass);
            var moc = mc.GetInstances();
            foreach(var mo in moc)
            {
                //Only get the first one
                if(result == "")
                {
                    try
                    {
                        result = mo[wmiProperty].ToString();
                        break;
                    }
                    catch
                    {
                    }
                }
            }
            return result;
        }

        private static string CpuID
        {
            get
            {
                //Uses first CPU identifier available in order of preference
                //Don't get all identifiers, as it is very time consuming
                string retVal = GetIdentifier("Win32_Processor", "UniqueId");
                if(retVal == "") //If no UniqueID, use ProcessorID
                {
                    retVal = GetIdentifier("Win32_Processor", "ProcessorId");
                    if(retVal == "") //If no ProcessorId, use Name
                    {
                        retVal = GetIdentifier("Win32_Processor", "Name");
                        if(retVal == "") //If no Name, use Manufacturer
                        {
                            retVal = GetIdentifier("Win32_Processor", "Manufacturer");
                        }
                        //Add clock speed for extra security
                        retVal += GetIdentifier("Win32_Processor", "MaxClockSpeed");
                    }
                }
                return retVal;
            }
        }

        //BIOS Identifier
        private static string BiosID
        {
            get
            {
                return GetIdentifier("Win32_BIOS", "Manufacturer")
                + GetIdentifier("Win32_BIOS", "SMBIOSBIOSVersion")
                + GetIdentifier("Win32_BIOS", "IdentificationCode")
                + GetIdentifier("Win32_BIOS", "SerialNumber")
                + GetIdentifier("Win32_BIOS", "ReleaseDate")
                + GetIdentifier("Win32_BIOS", "Version");
            }
        }

        //Main physical hard drive ID
        private static string DiskID
        {
            get
            {
                return GetIdentifier("Win32_DiskDrive", "Model")
                + GetIdentifier("Win32_DiskDrive", "Manufacturer")
                + GetIdentifier("Win32_DiskDrive", "Signature")
                + GetIdentifier("Win32_DiskDrive", "TotalHeads");
            }
        }

        //Motherboard ID
        private static string BaseboardID
        {
            get
            {
                return GetIdentifier("Win32_BaseBoard", "Model")
                + GetIdentifier("Win32_BaseBoard", "Manufacturer")
                + GetIdentifier("Win32_BaseBoard", "Name")
                + GetIdentifier("Win32_BaseBoard", "SerialNumber");
            }
        }

        //Primary video controller ID
        private static string VideoCardID
        {
            get
            {
                return GetIdentifier("Win32_VideoController", "DriverVersion")
                + GetIdentifier("Win32_VideoController", "Name");
            }
        }

        //First enabled network card ID
        private static string MacID
        {
            get
            {
                return GetIdentifier("Win32_NetworkAdapterConfiguration",
                "MACAddress", "IPEnabled");
            }
        }

        #endregion Original Device ID Getting Code
    }
}