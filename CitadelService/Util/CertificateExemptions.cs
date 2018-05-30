using CitadelCore.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using Citadel.Core.Windows.Util;

namespace CitadelService.Util
{
    public class CertificateExemptions : ICertificateExemptions
    {
        private string prepareDirectory()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "exemption-requests");

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        }

        static string Hash(string input)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(hash.Length * 2);

                foreach (byte b in hash)
                {
                    // can be "x2" if you want lowercase
                    sb.Append(b.ToString("X2"));
                }

                return sb.ToString();
            }
        }

        private string getCertPath(string host, X509Certificate certificate)
        {
            return Path.Combine(prepareDirectory(), $"{Hash(host)}-{certificate.GetCertHashString()}");
        }

        public void AddExemptionRequest(HttpWebRequest request, X509Certificate certificate)
        {
            string path = prepareDirectory();
            string certPath = getCertPath(request.Host, certificate);

            if(File.Exists(certPath) && !IsExempted(request, certificate))
            {
                File.Delete(certPath);
            }

            if(!File.Exists(certPath))
            {
                using (FileStream stream = File.Open(certPath, FileMode.Create))
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.WriteLine($"{Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Host))}|0");
                }
            }
        }

        public bool IsExempted(HttpWebRequest request, X509Certificate certificate)
        {
            string path = prepareDirectory();

            string certPath = getCertPath(request.Host, certificate);

            if (!File.Exists(certPath))
            {
                return false;
            }

            try
            {
                using (FileStream stream = File.OpenRead(certPath))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string exemptionText = reader.ReadToEnd();

                    string[] parts = exemptionText.Split('|');

                    string host = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                    int isExempted = 0;

                    if(host != request.Host)
                    {
                        return false;
                    }

                    if(!int.TryParse(parts[1], out isExempted))
                    {
                        return false;
                    }

                    return isExempted > 0;
                }
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), ex);
                return false;
            }

            return true;
        }
    }
}
