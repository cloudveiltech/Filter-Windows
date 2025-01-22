using CloudVeil;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CloudVeilInstallerUI.Models
{
    internal class WebUtil
    {
        [DllImport("ntdll.dll", SetLastError = true)]
        internal static extern uint RtlGetVersion(out OsVersionInfo versionInformation); // return type should be the NtStatus enum

        [StructLayout(LayoutKind.Sequential)]
        internal struct OsVersionInfo
        {
            private readonly uint OsVersionInfoSize;

            internal readonly uint MajorVersion;
            internal readonly uint MinorVersion;

            private readonly uint BuildNumber;

            private readonly uint PlatformId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            private readonly string CSDVersion;

            public string ToString()
            {
                return MajorVersion + "." + MinorVersion + "." + BuildNumber;
            }
        }

        public static string GetOsVersion()
        {
            var osVersionInfo = new OsVersionInfo();

            RtlGetVersion(out osVersionInfo);
            return osVersionInfo.ToString();
        }

        public static async Task<string> PostVersionStringAsync(string userId)
        {
            var userIdParts = userId.Split(':');
            if (userIdParts.Length == 2)
            {
                userId = userIdParts[1];
            } 
            var version = GetOsVersion();

            var values = new Dictionary<string, string>
              {
                  { "os", "WIN"},
                  { "os_version", version }
              };

            var content = new FormUrlEncodedContent(values);
            var client = new HttpClient();
            var response = await client.PostAsync(CompileSecrets.ServiceProviderApiPath + "/api/activations/version?acid=" + userId, content);

            var res = await response.Content.ReadAsStringAsync();
            return res;
        }
    }
}
