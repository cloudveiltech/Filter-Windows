/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagnosticsCollector
{
    public class HeaderComparisons
    {
        public string Uri { get; set; }
        public List<HeaderComparisonInfo> Comparisons { get; set; }
    }

    public enum ModificationType
    {
        Added, Removed, BothLists
    }

    public class ValueComparisonInfo
    {
        public string HeaderKey { get; set; }
        
        public string Value { get; set; }

        public ModificationType ModificationType { get; set; }
    }

    public class HeaderComparisonInfo
    {
        public string HeaderKey { get; set; }
        public ModificationType ModificationType { get; set; }

        public List<ValueComparisonInfo> ValueComparisons { get; set; }
    }

    public static class HeaderParser
    {
        public static Dictionary<string, List<string>> Parse(string headerString)
        {
            if(headerString == null)
            {
                return null;
            }

            Dictionary<string, List<string>> headerList = new Dictionary<string, List<string>>();

            string[] headers = headerString.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach(var header in headers)
            {
                string[] headerParts = header.Split(':');

                for (int i = 0; i < headerParts.Length; i++)
                {
                    headerParts[i] = headerParts[i].Trim();
                }

                if(headerParts.Length == 1)
                {
                    // Insert into last string TODO
                }

                string headerKey = headerParts[0].ToLower();

                List<string> valueList = null;
                if(!headerList.TryGetValue(headerKey, out valueList))
                {
                    valueList = new List<string>();
                    headerList[headerKey] = valueList;
                }

                valueList.Add(headerParts[1]);
            }

            return headerList;
        }

        // We basically want to diff list2 with list1.
        // For 
        public static List<HeaderComparisonInfo> Compare(Dictionary<string, List<string>> list1, Dictionary<string, List<string>> list2)
        {
            List<HeaderComparisonInfo> keyComparisons = new List<HeaderComparisonInfo>();

            foreach(var pair in list1)
            {
                List<string> headerValues;
                if(!list2.TryGetValue(pair.Key, out headerValues))
                {
                    keyComparisons.Add(new HeaderComparisonInfo()
                    {
                        HeaderKey = pair.Key,
                        ModificationType = ModificationType.Removed,
                        ValueComparisons = pair.Value.Select(v => new ValueComparisonInfo()
                        {
                            HeaderKey = pair.Key,
                            ModificationType = ModificationType.Removed,
                            Value = v
                        }).ToList()
                    });
                }
                else
                {
                    keyComparisons.Add(new HeaderComparisonInfo()
                    {
                        HeaderKey = pair.Key,
                        ModificationType = ModificationType.BothLists
                    });
                }
            }

            foreach(var pair in list2)
            {
                List<string> headerValues;
                if(!list1.TryGetValue(pair.Key, out headerValues))
                {
                    keyComparisons.Add(new HeaderComparisonInfo()
                    {
                        HeaderKey = pair.Key,
                        ModificationType = ModificationType.Added,
                        ValueComparisons = pair.Value.Select(v => new ValueComparisonInfo()
                        {
                            HeaderKey = pair.Key,
                            ModificationType = ModificationType.Added,
                            Value = v
                        }).ToList()
                    });
                }
            }

            foreach(var comparison in keyComparisons)
            {
                if(comparison.ModificationType == ModificationType.BothLists)
                {
                    List<string> values1, values2;

                    list1.TryGetValue(comparison.HeaderKey, out values1);
                    list2.TryGetValue(comparison.HeaderKey, out values2);

                    List<ValueComparisonInfo> valueComparisons = compareHeaderValues(comparison.HeaderKey, values1, values2);
                    comparison.ValueComparisons = valueComparisons;
                }
            }

            return keyComparisons;
        }

        private static List<ValueComparisonInfo> compareHeaderValues(string headerKey, List<string> values1, List<string> values2)
        {
            List<ValueComparisonInfo> valueComparisons = new List<ValueComparisonInfo>();

            foreach(var value in values1)
            {
                // If entry is found in values2, ValueComparisonInfo.ModificationType should be BothLists*
                bool valueExistsInList = false;

                foreach(var inner in values2)
                {
                    if(inner == value)
                    {
                        valueComparisons.Add(new ValueComparisonInfo()
                        {
                            Value = value,
                            ModificationType = ModificationType.BothLists
                        });

                        valueExistsInList = true;
                        break;
                    }
                }

                if(!valueExistsInList)
                {
                    valueComparisons.Add(new ValueComparisonInfo()
                    {
                        Value = value,
                        ModificationType = ModificationType.Removed
                    });
                }
            }

            foreach(var value in values2)
            {
                bool valueExistsInList = false;

                foreach(var inner in values1)
                {
                    if(value == inner)
                    {
                        valueExistsInList = true;
                        break;
                    }
                }

                if(!valueExistsInList)
                {
                    valueComparisons.Add(new ValueComparisonInfo()
                    {
                        Value = value,
                        ModificationType = ModificationType.Added
                    });
                }
            }

            return valueComparisons;
        }
    }
}
