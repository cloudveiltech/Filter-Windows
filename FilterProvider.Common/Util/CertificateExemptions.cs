﻿/*
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
using CloudVeil.Core.Windows.Util;
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
        private static string dbPath;

        static CertificateExemptions()
        {
            dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "ssl-exemptions.db");
        }

        public CertificateExemptions()
        {
            logger = LoggerUtil.GetAppWideLogger();

            try
            {
                SqliteConnection conn = openConnection(dbPath);

                SqliteCommand command = conn.CreateCommand();

                //                command.CommandText = "CREATE TABLE IF NOT EXISTS TriggerIndex (TriggerText VARCHAR(255), CategoryId INT16)";

                command.CommandText = "CREATE TABLE IF NOT EXISTS cert_exemptions (Thumbprint VARCHAR(64), Host VARCHAR(1024), DateExempted VARCHAR(24), ExpireDate VARCHAR(24))";
                command.ExecuteNonQuery();

                command.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS idx_thumbprint_host ON cert_exemptions (Thumbprint, Host)";
                command.ExecuteNonQuery();

                connection = conn;
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
            }
        }

        private NLog.Logger logger;

        private SqliteConnection connection;
        private object connectionLock = new object();

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

        public void TrustCertificate(string host, string thumbprint)
            => TrustCertificateInternal(host, thumbprint, 1);

        private void TrustCertificateInternal(string host, string thumbprint, int triesLeft)
        {
            try
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    bool createExemptionData = false;

                    SqliteParameter dateString = new SqliteParameter("$dateExempted", DateTime.UtcNow.ToString("o"));
                    SqliteParameter param0 = new SqliteParameter("$certHash", thumbprint);
                    SqliteParameter param1 = new SqliteParameter("$host", host);

                    command.CommandText = $"SELECT Thumbprint, Host, DateExempted, ExpireDate FROM cert_exemptions WHERE Thumbprint = $certHash AND Host = $host";
                    command.Parameters.Add(param0);
                    command.Parameters.Add(param1);

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        createExemptionData = !reader.Read();
                    }

                    command.Parameters.Add(dateString);

                    if (!createExemptionData)
                    {
                        command.CommandText = $"UPDATE cert_exemptions SET DateExempted = $dateExempted, ExpireDate = NULL WHERE Thumbprint = $certHash AND Host = $host";
                        command.ExecuteNonQuery();
                    }
                    else
                    {
                        command.CommandText = $"INSERT INTO cert_exemptions (DateExempted, ExpireDate, Thumbprint, Host) VALUES ($dateExempted, NULL, $certHash, $host)";
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
                if (triesLeft > 0)
                {
                    attemptConnectionRecovery();
                    TrustCertificateInternal(host, thumbprint, triesLeft--);
                }
            }
        }

        public bool IsExempted(string host, X509Certificate2 certificate)
            => IsExemptedInternal(host, certificate, 1);

        private bool IsExemptedInternal(string host, X509Certificate2 certificate, int triesLeft)
        {
            try
            {
                lock (connectionLock)
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT Thumbprint, Host, DateExempted, ExpireDate FROM cert_exemptions WHERE Thumbprint = $certHash AND Host = $host";
                        command.Parameters.Add(new SqliteParameter("$certHash", certificate.GetCertHashString()));
                        command.Parameters.Add(new SqliteParameter("$host", host));

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
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);

                if (triesLeft > 0)
                {
                    attemptConnectionRecovery();
                    return IsExemptedInternal(host, certificate, triesLeft--);
                }
                else
                {
                    return false;
                }
            }
        }

        private void attemptConnectionRecovery()
        {
            lock (connectionLock)
            {
                try
                {
                    connection.Dispose();
                    connection = null;
                }
                catch
                {

                }

                try
                {
                    connection = openConnection(dbPath);
                }
                catch (Exception ex)
                {
                    LoggerUtil.RecursivelyLogException(logger, ex);
                }
            }
        }
    }
}
