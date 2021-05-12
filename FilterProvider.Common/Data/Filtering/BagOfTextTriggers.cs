/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
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
using NLog;
using System.Diagnostics;
using CloudVeil.Core.Windows.Util;
using Filter.Platform.Common.Util;

namespace FilterProvider.Common.Data.Filtering
{
    /// <summary>
    /// The BagOfTextTriggers class serves the sole purpose of storing bits of text in a database,
    /// and then reponding to queries with answers about whether this class has stored the text in
    /// question.
    /// </summary>
    /// <remarks>
    /// This class is a super special snowflake that will unpredictably get triggered.
    /// </remarks>
    public class BagOfTextTriggers : IDisposable
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

        /// <summary>
        /// A bloom filter to help determine if a trigger is in the SQL database.
        /// </summary>
        public BloomFilter<string> TriggerFilter { get; set; }

        /// <summary>
        /// A bloom filter to prefetch results for first words, so we don't have to do a SQL call.
        /// </summary>
        public BloomFilter<string> FirstWordFilter { get; set; }

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

                command.CommandText = "CREATE TABLE IF NOT EXISTS FirstWordIndex (FirstWordText VARCHAR(255), IsWholeTrigger INT16, CategoryId INT16)";
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

                command.CommandText = "CREATE INDEX IF NOT EXISTS first_word_index ON FirstWordIndex (FirstWordText)";
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

