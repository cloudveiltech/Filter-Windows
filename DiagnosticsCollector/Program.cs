/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.IPC;
using Citadel.IPC.Messages;
using Filter.Platform.Common.Types;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticsCollector
{
    class Program
    {
        static SqliteConnection GetDiagnosticsSqlConnection(string filename = null, bool createTables = true)
        {
            if(filename == null)
            {
                filename = $"diag-{DateTime.Now.ToString("dd-MM-yyyy-hh-mm-ss")}";
            }

            SqliteConnection connection = new SqliteConnection($"Filename={filename}");
            connection.Open();

            // Initialize database table.
            if (createTables)
            {
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE IF NOT EXISTS diagnostics (" +
                        "id INT PRIMARY KEY," +
                        "client_request_body BLOB, " +
                        "server_request_body BLOB, " +
                        "client_request_headers TEXT, " +
                        "server_request_headers TEXT," +
                        "client_request_uri TEXT," +
                        "server_request_uri TEXT," +
                        "server_response_body BLOB," +
                        "server_response_headers BLOB," +
                        "status_code INT," +
                        "date_started TEXT," +
                        "date_ended TEXT," +
                        "host TEXT," +
                        "request_uri TEXT," +
                        "diagnostics_type INT" +
                        ");";

                    command.ExecuteNonQuery();
                }
            }

            return connection;
        }

        static IPCClient ipcClient;

        static void Main(string[] args)
        {
            Citadel.Core.Windows.Platform.Init();

            Console.WriteLine("This program was designed to be a diagnostics collector for CloudVeil for Windows.");
            Console.WriteLine("Use this program when you want to collect data on sites that aren't behaving properly while the filter is running.");
            Console.WriteLine("Here are the common commands:");
            Console.WriteLine("\tstart-diag [filename]: Use this to start collecting diagnostics information from CloudVeil for Windows");
            Console.WriteLine("\tstop-diag: Stops the diagnostic data collection and saves it to the file specified by start-diag");
            Console.WriteLine("\tquit: Quits the program.");
            Console.WriteLine("Use 'help' to get a comprehensive list of commands.");

            bool quit = false;

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            //System.AppDomain.CurrentDomain.
            IPCClient.InitDefault();
            ipcClient = IPCClient.Default;

            Console.WriteLine("Waiting for connection...");
            ipcClient.WaitForConnection();
            Console.WriteLine("Connected...");

            while(!quit)
            {
                Console.Write("> ");
                string command = Console.ReadLine();

                string[] commandParts = command.Split(' ');

                switch(commandParts[0])
                {
                    case "start-diag":
                        {
                            string filename = null;

                            if (commandParts.Length > 1)
                            {
                                filename = string.Join(" ", commandParts.Skip(1).ToArray());
                            }

                            StartDiagnostics(filename);
                            break;
                        }

                    case "admin-test":
                        {
                            string filename = null, arguments = null;
                            Console.Write("Enter exe filename: ");
                            filename = Console.ReadLine();

                            Console.Write("Enter arguments: ");
                            arguments = Console.ReadLine();

                            ipcClient.Send<MyProcessInfo>(IpcCall.AdministratorStart, new MyProcessInfo()
                            {
                                Filename = filename,
                                Arguments = arguments 
                            });

                            break;
                        }

                    case "load-diag":
                        {
                            string filename = null;

                            if (commandParts.Length > 1)
                            {
                                filename = string.Join(" ", commandParts.Skip(1).ToArray());
                            }
                            else
                            {
                                Console.WriteLine("load-diag requires a filename.");
                                break;
                            }

                            LoadDiagnostics(filename);
                            break;
                        }
                        

                    case "stop-diag":
                        StopDiagnostics();
                        break;

                    case "compare-client-server-requests":
                        {
                            string filename = null;

                            if (commandParts.Length > 1)
                            {
                                filename = string.Join(" ", commandParts.Skip(1).ToArray());
                            }

                            CompareClientServerRequestHeaders(filename);
                        }
                        break;

                    case "help":
                        Help();
                        break;

                    case "quit":
                        quit = true;
                        break;

                }
            }
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            StopDiagnostics();
        }

        static void Help()
        {
            Console.WriteLine("Here are all the available commands:");
            Console.WriteLine("\tstart-diag [filename]: Use this to start collecting diagnostics information from CloudVeil for Windows");
            Console.WriteLine("\tstop-diag: Stopts the diagnostic data collection and saves it to the file specified by start-diag");
            Console.WriteLine("\tload-diag [filename]: Loads diagnostics data from the specified file.");
            /*Console.WriteLine("\tunexpected-empty-responses: Prints a list of all responses with a content-length of 0 and a status code other than 204");
            Console.WriteLine("\tall-empty-responses: Prints a list of all responses with a content-length of 0, including 204s");
            Console.WriteLine("\terror-responses: Prints a list of all responses which returned an error status.");*/
            Console.WriteLine("\tcompare-client-server-requests: Prints a list of all requests whose client and server sides did not match each other.");
        }

        private static SqliteParameter getParameter(string name, object value) => new SqliteParameter(name, value ?? DBNull.Value);

        static void AddDiagnosticsEntryV1(SqliteConnection connection, DiagnosticsInfoV1 info)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO diagnostics (client_request_body, server_request_body, client_request_headers," +
                        "server_request_headers, client_request_uri, server_request_uri, server_response_body, server_response_headers," +
                        "status_code, date_started, date_ended, host, request_uri, diagnostics_type) VALUES" +
                        "($client_request_body, $server_request_body, $client_request_headers," +
                        "$server_request_headers, $client_request_uri, $server_request_uri, $server_response_body, $server_response_headers," +
                        "$status_code, $date_started, $date_ended, $host, $request_uri, $diagnostics_type)";

                command.Parameters.Add(getParameter("$client_request_body", info.ClientRequestBody));
                command.Parameters.Add(getParameter("$server_request_body", info.ServerRequestBody));
                command.Parameters.Add(getParameter("$client_request_headers", info.ClientRequestHeaders));
                command.Parameters.Add(getParameter("$server_request_headers", info.ServerRequestHeaders));
                command.Parameters.Add(getParameter("$client_request_uri", info.ClientRequestUri?.ToString()));
                command.Parameters.Add(getParameter("$server_request_uri", info.ServerRequestUri?.ToString()));
                command.Parameters.Add(getParameter("$server_response_body", info.ServerResponseBody));
                command.Parameters.Add(getParameter("$server_response_headers", info.ServerResponseHeaders));
                command.Parameters.Add(getParameter("$status_code", info.StatusCode));
                command.Parameters.Add(getParameter("$date_started", info.DateStarted.ToString("o")));
                command.Parameters.Add(getParameter("$date_ended", info.DateEnded.ToString("o")));
                command.Parameters.Add(getParameter("$host", info.Host));
                command.Parameters.Add(getParameter("$request_uri", info.RequestUri));
                command.Parameters.Add(getParameter("$diagnostics_type", info.DiagnosticsType));

                command.ExecuteNonQuery();
            }
        }

        static SqliteConnection currentConnection = null;
        static void StartDiagnostics(string filename)
        {
            ipcClient.SendDiagnosticsEnable(true);

            SqliteConnection connection = GetDiagnosticsSqlConnection(filename);
            currentConnection = connection;

            ipcClient.OnDiagnosticsInfo = (msg) =>
            {
                switch(msg.ObjectVersion)
                {
                    case Citadel.IPC.Messages.DiagnosticsVersion.V1:
                        var info = msg.Info as Citadel.IPC.Messages.DiagnosticsInfoV1;

                        AddDiagnosticsEntryV1(connection, info);

                        break;
                }
            };
        }

        static void StopDiagnostics()
        {
            ipcClient.SendDiagnosticsEnable(false);
            ipcClient.OnDiagnosticsInfo = null;
        }

        static void LoadDiagnostics(string filename)
        {
            if(!File.Exists(filename))
            {
                Console.WriteLine($"File '{filename}' does not exist.");
            }

            try
            {
                SqliteConnection connection = GetDiagnosticsSqlConnection(filename, false);
                currentConnection = connection;
                Console.WriteLine("Diagnostics loaded");

            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load diagnostics: {0}", ex);
            }
        }

        static void CompareClientServerRequestHeaders(string filename = null)
        {
            TextWriter writer = null;

            try
            {
                if (filename == null)
                {
                    writer = Console.Out;
                }
                else
                {
                    writer = new StreamWriter(filename);
                }

                List<HeaderComparisons> comparisonResults = new List<HeaderComparisons>();

                using (SqliteCommand command = currentConnection.CreateCommand())
                {
                    command.CommandText = "SELECT client_request_headers, server_request_headers, client_request_uri FROM diagnostics;";

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string clientHeaders = reader.IsDBNull(0) ? null : reader.GetString(0);
                            string serverHeaders = reader.IsDBNull(1) ? null : reader.GetString(1);
                            string clientUri = reader.IsDBNull(2) ? null : reader.GetString(2);

                            var clientHeaderList = HeaderParser.Parse(clientHeaders);
                            var serverHeaderList = HeaderParser.Parse(serverHeaders);

                            if (clientHeaderList == null || serverHeaderList == null)
                            {
                                comparisonResults.Add(new HeaderComparisons()
                                {
                                    Uri = clientUri,
                                    Comparisons = null
                                });
                            }
                            else
                            {
                                var headerComparisonResults = HeaderParser.Compare(clientHeaderList, serverHeaderList);

                                var headerComparisons = new HeaderComparisons()
                                {
                                    Uri = clientUri,
                                    Comparisons = headerComparisonResults
                                };

                                comparisonResults.Add(headerComparisons);
                            }
                        }
                    }
                }

                StringBuilder completeReportBuilder = new StringBuilder();

                StringBuilder reportBuilder = new StringBuilder();
                foreach (var result in comparisonResults)
                {
                    reportBuilder.AppendLine($"URI {result.Uri}");

                    int linesPrinted = 0;

                    if (result.Comparisons == null)
                    {
                        reportBuilder.AppendLine($"Either client or server request headers was null.");
                        linesPrinted++;
                    }
                    else
                    {
                        foreach (var comparison in result.Comparisons)
                        {
                            switch (comparison.ModificationType)
                            {
                                case ModificationType.Added:
                                    reportBuilder.AppendLine($"Added by proxy :: {comparison.HeaderKey}");
                                    linesPrinted++;

                                    foreach (var value in comparison.ValueComparisons)
                                    {
                                        reportBuilder.AppendLine($"\t{value.HeaderKey}: {value.Value}");
                                        linesPrinted++;
                                    }
                                    break;

                                case ModificationType.Removed:
                                    reportBuilder.AppendLine($"Removed by proxy :: {comparison.HeaderKey}");
                                    linesPrinted++;

                                    foreach (var value in comparison.ValueComparisons)
                                    {
                                        reportBuilder.AppendLine($"\t{value.HeaderKey}: {value.Value}");
                                        linesPrinted++;
                                    }
                                    break;

                                case ModificationType.BothLists:
                                    StringBuilder innerBuilder = new StringBuilder();
                                    int innerLinesPrinted = 0;
                                    foreach (var value in comparison.ValueComparisons)
                                    {
                                        if (value.ModificationType != ModificationType.BothLists)
                                        {
                                            innerBuilder.AppendLine($"\t{value.HeaderKey}: {value.Value}");
                                            innerLinesPrinted++;
                                        }
                                    }

                                    if (innerLinesPrinted > 0)
                                    {
                                        reportBuilder.AppendLine($"Header values modified by proxy :: {comparison.HeaderKey}");
                                        linesPrinted++;

                                        reportBuilder.Append(innerBuilder);
                                        linesPrinted += innerLinesPrinted;
                                    }

                                    break;

                            }
                        }
                    }

                    if (linesPrinted > 0)
                    {
                        reportBuilder.AppendLine("--------------------------------------------");
                        linesPrinted++;

                        completeReportBuilder.Append(reportBuilder);
                    }
                }

                writer.Write(completeReportBuilder.ToString());
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
            finally
            {
                if(filename != null)
                {
                    writer.Close();
                }
            }
        }
    }
}
