/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using CitadelService.Services;
using NLog;
using System.Diagnostics;

namespace CitadelService.Data.Filtering
{
    /// <summary>
    /// The BagOfTextTriggers class serves the sole purpose of storing bits of text in a database,
    /// and then reponding to queries with answers about whether this class has stored the text in
    /// question.
    /// </summary>
    /// <remarks>
    /// This class is a super special snowflake that will unpredictably get triggered.
    /// </remarks>
    internal class BagOfTextTriggers : IDisposable
    {
        /// <summary>
        /// Our Sqlite connection.
        /// </summary>
        private SqliteConnection m_connection;

        /// <summary>
        /// Holds whether or not this instance has any triggers loaded.
        /// </summary>
        private volatile bool m_hasTriggers = false;

        /// <summary>
        /// Get whether or not this instance has any triggers loaded.
        /// </summary>
        public bool HasTriggers
        {
            get
            {
                return m_hasTriggers;
            }
        }

        private Logger m_logger;

        /// <summary>
        /// Constructs a new BagOfTextTriggers.
        /// </summary>
        /// <param name="dbAbsolutePath">
        /// The absolute path where the database exists or should be created.
        /// </param>
        /// <param name="overwrite">
        /// If true, and a file exists at the supplied absolute db path, it will be deleted first.
        /// Default is true.
        /// </param>
        /// <param name="useMemory">
        /// If true, the database will be created as a purely in-memory database.
        /// </param>
        public BagOfTextTriggers(string dbAbsolutePath, bool overwrite = true, bool useMemory = false, Logger logger = null)
        {
            m_logger = logger;

            if(!useMemory && overwrite && File.Exists(dbAbsolutePath))
            {
                File.Delete(dbAbsolutePath);
            }

            bool isNew = !File.Exists(dbAbsolutePath);

            if(useMemory)
            {
                var version = typeof(BagOfTextTriggers).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version;
                var rnd = new Random();
                var rndNum = rnd.Next();
                var generatedDbName = string.Format("{0} {1} - {2}", nameof(BagOfTextTriggers), version, rndNum);

                // "Data Source = :memory:; Cache = shared;"
                var cb = new SqliteConnectionStringBuilder();
                cb.Mode = SqliteOpenMode.Memory;
                cb.Cache = SqliteCacheMode.Shared;
                cb.DataSource = generatedDbName;
                m_connection = new SqliteConnection(cb.ToString());

                //m_connection = new SqliteConnection("FullUri=file::memory:?cache=shared;Version=3;");
            }
            else
            {
                var cb = new SqliteConnectionStringBuilder();
                cb.Mode = SqliteOpenMode.ReadWriteCreate;
                cb.Cache = SqliteCacheMode.Shared;
                cb.DataSource = dbAbsolutePath;

                m_connection = new SqliteConnection(cb.ToString());
                //m_connection = new SqliteConnection(string.Format("Data Source={0};Version=3;", dbAbsolutePath));
            }

            //m_connection.Flags = SQLiteConnectionFlags.UseConnectionPool | SQLiteConnectionFlags.NoConvertSettings | SQLiteConnectionFlags.NoVerifyTypeAffinity;
            m_connection.Open();

            ConfigureDatabase();

            CreateTables();
        }

