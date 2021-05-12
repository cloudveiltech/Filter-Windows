using System;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.Win32;

namespace CitadelService.Util
{
    public class InstalledProgram
    {
        public string DisplayName { get; set; }
        public string DisplayVersion { get; set; }

        public int? EstimatedSize { get; set; }
        
        public DateTime? InstallDate { get; set; }

        public int? Language { get; set; }

        public string Publisher { get; set; }
        
        public bool SystemComponent { get; set; }
    }

    public static class InstalledPrograms
    {
        private const string UninstallRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string UninstallRegistryKeyWOW = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        /// <summary>
        /// Prints the program to string builder.
        /// </summary>
        /// <param name="sb">The string builder.</param>
        /// <param name="program">The program.</param>
        /// <returns>Returns true if function appended anything to string builder</returns>
        private static bool printProgramToStringBuilder(StringBuilder sb, InstalledProgram program)
        {
            if(program.DisplayName == null && program.DisplayVersion == null)
            {
                return false;
            }

            sb.AppendLine($"\t\t{program.DisplayName} {program.DisplayVersion}");

            if(program.InstallDate != null || program.EstimatedSize != null)
            {
                sb.Append("\t\t");

                if (program.InstallDate != null)
                {
                    sb.Append($"Installed on {program.InstallDate?.ToShortDateString()}");
                }

                if (program.EstimatedSize != null)
                {
                    if (program.InstallDate != null) sb.Append(", ");

                    sb.Append($" Est. Size: {program.EstimatedSize}KB");
                }

                sb.AppendLine();
            }

            if (program.Publisher != null)
            {
                sb.AppendLine($"\t\tPublisher: {program.Publisher}");
            }

            return true;
        }

        public static string BuildInstalledProgramsReport()
        {
            StringBuilder sb = new StringBuilder();
            string separator = new string('-', 80);

            try
            {
                List<InstalledProgram> programs = GetInstalledPrograms();

                IEnumerable<InstalledProgram> systemComponents = programs.Where(p => p.SystemComponent);
                IEnumerable<InstalledProgram> others = programs.Where(p => !p.SystemComponent);

                sb.AppendLine("Installed Programs Report");
                sb.AppendLine(separator);
                sb.AppendLine("\tUser-installed Programs");
                sb.AppendLine(separator);

                foreach (var program in others)
                {
                    bool printed = printProgramToStringBuilder(sb, program);

                    if (printed) sb.AppendLine();
                }

                sb.AppendLine();
                sb.AppendLine("\tSystem Components (or MSIs installed that don't show up in add/remove programs)");
                sb.AppendLine(separator);

                foreach(var program in systemComponents)
                {
                    bool printed = printProgramToStringBuilder(sb, program);

                    if (printed) sb.AppendLine();
                }

                return sb.ToString();
            }
            catch(Exception ex)
            {
                string msg = $"Error occurred while building installed programs report. {ex}";

                if (sb != null)
                {
                    sb.AppendLine();
                    sb.AppendLine(msg);
                    return sb.ToString();
                }
                else
                {
                    return msg;
                }
            }

        }

        public static List<InstalledProgram> GetInstalledPrograms()
        {
            List<InstalledProgram> programs = new List<InstalledProgram>();

            List<InstalledProgram> list1 = getInstalledProgramsFromKey(UninstallRegistryKey);
            List<InstalledProgram> list2 = getInstalledProgramsFromKey(UninstallRegistryKeyWOW);

            programs.AddRange(list1);
            programs.AddRange(list2);

            return programs;
        }

        private static List<InstalledProgram> getInstalledProgramsFromKey(string keyName)
        {
            List<InstalledProgram> programs = new List<InstalledProgram>();

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName))
            {
                foreach(string subkeyName in key.GetSubKeyNames())
                {
                    using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                    {
                        InstalledProgram program = getInstalledProgramFromKey(subkey);

                        programs.Add(program);
                    }
                }
            }

            return programs;
        }

        private static InstalledProgram getInstalledProgramFromKey(RegistryKey key)
        {
            InstalledProgram program = new InstalledProgram();

            program.DisplayName = key.GetValue("DisplayName") as string;
            program.DisplayVersion = key.GetValue("DisplayVersion") as string;

            program.EstimatedSize = key.GetValue("EstimatedSize", null) as int?;

            string installDateString = key.GetValue("InstallDate") as string;

            DateTime? installDate = null;
            if(installDateString != null)
            {
                DateTime dt;
                if(DateTime.TryParseExact(installDateString, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt))
                {
                    installDate = dt;
                }
            }

            program.Language = key.GetValue("Language") as int?;

            program.Publisher = key.GetValue("Publisher") as string;

            int? systemComponentUint = key.GetValue("SystemComponent", 0) as int?;
            program.SystemComponent = systemComponentUint == null || systemComponentUint == 0 ? false : true;

            return program;
        }
    }
}
