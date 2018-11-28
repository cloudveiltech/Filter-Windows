/*
The MIT License (MIT)

Copyright (c) 2015 titanium007

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

// From https://github.com/justcoding121/Titanium-Web-Proxy

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Filter.Platform.Common.Util;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace FilterProvider.Common.Proxy.Certificate
{
    /// <summary>
    ///     Implements certificate generation operations.
    /// </summary>
    public class BCCertificateMaker
    {
        private const int certificateValidDays = 1825;
        private const int certificateGraceDays = 366;

        // The FriendlyName value cannot be set on Unix.
        // Set this flag to true when exception detected to avoid further exceptions
        private static bool doNotSetFriendlyName;

        internal static readonly Regex CNRemoverRegex =
            new Regex(@"^CN\s*=\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private NLog.Logger m_logger;

        public BCCertificateMaker()
        {
            m_logger = LoggerUtil.GetAppWideLogger();
        }

        /// <summary>
        ///     Makes the certificate.
        /// </summary>
        /// <param name="sSubjectCn">The s subject cn.</param>
        /// <param name="isRoot">if set to <c>true</c> [is root].</param>
        /// <param name="signingCert">The signing cert.</param>
        /// <returns>X509Certificate2 instance.</returns>
        public X509Certificate2 MakeCertificate(string sSubjectCn, bool isRoot, X509Certificate2 signingCert = null, AsymmetricCipherKeyPair subjectKeyPair = null)
        {
            return makeCertificateInternal(sSubjectCn, isRoot, true, signingCert, subjectKeyPair);
        }

        public static AsymmetricCipherKeyPair CreateKeyPair(int keyStrength)
        {
            var randomGenerator = new CryptoApiRandomGenerator();
            var secureRandom = new SecureRandom(randomGenerator);

            var keyGenerationParameters = new KeyGenerationParameters(secureRandom, keyStrength);
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);
            var subjectKeyPair = keyPairGenerator.GenerateKeyPair();

            return subjectKeyPair;
        }

        /// <summary>
        ///     Generates the certificate.
        /// </summary>
        /// <param name="subjectName">Name of the subject.</param>
        /// <param name="issuerName">Name of the issuer.</param>
        /// <param name="validFrom">The valid from.</param>
        /// <param name="validTo">The valid to.</param>
        /// <param name="keyStrength">The key strength.</param>
        /// <param name="signatureAlgorithm">The signature algorithm.</param>
        /// <param name="issuerPrivateKey">The issuer private key.</param>
        /// <param name="hostName">The host name</param>
        /// <returns>X509Certificate2 instance.</returns>
        /// <exception cref="PemException">Malformed sequence in RSA private key</exception>
        private static X509Certificate2 generateCertificate(string hostName,
            string subjectName,
            string issuerName, DateTime validFrom,
            DateTime validTo, int keyStrength = 2048,
            string signatureAlgorithm = "SHA256WithRSA",
            AsymmetricKeyParameter issuerPrivateKey = null,
            AsymmetricCipherKeyPair subjectKeyPair = null)
        {
            // Generating Random Numbers
            var randomGenerator = new CryptoApiRandomGenerator();
            var secureRandom = new SecureRandom(randomGenerator);

            // The Certificate Generator
            var certificateGenerator = new X509V3CertificateGenerator();

            // Serial Number
            var serialNumber =
                BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(long.MaxValue), secureRandom);
            certificateGenerator.SetSerialNumber(serialNumber);

            // Issuer and Subject Name
            var subjectDn = new X509Name(subjectName);
            var issuerDn = new X509Name(issuerName);
            certificateGenerator.SetIssuerDN(issuerDn);
            certificateGenerator.SetSubjectDN(subjectDn);

            certificateGenerator.SetNotBefore(validFrom);
            certificateGenerator.SetNotAfter(validTo);

            if (hostName != null)
            {
                // add subject alternative names
                var subjectAlternativeNames = new Asn1Encodable[] { new GeneralName(GeneralName.DnsName, hostName) };

                var subjectAlternativeNamesExtension = new DerSequence(subjectAlternativeNames);
                certificateGenerator.AddExtension(X509Extensions.SubjectAlternativeName.Id, false,
                    subjectAlternativeNamesExtension);
            }

            // Subject Public Key
            if(subjectKeyPair == null)
            {
                subjectKeyPair = CreateKeyPair(keyStrength);
            }

            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            // Set certificate intended purposes to only Server Authentication
            certificateGenerator.AddExtension(X509Extensions.ExtendedKeyUsage.Id, false,
                new ExtendedKeyUsage(KeyPurposeID.IdKPServerAuth));
            if (issuerPrivateKey == null)
            {
                certificateGenerator.AddExtension(X509Extensions.BasicConstraints.Id, true, new BasicConstraints(true));
            }

            var signatureFactory = new Asn1SignatureFactory(signatureAlgorithm,
                issuerPrivateKey ?? subjectKeyPair.Private, secureRandom);

            // Self-sign the certificate
            var certificate = certificateGenerator.Generate(signatureFactory);

            // Corresponding private key
            var privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(subjectKeyPair.Private);

            var seq = (Asn1Sequence)Asn1Object.FromByteArray(privateKeyInfo.ParsePrivateKey().GetDerEncoded());

            if (seq.Count != 9)
            {
                throw new PemException("Malformed sequence in RSA private key");
            }

            var rsa = RsaPrivateKeyStructure.GetInstance(seq);
            var rsaparams = new RsaPrivateCrtKeyParameters(rsa.Modulus, rsa.PublicExponent, rsa.PrivateExponent,
                rsa.Prime1, rsa.Prime2, rsa.Exponent1,
                rsa.Exponent2, rsa.Coefficient);

#if NET45
            // Set private key onto certificate instance
            var x509Certificate = new X509Certificate2(certificate.GetEncoded());
            x509Certificate.PrivateKey = DotNetUtilities.ToRSA(rsaparams);
#else
            var x509Certificate = withPrivateKey(certificate, rsaparams);
#endif

            if (!doNotSetFriendlyName)
            {
                try
                {
                    x509Certificate.FriendlyName = CNRemoverRegex.Replace(subjectName, string.Empty);
                }
                catch (PlatformNotSupportedException)
                {
                    doNotSetFriendlyName = true;
                }
            }

            return x509Certificate;
        }

        private static X509Certificate2 withPrivateKey(X509Certificate certificate, AsymmetricKeyParameter privateKey)
        {
            const string password = "password";
            Pkcs12Store store = null;

            Pkcs12StoreBuilder builder = new Pkcs12StoreBuilder();
            builder.SetUseDerEncoding(true);
            store = builder.Build();

            var entry = new X509CertificateEntry(certificate);
            store.SetCertificateEntry(certificate.SubjectDN.ToString(), entry);

            store.SetKeyEntry(certificate.SubjectDN.ToString(), new AsymmetricKeyEntry(privateKey), new[] { entry });
            using (var ms = new MemoryStream())
            {
                store.Save(ms, password.ToCharArray(), new SecureRandom(new CryptoApiRandomGenerator()));

                return new X509Certificate2(ms.ToArray(), password, X509KeyStorageFlags.Exportable);
            }
        }

        /// <summary>
        ///     Makes the certificate internal.
        /// </summary>
        /// <param name="isRoot">if set to <c>true</c> [is root].</param>
        /// <param name="hostName">hostname for certificate</param>
        /// <param name="subjectName">The full subject.</param>
        /// <param name="validFrom">The valid from.</param>
        /// <param name="validTo">The valid to.</param>
        /// <param name="signingCertificate">The signing certificate.</param>
        /// <returns>X509Certificate2 instance.</returns>
        /// <exception cref="System.ArgumentException">
        ///     You must specify a Signing Certificate if and only if you are not creating a
        ///     root.
        /// </exception>
        private X509Certificate2 makeCertificateInternal(bool isRoot,
            string hostName, string subjectName,
            DateTime validFrom, DateTime validTo, X509Certificate2 signingCertificate, AsymmetricCipherKeyPair subjectKeyPair = null)
        {
            if (isRoot != (null == signingCertificate))
            {
                throw new ArgumentException(
                    "You must specify a Signing Certificate if and only if you are not creating a root.",
                    nameof(signingCertificate));
            }

            if (isRoot)
            {
                return generateCertificate(null, subjectName, subjectName, validFrom, validTo, subjectKeyPair: subjectKeyPair);
            }

            var kp = DotNetUtilities.GetKeyPair(signingCertificate.PrivateKey);
            return generateCertificate(hostName, subjectName, signingCertificate.Subject, validFrom, validTo,
                issuerPrivateKey: kp.Private);
        }

        /// <summary>
        ///     Makes the certificate internal.
        /// </summary>
        /// <param name="subject">The s subject cn.</param>
        /// <param name="isRoot">if set to <c>true</c> [is root].</param>
        /// <param name="switchToMtaIfNeeded">if set to <c>true</c> [switch to MTA if needed].</param>
        /// <param name="signingCert">The signing cert.</param>
        /// <param name="cancellationToken">Task cancellation token</param>
        /// <returns>X509Certificate2.</returns>
        private X509Certificate2 makeCertificateInternal(string subject, bool isRoot,
            bool switchToMtaIfNeeded, X509Certificate2 signingCert = null, AsymmetricCipherKeyPair subjectKeyPair = null,
            CancellationToken cancellationToken = default)
        {
#if NET45
            if (switchToMtaIfNeeded && Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA)
            {
                X509Certificate2 certificate = null;
                using (var manualResetEvent = new ManualResetEventSlim(false))
                {
                    ThreadPool.QueueUserWorkItem(o =>
                    {
                        try
                        {
                            certificate = makeCertificateInternal(subject, isRoot, false, signingCert);
                        }
                        catch (Exception ex)
                        {
                            exceptionFunc(new Exception("Failed to create BC certificate", ex));
                        }

                        if (!cancellationToken.IsCancellationRequested)
                        {
                            manualResetEvent.Set();
                        }
                    });

                    manualResetEvent.Wait(TimeSpan.FromMinutes(1), cancellationToken);
                }

                return certificate;
            }
#endif

            return makeCertificateInternal(isRoot, subject, $"CN={subject}",
                DateTime.UtcNow.AddDays(-certificateGraceDays), DateTime.UtcNow.AddDays(certificateValidDays),
                isRoot ? null : signingCert, subjectKeyPair: subjectKeyPair);
        }

        public static void ExportDotNetCertificate(X509Certificate2 cert, TextWriter outputStream)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(cert.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");

            outputStream.Write(builder.ToString());
        }

        public static void ExportPrivateKey(AsymmetricKeyParameter privKey, TextWriter outputStream)
        {
            PrivateKeyInfo info = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privKey);

            var seq = (Asn1Sequence)Asn1Object.FromByteArray(info.ParsePrivateKey().GetDerEncoded());

            if (seq.Count != 9)
            {
                throw new PemException("Malformed sequence in RSA private key");
            }

            var rsa = RsaPrivateKeyStructure.GetInstance(seq);
            var parameters = new RsaPrivateCrtKeyParameters(rsa.Modulus, rsa.PublicExponent, rsa.PrivateExponent,
                rsa.Prime1, rsa.Prime2, rsa.Exponent1,
                rsa.Exponent2, rsa.Coefficient);

            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write((byte)0x30); // SEQUENCE
                using (var innerStream = new MemoryStream())
                {
                    var innerWriter = new BinaryWriter(innerStream);
                    EncodeIntegerBigEndian(innerWriter, new byte[] { 0x00 }); // Version
                    EncodeIntegerBigEndian(innerWriter, parameters.Modulus.ToByteArray());
                    EncodeIntegerBigEndian(innerWriter, parameters.PublicExponent.ToByteArray());
                    EncodeIntegerBigEndian(innerWriter, parameters.Exponent.ToByteArray());
                    EncodeIntegerBigEndian(innerWriter, parameters.P.ToByteArray());
                    EncodeIntegerBigEndian(innerWriter, parameters.Q.ToByteArray());
                    EncodeIntegerBigEndian(innerWriter, parameters.DP.ToByteArray());
                    EncodeIntegerBigEndian(innerWriter, parameters.DQ.ToByteArray());
                    EncodeIntegerBigEndian(innerWriter, parameters.QInv.ToByteArray());
                    var length = (int)innerStream.Length;
                    EncodeLength(writer, length);
                    writer.Write(innerStream.GetBuffer(), 0, length);
                }

                var base64 = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length).ToCharArray();
                outputStream.WriteLine("-----BEGIN RSA PRIVATE KEY-----");
                // Output as Base64 with lines chopped at 64 characters
                for (var i = 0; i < base64.Length; i += 64)
                {
                    outputStream.WriteLine(base64, i, Math.Min(64, base64.Length - i));
                }
                outputStream.WriteLine("-----END RSA PRIVATE KEY-----");
            }
        }

        private static void EncodeLength(BinaryWriter stream, int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException("length", "Length must be non-negative");
            if (length < 0x80)
            {
                // Short form
                stream.Write((byte)length);
            }
            else
            {
                // Long form
                var temp = length;
                var bytesRequired = 0;
                while (temp > 0)
                {
                    temp >>= 8;
                    bytesRequired++;
                }
                stream.Write((byte)(bytesRequired | 0x80));
                for (var i = bytesRequired - 1; i >= 0; i--)
                {
                    stream.Write((byte)(length >> (8 * i) & 0xff));
                }
            }
        }

        private static void EncodeIntegerBigEndian(BinaryWriter stream, byte[] value, bool forceUnsigned = true)
        {
            stream.Write((byte)0x02); // INTEGER
            var prefixZeros = 0;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != 0) break;
                prefixZeros++;
            }
            if (value.Length - prefixZeros == 0)
            {
                EncodeLength(stream, 1);
                stream.Write((byte)0);
            }
            else
            {
                if (forceUnsigned && value[prefixZeros] > 0x7f)
                {
                    // Add a prefix zero to force unsigned if the MSB is 1
                    EncodeLength(stream, value.Length - prefixZeros + 1);
                    stream.Write((byte)0);
                }
                else
                {
                    EncodeLength(stream, value.Length - prefixZeros);
                }
                for (var i = prefixZeros; i < value.Length; i++)
                {
                    stream.Write(value[i]);
                }
            }
        }
    }
}