        /// <summary>
        /// Configures the database page size, cache size for optimal performance according to our
        /// needs.
        /// </summary>
        private async void ConfigureDatabase()
        {
            using(var cmd = m_connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA page_size=65536;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA cache_size=-65536;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA soft_heap_limit=131072;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA synchronous=OFF;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA journal_mode=MEMORY;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA locking_mode=NORMAL;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA temp_store=2;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA ignore_check_constraints=TRUE;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA cell_size_check=FALSE;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA cache_spill=FALSE;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA automatic_index=FALSE;";
                await cmd.ExecuteNonQueryAsync();

                cmd.CommandText = "PRAGMA busy_timeout=20000;";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Creates needed tables on the source database if they do not exist.
        /// </summary>
        private async void CreateTables()
        {
            using(var tsx = m_connection.BeginTransaction())
            using(var command = m_connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE IF NOT EXISTS TriggerIndex (TriggerText VARCHAR(255), CategoryId INT16)";
                await command.ExecuteNonQueryAsync();

                tsx.Commit();
            }
        }

        /// <summary>
        /// Creates needed indexes on the database if they do not exist.
        /// </summary>
        private void CreatedIndexes()
        {
            using(var command = m_connection.CreateCommand())
            {
                command.CommandText = "CREATE INDEX IF NOT EXISTS trigger_index ON TriggerIndex (TriggerText)";
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Finalize the database for read only access. This presently builds indexes if they do not
        /// exist, and is meant to be called AFTER all bulk insertions are complete. Note that
        /// calling this command does not rebuild the Sqlite connection to enforce read-only mode.
        /// Write access is still possible after calling.
        /// </summary>
        public void FinalizeForRead()
        {
            CreatedIndexes();
        }

        /// <summary>
        /// Loads each line read from the supplied stream as a new trigger and stores it.
        /// </summary>
        /// <param name="inputStream">
        /// The source input stream where each line represents a unique trigger.
        /// </param>
        /// <param name="categoryId">
        /// The category ID that all loaded triggers are to be assigned.
        /// </param>
        /// <returns>
        /// The total number of triggers loaded from the supplied input stream.
        /// </returns>
        public async Task<int> LoadStoreFromStream(Stream inputStream, short categoryId)
        {
            int loaded = 0;
            using(var transaction = m_connection.BeginTransaction())
            {
                using(var cmd = m_connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO TriggerIndex VALUES ($rigger, $categoryId)";
                    var domainParam = new SqliteParameter("$rigger", DbType.String);
                    var categoryIdParam = new SqliteParameter("$categoryId", DbType.Int16);
                    cmd.Parameters.Add(domainParam);
                    cmd.Parameters.Add(categoryIdParam);

                    string line = null;
                    using(var sw = new StreamReader(inputStream))
                    {
                        while((line = await sw.ReadLineAsync()) != null)
                        {
                            if(line.Trim().Length > 0)
                            {
                                cmd.Parameters[0].Value = line.Trim();
                                cmd.Parameters[1].Value = categoryId;
                                await cmd.ExecuteNonQueryAsync();
                                ++loaded;
                            }
                        }
                    }
                }

                transaction.Commit();
            }

            m_hasTriggers = loaded > 0;

            return loaded;
        }

        /// <summary>
        /// Checks to see if the string supplied exactly matches a known trigger.
        /// </summary>
        /// <param name="input">
        /// </param>
        /// <param name="firstMatchCategory">
        /// </param>
        /// <returns>
        /// </returns>
        public bool IsTrigger(string input, out short firstMatchCategory, Func<short, bool> categoryAppliesCb)
        {
            using(var cmd = m_connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT * from TriggerIndex where TriggerText = $trigger";

                var domainSumParam = new SqliteParameter("$trigger", System.Data.DbType.String);
                domainSumParam.Value = input.ToLower();
                cmd.Parameters.Add(domainSumParam);

                using(var reader = cmd.ExecuteReader())
                {
                    if(!reader.HasRows)
                    {
                        firstMatchCategory = -1;
                        return false;
                    }

                    while(reader.Read())
                    {
                        var thisCat = reader.GetInt16(1);
                        if(categoryAppliesCb(thisCat))
                        {
                            firstMatchCategory = thisCat;
                            return true;
                        }
                    }
                }
            }

            firstMatchCategory = -1;
            return false;
        }

        /// <summary>
        /// Checks to see if the string supplied contains at least one substring that is a trigger.
        /// The supplied string will be broken apart by a static internal logic to perform this
        /// check.
        /// </summary>
        /// <param name="input">
        /// The input string to search for triggers.
        /// </param>
        /// <param name="firstMatchCategory">
        /// The category of the first discovered trigger. Will be -1 on failure.
        /// </param>
        /// <param name="rebuildAndTestFragments">
        /// If no match is found from a single substring fragment, and this parameter is set to true,
        /// the function will begin reconstructing new ordered sentences dynamically from all
        /// extracted fragments to see if a combination of substrings forms a trigger.
        /// </param>
        /// <param name="maxRebuildLen">
        /// If the rebuildAndTestFragments parameter is set to true, this can be set to the maximum
        /// number of substrings to combine in search for a match. By leaving the parameter at its
        /// default value, -1, every possible combination in every length will be tried. For example,
        /// if you're only wanting to match 3 works like "I dont care", you would set this parameter
        /// to a value of 3.
        ///
        /// Note that there are contraints on how long these combinations can be, so regardless of
        /// what the value is here, strings created from joins internally will be skipped when they
        /// exceed internal database column limits.
        /// </param>
        /// <returns>
        /// True if the supplied text contained one or more substrings that were indentified as a
        /// trigger.
        /// </returns>
        public bool ContainsTrigger(string input, out short firstMatchCategory, out string matchedTrigger, Func<short, bool> categoryAppliesCb, bool rebuildAndTestFragments = false, int maxRebuildLen = -1)
        {
            firstMatchCategory = -1;
            matchedTrigger = null;

            if(!m_hasTriggers)
            {
                return false;
            }

            var stopwatch = Stopwatch.StartNew();
            var split = Split(input); // THIS may be part of the performance issue
            stopwatch.Stop();

            m_logger.Info("Split time took {0}", stopwatch.ElapsedMilliseconds);

            using(var myConn = new SqliteConnection(m_connection.ConnectionString))
            {
                myConn.Open();

                int itr = 0;

                using(var tsx = myConn.BeginTransaction())
                using(var cmd = myConn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT * from TriggerIndex where TriggerText = $trigger";

                    var domainSumParam = new SqliteParameter("$trigger", System.Data.DbType.String);
                    cmd.Parameters.Add(domainSumParam);

                    bool skippingTags = false;
                    string[] newSplit = new string[split.Count];

                    foreach(var s in split)
                    {
                        switch(s.ToLower())
                        {
                            case "<div":
                            case "<span":
                            case "<a":
                            case "<img":
                            case "<meta":
                            case "<link":
                            case "<li":
                                skippingTags = true;
                                break;

                            case ">":
                                skippingTags = false;
                                break;
                        }

                        if(!skippingTags)
                        {
                            newSplit[itr] = s;
                            itr++;
                        }

                        cmd.Parameters[0].Value = s;
                        using(var reader = cmd.ExecuteReader())
                        {
                            if(!reader.HasRows)
                            {
                                continue;
                            }

                            while(reader.Read())
                            {
                                var thisCat = reader.GetInt16(1);
                                if(categoryAppliesCb(thisCat))
                                {
                                    firstMatchCategory = thisCat;
                                    matchedTrigger = s;
                                    return true;
                                }
                            }
                        }
                    }

                    // No match yet. Do rebuild if user asked for it.
                    if(rebuildAndTestFragments)
                    {
                        var len = itr; //newSplit.Length;

                        if(maxRebuildLen == -1 || maxRebuildLen > len)
                        {
                            maxRebuildLen = len;
                        }

                        ulong __itrCount = 0;
                        ulong __sqlItrCount = 0;

                        if(m_logger != null)
                        {
                            m_logger.Info("Trigger scan length = {0}", len);
                        }

                        for(var i = 0; i < len; ++i)
                        {
                            for(var j = i + 1; j < len && j - i <= maxRebuildLen; ++j)
                            {
                                __itrCount++;
                                var sub = new ArraySegment<string>(newSplit, i, j - i);
                                //var sub = split.GetRange(i, j - i);
                                
                                var subLen = (sub.Count - 1) + sub.Sum(xx => xx.Length);
                                // Don't bother checking the string if it exceeds the limits of our
                                // database column length. Currently it's char 255.
                                if(subLen >= byte.MaxValue)
                                {
                                    break;
                                }

                                var joined = string.Join(" ", sub);
                                cmd.Parameters[0].Value = joined;

                                using(var reader = cmd.ExecuteReader())
                                {
                                    __sqlItrCount++;

                                    if(!reader.HasRows)
                                    {
                                        continue;
                                    }

                                    while(reader.Read())
                                    {
                                        var thisCat = reader.GetInt16(1);
                                        if(categoryAppliesCb(thisCat))
                                        {
                                            firstMatchCategory = thisCat;
                                            matchedTrigger = joined;
                                            return true;
                                        }
                                    }
                                }
                            }
                        }

                        if(m_logger != null)
                        {
                            m_logger.Info("Number of times iterated: {0}", __itrCount);
                        }
                    }

                    tsx.Commit();
                }
            }

            return false;
        }

        private static List<string> Split(string input)
        {
            var sb = new StringBuilder();
            var res = new List<string>();
            var len = input.Length;
            for(var i = 0; i < len; ++i)
            {
                switch(input[i])
                {
                    case '<':
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'e':
                    case 'f':
                    case 'g':
                    case 'h':
                    case 'i':
                    case 'j':
                    case 'k':
                    case 'l':
                    case 'm':
                    case 'n':
                    case 'o':
                    case 'p':
                    case 'q':
                    case 'r':
                    case 's':
                    case 't':
                    case 'u':
                    case 'v':
                    case 'w':
                    case 'x':
                    case 'y':
                    case 'z':
                    case '-':
                    case '.':
                    case 'A':
                    case 'B':
                    case 'C':
                    case 'D':
                    case 'E':
                    case 'F':
                    case 'G':
                    case 'H':
                    case 'I':
                    case 'J':
                    case 'K':
                    case 'L':
                    case 'M':
                    case 'N':
                    case 'O':
                    case 'P':
                    case 'Q':
                    case 'R':
                    case 'S':
                    case 'T':
                    case 'U':
                    case 'V':
                    case 'W':
                    case 'X':
                    case 'Y':
                    case 'Z':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case '0':
                    {
                        sb.Append(char.ToLower(input[i]));
                    }
                    break;

                    case '>':
                        res.Add(sb.ToString());
                        sb.Clear();
                        res.Add(">");
                        break;

                    default:
                    {
                        if(sb.Length > 0)
                        {
                            res.Add(sb.ToString());
                            sb.Clear();
                        }
                    }
                    break;
                }
            }

            if(sb.Length > 0)
            {
                res.Add(sb.ToString());
                sb.Clear();
            }

            return res;
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if(!disposedValue)
            {
                if(disposing)
                {
                    if(m_connection != null)
                    {
                        m_connection.Close();
                        m_connection = null;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free
        //       unmanaged resources. ~BagOfTextTriggers() { // Do not change this code. Put cleanup
        // code in Dispose(bool disposing) above. Dispose(false); }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}