using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Te.Citadel.Util
{
    public static class X509Extensions
    {
        public static string ExportToPem(this X509Certificate2 cert)
        {
            var builder = new StringBuilder();

            builder.AppendLine(cert.Subject);
            builder.AppendLine(new string('=', cert.Subject.Length));
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----").AppendLine();

            return builder.ToString();
        }
    }
}
