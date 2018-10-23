// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

using System;
using System.IO;
using System.Collections.Generic;

namespace Filter.Platform.Common.Util
{
    public class FileAuthenticationStorage : IAuthenticationStorage
    {
        public FileAuthenticationStorage()
        {

        }

        private Dictionary<string, string> m_authDict;

        private void initializeDictionary()
        {
            if (m_authDict == null)
            {
                m_authDict = new Dictionary<string, string>();


            }
        }

        private IPathProvider pathProvider = PlatformTypes.New<IPathProvider>();

        private void loadDictionary()
        {
            string fileName = Path.Combine(pathProvider.ApplicationDataFolder, "authentication.settings");

            if (!File.Exists(fileName))
            {
                return;
            }

            using (FileStream stream = new FileStream(fileName, FileMode.Open))
            using (StreamReader reader = new StreamReader(stream))
            {
                string line = null;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] lineParts = line.Split(new char[] { '=' }, 1);

                    m_authDict[lineParts[0]] = lineParts[1];
                }
            }
        }

        private void saveDictionary()
        {
            string fileName = Path.Combine(pathProvider.ApplicationDataFolder, "authentication.settings");

            using (FileStream stream = new FileStream(fileName, FileMode.Create))
            using (StreamWriter writer = new StreamWriter(stream))
            {
                foreach (var pair in m_authDict)
                {
                    writer.WriteLine($"{pair.Key}={pair.Value}");
                }
            }
        }

        public string AuthToken
        {
            get
            {
                initializeDictionary();

                string value = null;
                m_authDict.TryGetValue("AuthToken", out value);

                return value;
            }

            set
            {
                initializeDictionary();

                m_authDict["AuthToken"] = value;
                saveDictionary();
            }
        }

        public string UserEmail
        {
            get
            {
                initializeDictionary();

                string value = null;
                m_authDict.TryGetValue("UserEmail", out value);

                return value;
            }

            set
            {
                initializeDictionary();

                m_authDict["UserEmail"] = value;
                saveDictionary();
            }
        }
    }
}