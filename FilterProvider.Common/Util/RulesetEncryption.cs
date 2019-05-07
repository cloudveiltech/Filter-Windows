using CloudVeil;
using Filter.Platform.Common.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FilterProvider.Common.Util
{
    /// <summary>
    /// This class implements my favorite kind of security. Security. by Obscurity. Yay!
    /// </summary>
    public class RulesetEncryption
    {
        static RulesetEncryption()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        private static NLog.Logger logger;

        public static CryptoStream DecryptionStream(Stream stream)
        {
            try
            {
                RijndaelManaged rijndael = new RijndaelManaged();
                rijndael.IV = CompileSecrets.ListEncryptionInitVector;
                rijndael.Key = CompileSecrets.ListEncryptionKey;

                ICryptoTransform decryptor = rijndael.CreateDecryptor(rijndael.Key, rijndael.IV);

                CryptoStream cs = new CryptoStream(stream, decryptor, CryptoStreamMode.Read);
                return cs;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to decrypt text: {ex}");
                return null;
            }
        }

        public static CryptoStream EncryptionStream(Stream stream)
        {
            try
            {
                RijndaelManaged rijndael = new RijndaelManaged();
                rijndael.IV = CompileSecrets.ListEncryptionInitVector;
                rijndael.Key = CompileSecrets.ListEncryptionKey;

                ICryptoTransform encryptor = rijndael.CreateEncryptor(rijndael.Key, rijndael.IV);

                CryptoStream cs = new CryptoStream(stream, encryptor, CryptoStreamMode.Write);
                return cs;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to encrypt text: {ex}");
                return null;
            }
        }

        public static byte[] Decrypt(byte[] encrypted)
        {
            try
            {
                byte[] buffer = new byte[4096];

                using (MemoryStream output = new MemoryStream())
                using (MemoryStream input = new MemoryStream(encrypted))
                using (CryptoStream cs = DecryptionStream(input))
                {
                    while (true)
                    {
                        int bytesRead = cs.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            break;
                        }
                        else
                        {
                            output.Write(buffer, 0, bytesRead);
                        }
                    }

                    return output.ToArray();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to encrypt text: {ex}");
                return null;
            }
        }

        public static byte[] Encrypt(byte[] textBytes)
        {
            try
            {
                using (var output = new MemoryStream())
                using (var cs = EncryptionStream(output))
                {
                    cs.Write(textBytes, 0, textBytes.Length);
                    return output.ToArray();
                }
            }
            catch(Exception ex)
            {
                logger.Error($"Failed to encrypt text: {ex}");
                return null;
            }
        }
    }
}
