/*
* Copyright © 2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

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
using Microsoft.Data.Sqlite;
using System.Net.Security;
using Filter.Platform.Common.Util;

namespace FilterProvider.Common.Util
{
    /// <summary>
    /// This handler is for the filter provider system to hook into our certificate exemption system.
    /// </summary>
    /// <param name="request">The HttpWebRequest which caused this certificate exemption request.</param>
    /// <param name="certificate">The X509Certificate which caused this exemption request.</param>
    public delegate void AddExemptionRequestHandler(string host, string certHash, bool isTrusted);

    public class CertificateExemptions
    {
        private static string s_dbPath;

        static CertificateExemptions()
        {
            s_dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "ssl-exemptions.db");

        }

        public CertificateExemptions()
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            try
            {
                SqliteConnection conn = openConnection(s_dbPath);

                SqliteCommand command = conn.CreateCommand();

                //                command.CommandText = "CREATE TABLE IF NOT EXISTS TriggerIndex (TriggerText VARCHAR(255), CategoryId INT16)";

                command.CommandText = "CREATE TABLE IF NOT EXISTS cert_exemptions (Thumbprint VARCHAR(64), Host VARCHAR(1024), DateExempted VARCHAR(24), ExpireDate VARCHAR(24))";
                command.ExecuteNonQuery();

                command.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS idx_thumbprint_host ON cert_exemptions (Thumbprint, Host)";
                command.ExecuteNonQuery();

                m_connection = conn;
            } catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }
        }

        private NLog.Logger m_logger;

        private SqliteConnection m_connection;

        private SqliteConnection openConnection(string dbPath)
        {
            SqliteConnectionStringBuilder cb = new SqliteConnectionStringBuilder();
            cb.Cache = SqliteCacheMode.Shared;
            cb.DataSource = dbPath;
            cb.Mode = SqliteOpenMode.ReadWriteCreate;

            SqliteConnection conn = new SqliteConnection(cb.ToString());
            conn.Open();

            return conn;
        }

        private bool isReaderRowCurrentlyExempted(SqliteDataReader reader)
        {
            // if DateExempted != NULL && (ExpireDate == NULL || ExpireDate > CurrentDate)
            if (!reader.IsDBNull(2))
            {
                DateTime dateExempted;
                if (!DateTime.TryParse(reader.GetString(2), out dateExempted))
                {
                    return false;
                }

                if (reader.IsDBNull(3))
                {
                    // ExpireDate == NULL
                    return true;
                }
                else
                {
                    DateTime expireDate;
                    if (!DateTime.TryParse(reader.GetString(3), out expireDate))
                    {
                        return false;
                    }
                    else if (DateTime.Now > expireDate)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            else
            {
                return false;
            }
        }

        public event AddExemptionRequestHandler OnAddCertificateExemption;

        public void TrustCertificate(string host, string certHash)
        {
            try
            {
                using (SqliteCommand command = m_connection.CreateCommand())
                {
                    SqliteParameter dateString = new SqliteParameter("$dateString", DateTime.UtcNow.ToString("o"));
                    SqliteParameter param0 = new SqliteParameter("$certHash", certHash);
                    SqliteParameter param1 = new SqliteParameter("$host", host);

                    command.CommandText = $"UPDATE cert_exemptions SET DateExempted = $dateString WHERE Thumbprint = $certHash AND Host = $host";
                    command.Parameters.Add(dateString);
                    command.Parameters.Add(param0);
                    command.Parameters.Add(param1);

                    command.ExecuteNonQuery();

                    OnAddCertificateExemption?.Invoke(host, certHash, true);
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }
        }

        public void AddExemptionRequest(HttpWebRequest request, X509Certificate certificate)
        {
            try
            {
                using (SqliteCommand command = m_connection.CreateCommand())
                {
                    bool clearExemptionData = false;
                    bool createExemptionData = false;

                    SqliteParameter param0 = new SqliteParameter("$certHash", certificate.GetCertHashString());
                    SqliteParameter param1 = new SqliteParameter("$host", request.Host);

                    command.CommandText = $"SELECT Thumbprint, Host, DateExempted, ExpireDate FROM cert_exemptions WHERE Thumbprint = $certHash AND Host = $host";
                    command.Parameters.Add(param0);
                    command.Parameters.Add(param1);
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            clearExemptionData = !isReaderRowCurrentlyExempted(reader);
                        }
                        else
                        {
                            createExemptionData = true;
                        }
                    }

                    if (clearExemptionData)
                    {
                        command.CommandText = $"UPDATE cert_exemptions SET DateExempted = NULL, ExpireDate = NULL WHERE Thumbprint = $certHash AND Host = $host";
                        command.ExecuteNonQuery();

                        OnAddCertificateExemption?.Invoke(request.Host, certificate.GetCertHashString(), false);
                    }
                    else if (createExemptionData)
                    {
                        command.CommandText = $"INSERT INTO cert_exemptions (DateExempted, ExpireDate, Thumbprint, Host) VALUES (NULL, NULL, $certHash, $host)";
                        command.ExecuteNonQuery();

                        OnAddCertificateExemption?.Invoke(request.Host, certificate.GetCertHashString(), false);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }
        }

        public bool IsExempted(HttpWebRequest request, X509Certificate certificate)
        {
            try
            {
                using (SqliteCommand command = m_connection.CreateCommand())
                {
                    command.CommandText = "SELECT Thumbprint, Host, DateExempted, ExpireDate FROM cert_exemptions WHERE Thumbprint = $certHash AND Host = $host";
                    command.Parameters.Add(new SqliteParameter("$certHash", certificate.GetCertHashString()));
                    command.Parameters.Add(new SqliteParameter("$host", request.Host));

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            if (isReaderRowCurrentlyExempted(reader))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ex);
                return false;
            }
        }

        public bool CertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            HttpWebRequest request = (HttpWebRequest)sender;

            try
            {
                if (IsExempted(request, certificate))
                {
                    return true;
                }
                else
                {
                    AddExemptionRequest(request, certificate);
                }
            }
            catch(Exception ex)
            {
                m_logger.Error(ex);
            }

            return false;
        }
    }
}