        public void InitializeBloomFilters()
        {
            int firstWordCount;

            using (var countCmd = m_connection.CreateCommand())
            {
                countCmd.CommandText = "SELECT COUNT(1) FROM FirstWordIndex;";

                using (var reader = countCmd.ExecuteReader())
                {
                    reader.Read();
                    firstWordCount = reader.GetInt32(0);
                }
            }

            FirstWordFilter = new BloomFilter<string>(firstWordCount == 0 ? 100 : firstWordCount);

            using (var cmd = m_connection.CreateCommand())
            {
                cmd.CommandText = "SELECT FirstWordText FROM FirstWordIndex;";

                using (var reader = cmd.ExecuteReader())
                {
                    while(reader.Read())
                    {
                        string firstWord = reader.GetString(0);
                        FirstWordFilter.Add(firstWord);
                    }
                }
            }

            int triggerCount;

            using (var countCmd = m_connection.CreateCommand())
            {
                countCmd.CommandText = "SELECT COUNT(1) FROM TriggerIndex;";

                using (var reader = countCmd.ExecuteReader())
                {
                    reader.Read();
                    triggerCount = reader.GetInt32(0);
                }
            }

            TriggerFilter = new BloomFilter<string>(triggerCount == 0 ? 100 : triggerCount);
            
            using (var cmd = m_connection.CreateCommand())
            {
                cmd.CommandText = "SELECT TriggerText FROM TriggerIndex;";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string triggerWord = reader.GetString(0);
                        TriggerFilter.Add(triggerWord);
                    }
                }
            }

            m_logger.Info($"trigger count = {triggerCount}, first word count = {firstWordCount}");
        }

        private class StoreCommands : IDisposable
        {
            SqliteConnection connection;

            SqliteCommand firstWordCommand;
            SqliteCommand triggerCommand;

            public StoreCommands(SqliteConnection connection)
            {
                this.connection = connection;

                firstWordCommand = connection.CreateCommand();
                triggerCommand = connection.CreateCommand();

                firstWordCommand.CommandText = "INSERT INTO FirstWordIndex VALUES ($firstWord, $isWholeTrigger, $categoryId)";
                triggerCommand.CommandText = "INSERT INTO TriggerIndex VALUES ($trigger, $categoryId)";
                var domainParam = new SqliteParameter("$trigger", DbType.String);
                var categoryIdParam = new SqliteParameter("$categoryId", DbType.Int16);
                triggerCommand.Parameters.Add(domainParam);
                triggerCommand.Parameters.Add(categoryIdParam);

                var firstWordParam = new SqliteParameter("$firstWord", DbType.String);
                var isWholeTriggerParam = new SqliteParameter("$isWholeTrigger", DbType.Int16);
                var firstWordCategoryIdParam = new SqliteParameter("$categoryId", DbType.Int16);
                firstWordCommand.Parameters.Add(firstWordParam);
                firstWordCommand.Parameters.Add(isWholeTriggerParam);
                firstWordCommand.Parameters.Add(firstWordCategoryIdParam);
            }

            public async Task<bool> StoreTrigger(string line, short categoryId)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.Length > 0)
                {
                    List<string> trimmedLineParts = Split(trimmedLine);

                    triggerCommand.Parameters[0].Value = trimmedLine;
                    triggerCommand.Parameters[1].Value = categoryId;

                    firstWordCommand.Parameters[0].Value = trimmedLineParts.First();
                    firstWordCommand.Parameters[1].Value = trimmedLineParts.Count == 1;
                    firstWordCommand.Parameters[2].Value = categoryId;

                    await triggerCommand.ExecuteNonQueryAsync();
                    await firstWordCommand.ExecuteNonQueryAsync();

                    return true;
                }
                else
                {
                    return false;
                }
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        firstWordCommand.Dispose();
                        triggerCommand.Dispose();
                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                    // TODO: set large fields to null.

                    disposedValue = true;
                }
            }

            // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
            // ~StoreCommands() {
            //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            //   Dispose(false);
            // }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
                // TODO: uncomment the following line if the finalizer is overridden above.
                // GC.SuppressFinalize(this);
            }
            #endregion
        }

        public async Task<int> LoadStoreFromList(IEnumerable<string> inputList, short categoryId)
        {
            int loaded = 0;
            using (var transaction = m_connection.BeginTransaction())
            {
                using (var storeCommands = new StoreCommands(m_connection))
                {
                    foreach(string line in inputList)
                    {
                        if (line == null) continue;

                        if (await storeCommands.StoreTrigger(line, categoryId)) ++loaded;
                    }
                }

                transaction.Commit();
            }

            m_hasTriggers = loaded > 0;

            return loaded;
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
                using (var storeCommands = new StoreCommands(m_connection))
                {
                    string line = null;
                    using (var sw = new StreamReader(inputStream))
                    {
                        while ((line = await sw.ReadLineAsync()) != null)
                        {
                            if (await storeCommands.StoreTrigger(line, categoryId)) ++loaded;
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

       //     LoggerUtil.GetAppWideLogger().Info("Triggers for input: " + input);

            var split = Split(input);

            /*using (FileStream debugStream = new FileStream(@"C:\ProgramData\CloudVeil\textTriggerDebug.txt", FileMode.Append))
            using (StreamWriter writer = new StreamWriter(debugStream))*/
            using (var myConn = new SqliteConnection(m_connection.ConnectionString))
            {
                myConn.Open();

                int itr = 0;

                SqliteCommand triggerCommand = null;

                using (var tsx = myConn.BeginTransaction())
                using (var cmd = myConn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT * from FirstWordIndex where FirstWordText = $trigger";

                    var domainSumParam = new SqliteParameter("$trigger", System.Data.DbType.String);
                    cmd.Parameters.Add(domainSumParam);

                    bool skippingTags = false;
                    bool skippingScript = false;
                    bool quoteReached = false;
                    bool collectingImportantAttributes = false;
                    bool isClosingTag = false;

                    List<string> newSplit = new List<string>();
                    List<List<string>> wordLists = new List<List<string>>();

                    List<List<string>> listsToRemove = new List<List<string>>(maxRebuildLen);

                    foreach (var s in split)
                    {

                        // Completely skip closing tags.
                        if (s.Length > 2 && s[0] == '<' && s[1] == '/' && s[s.Length - 1] == '>')
                        {
                            isClosingTag = true;
                        }

                        // We require some more complex determinations for opening tags as they may have important
                        // text inside them.
                        if (s.Length >= 2 && s[0] == '<' && s[1] != '/' && s.Length > 1 && s.Length < 10)
                        {
                            skippingTags = true;
                        }

                        switch (s.ToLower())
                        {
                            case "<script":
                            case "<style":
                                skippingScript = true;
                                break;

                            case "</script>":
                            case "</style>":
                                skippingScript = false;
                                break;

                            case ">":
                                skippingTags = false;
                                break;

                            case "alt":
                            case "title":
                            case "href":
                                collectingImportantAttributes = true;
                                break;

                            case "\"":
                            case "\'":
                                quoteReached = !quoteReached;
                                if (!quoteReached)
                                {
                                    collectingImportantAttributes = false;
                                }

                                break;

                        }

                        if (isClosingTag)
                        {
                            isClosingTag = false;
                            continue;
                        }

                        if (collectingImportantAttributes)
                        {
                            newSplit.Add(s);
                            itr++;
                        }
                        else if (!skippingTags && !skippingScript && s != ">")
                        {
                            newSplit.Add(s);
                            itr++;
                        }

                        if (skippingTags && !collectingImportantAttributes)
                        {
                            continue;
                        }

                        listsToRemove.Clear();

                        // Check word lists.
                        foreach (var list in wordLists)
                        {
                            if (list.Count >= maxRebuildLen)
                            {
                                listsToRemove.Add(list);
                            }
                            else
                            {
                                list.Add(s);
                            }
                        }

                        foreach (var l in listsToRemove)
                        {
                            wordLists.Remove(l);
                        }

                        // Check word lists.
                        foreach (var list in wordLists)
                        {
                            string triggerCandidate = string.Join(" ", list);

                            if (!TriggerFilter.Contains(triggerCandidate))
                            {
                                continue;
                            }

                            if (triggerCommand == null)
                            {
                                triggerCommand = myConn.CreateCommand();
                                triggerCommand.CommandText = "SELECT * FROM TriggerIndex WHERE TriggerText = $trigger";
                                var wholeTriggerDomainSumParam = new SqliteParameter("$trigger", System.Data.DbType.String);
                                wholeTriggerDomainSumParam.Value = input.ToLower();
                                triggerCommand.Parameters.Add(wholeTriggerDomainSumParam);
                            }

                            triggerCommand.Parameters[0].Value = triggerCandidate;
                            using (var triggerReader = triggerCommand.ExecuteReader())
                            {
                                while (triggerReader.Read())
                                {
                                    var thisCat = triggerReader.GetInt16(1);
                                    if (categoryAppliesCb(thisCat))
                                    {
                                        firstMatchCategory = thisCat;
                                        matchedTrigger = string.Join(" ", list);

                                        return true;
                                    }
                                }
                            }
                        }

                        // TODO: Apply bloom filters to this problem.
                        // We need a bloom filter for first words and a bloom filter for triggers for maximum effect.
                        if (!FirstWordFilter.Contains(s))
                        {
                            continue;
                        }

                        cmd.Parameters[0].Value = s;
                        using (var reader = cmd.ExecuteReader())
                        {

                            if (!reader.HasRows)
                            {
                                continue;
                            }

                            while (reader.Read())
                            {
                                var isWholeTrigger = reader.GetInt16(1);

                                if (isWholeTrigger > 0)
                                {
                                    var thisCat = reader.GetInt16(2);
                                    if (categoryAppliesCb(thisCat))
                                    {
                                        firstMatchCategory = thisCat;
                                        matchedTrigger = s;
                                        return true;
                                    }
                                }
                                else
                                {
                                    var newList = new List<string>(maxRebuildLen);
                                    newList.Add(s);
                                    wordLists.Add(newList);

                                    break;
                                }
                            }
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
                        if(sb.Length > 0)
                        {
                            res.Add(sb.ToString());
                            sb.Clear();
                        }

                        sb.Append('<');

                        if(i < len-1 && input[i + 1] == '/')
                        {
                            while(input[i] != '>')
                            {
                                i++;
                                if(i >= len) {
                                    break;
                                }
                                sb.Append(input[i]);
                            }


                        }
                        break;

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

                    case '"':
                    case '\'':
                        res.Add(sb.ToString());
                        sb.Clear();
                        res.Add(new string(input[i], 1));
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