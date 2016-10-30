﻿// Copyright (c) 2013, Gerson Kurz
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
//
// Redistributions of source code must retain the above copyright notice, this list
// of conditions and the following disclaimer. Redistributions in binary form must
// reproduce the above copyright notice, this list of conditions and the following
// disclaimer in the documentation and/or other materials provided with the distribution.
// 
// Neither the name regdiff nor the names of its contributors may be used to endorse
// or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
// IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
// INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
// NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace ngbt.regis3
{
    /// <summary>
    /// This class takes two (named) registry keys and produces a set of differences between the two keys.
    /// In regis3, keys can be imported from different sources (the registry, a .REG file, an .XML file) and are represented
    /// in memory as RegKeyEntry trees. This class takes two such trees (independently of their origin) and compares them.
    /// </summary>
    public class RegDiff
    {
        /// <summary>
        /// Keys missing in 1 but present in 2
        /// </summary>
        public readonly List<RegKeyEntry> MissingKeysIn1 = new List<RegKeyEntry>();

        /// <summary>
        /// Keys missing in 2 but present in 1
        /// </summary>
        public readonly List<RegKeyEntry> MissingKeysIn2 = new List<RegKeyEntry>();

        /// <summary>
        /// Values missing in 1 but present in 2
        /// </summary>
        public readonly List<MissingValue> MissingValuesIn1 = new List<MissingValue>();

        /// <summary>
        /// Values missing in 2 but present in 1 
        /// </summary>
        public readonly List<MissingValue> MissingValuesIn2 = new List<MissingValue>();

        /// <summary>
        /// Data mismatches: a key/value exists in both files, but the respective data doesn't match
        /// </summary>
        public readonly List<DataMismatch> DataMismatches = new List<DataMismatch>();

        /// <summary>
        /// Kind mismatches: a key/value exists in both files, but the kind doesn't match (for example, its a string in one file and an integer in the other)
        /// </summary>
        public readonly List<KindMismatch> KindMismatches = new List<KindMismatch>();

        /// <summary>
        /// Name of the first file / key / source
        /// </summary>
        public readonly string Name1;

        /// <summary>
        /// Name of the second file / key / source
        /// </summary>
        public readonly string Name2;

        /// <summary>
        /// First key 
        /// </summary>
        public readonly RegKeyEntry Key1;

        /// <summary>
        /// Second key
        /// </summary>
        public readonly RegKeyEntry Key2;

        /// <summary>
        /// Internal list of aliases
        /// </summary>
        private readonly Dictionary<string, string> Aliases;
         
        /// <summary>
        /// The constructor creates two named registry keys and compares them
        /// </summary>
        /// <param name="key1">First key</param>
        /// <param name="name1">Name of first key</param>
        /// <param name="key2">Second key</param>
        /// <param name="name2">Name of second key</param>
        /// <param name="aliases">Dictionary of key aliases</param>
        public RegDiff(RegKeyEntry key1, string name1, RegKeyEntry key2, string name2, Dictionary<string, string> aliases)
        {
            Key1 = key1;
            Key2 = key2;
            Name1 = name1;
            Name2 = name2;
            Aliases = aliases;
            CompareRecursive(key1, key2);
        }

        /// <summary>
        /// This function creates a new RegKeyEntry, that represents the diff information; 
        /// assuming that key 1 is the old information, and key 2 the new information
        /// 
        /// That means:
        /// - if a key is missing in 1, it is to be added
        /// - if a key is missing in 2, it is to be removed
        /// - if a value is missing in 1, it is to be added
        /// - if a value is missing in 2, it is to be removed
        /// - if a value has changed, use the data from 2 
        /// </summary>
        /// <returns>A newly created RegKeyEntry that describes the differences</returns>
        public RegKeyEntry CreateDiffKeyEntry()
        {
            RegKeyEntry result = new RegKeyEntry(null, null);

            foreach (RegKeyEntry keyMissingIn1 in MissingKeysIn1)
            {
                result.AskToAddKey(keyMissingIn1);
            }
            foreach (RegKeyEntry keyMissingIn2 in MissingKeysIn2)
            {
                result.AskToRemoveKey(keyMissingIn2);
            }
            foreach (MissingValue missingValueIn1 in MissingValuesIn1)
            {
                result.AskToAddValue(missingValueIn1.Key, missingValueIn1.Value);
            }
            foreach (MissingValue missingValueIn2 in MissingValuesIn2)
            {
                result.AskToRemoveValue(missingValueIn2.Key, missingValueIn2.Value);
            }
            foreach (DataMismatch dataMismatch in DataMismatches)
            {
                result.AskToAddValue(dataMismatch.Key, dataMismatch.Value2);
            }
            foreach (KindMismatch kindMismatch in KindMismatches)
            {
                result.AskToAddValue(kindMismatch.Key, kindMismatch.Value2);
            }
            return result;
        }

        /// <summary>
        /// This function creates a new RegKeyEntry, that represents the merge information; 
        /// assuming that key 1 is the old information, and key 2 the new information
        /// 
        /// That means:
        /// - includes all information from key 2
        /// - if a key is missing in 2, it is to be removed
        /// - if a value is missing in 2, it is to be removed
        /// - if a value has changed, use the data from 2 
        /// </summary>
        /// <returns>A newly created RegKeyEntry that describes the merge information</returns>
        public RegKeyEntry CreateMergeKeyEntry()
        {
            RegKeyEntry result = new RegKeyEntry(Key2);

            foreach (RegKeyEntry keyMissingIn2 in MissingKeysIn2)
            {
                result.AskToRemoveKey(keyMissingIn2);
            }
            foreach (MissingValue missingValueIn2 in MissingValuesIn2)
            {
                result.AskToRemoveValue(missingValueIn2.Key, missingValueIn2.Value);
            }
            return result;
        }

        private void CompareValues(RegKeyEntry key, RegValueEntry value1, RegValueEntry value2)
        {
            if (value1.Kind != value2.Kind)
            {
                KindMismatches.Add(new KindMismatch(key, value1, value2));
            }
            else
            {
                if (value1.Value is byte[])
                {
                    CompareByteArrays(key, value1, value2);
                }
                else if (value1.Value is string[])
                {
                    CompareStringArrays(key, value1, value2);
                }
                else if (!value1.Value.Equals(value2.Value))
                {
                    DataMismatches.Add(new DataMismatch(key, value1, value2));
                }
            }
        }

        private void CompareStringArrays(RegKeyEntry key, RegValueEntry value1, RegValueEntry value2)
        {
            string[] a = value1.Value as string[];
            string[] b = value2.Value as string[];

            if (a.Length != b.Length)
            {
                DataMismatches.Add(new DataMismatch(key, value1, value2));
            }
            else
            {
                for (int i = 0; i < a.Length; ++i)
                {
                    if (!a[i].Equals(b[i]))
                    {
                        DataMismatches.Add(new DataMismatch(key, value1, value2));
                    }
                }
            }
        }

        private void CompareByteArrays(RegKeyEntry key, RegValueEntry value1, RegValueEntry value2)
        {
            byte[] a = value1.AsByteArray();
            byte[] b = value2.AsByteArray();

            if (a.Length != b.Length)
            {
                DataMismatches.Add(new DataMismatch(key, value1, value2));
            }
            else
            {
                for (int i = 0; i < a.Length; ++i)
                {
                    if (a[i] != b[i])
                    {
                        DataMismatches.Add(new DataMismatch(key, value1, value2));
                    }
                }
            }
        }

        private static void ReportMissingKeys(StringBuilder result, List<RegKeyEntry> keys, string name)
        {
            if (keys.Count > 0)
            {
                if (keys.Count == 1)
                {
                    result.AppendFormat("1 key missing in '{0}':\r\n", name);
                }
                else
                {
                    result.AppendFormat("{0} keys missing in '{1}':\r\n", keys.Count, name);
                }
                
                foreach (RegKeyEntry key in keys)
                {
                    result.AppendFormat("- {0}\r\n", key.Path);
                }
                result.AppendLine();
            }
        }

        private void ReportDataMismatches(StringBuilder result)
        {
            if (DataMismatches.Count > 0)
            {
                if (DataMismatches.Count == 1)
                {
                    result.AppendFormat("1 data mismatch:\r\n");
                }
                else
                {
                    result.AppendFormat("{0} data mismatches:\r\n", DataMismatches.Count);
                }

                foreach (DataMismatch mismatch in DataMismatches)
                {
                    result.AppendFormat("- in {0}\r\n", mismatch.Key.Path);
                    if (mismatch.Value1.IsDefaultValue)
                    {
                        result.AppendFormat("-- default value (Type {0})\r\n", mismatch.Value1.Kind);
                    }
                    else
                    {
                        result.AppendFormat("-- value {0} (Type {1})\r\n", mismatch.Value1.Name, mismatch.Value1.Kind);
                    }
                    if (mismatch.Value1.Value is byte[])
                    {
                        DumpByteMismatch(result, mismatch.Value1.Value as byte[], mismatch.Value2.Value as byte[]);
                    }
                    else
                    {
                        result.AppendFormat("----- {0}\r\n", mismatch.Value1.Value);
                        result.AppendFormat("----- {0}\r\n", mismatch.Value2.Value);
                    }
                }
                result.AppendLine();
            }
        }
        
        private void DumpByteMismatch(StringBuilder result, byte[] a, byte[] b)
        {
            if (a == null)
            {
                result.AppendLine("a is null");
                result.AppendLine(string.Format("b is array of {0} bytes", b.Length));
                return;
            }
            else if (b == null)
            {
                result.AppendLine(string.Format("a is array of {0} bytes", a.Length));
                result.AppendLine("b is null");
                return;

            }

            int n = Math.Max(a.Length, b.Length);

            for (int i = 0; i < n; ++i)
            {
                if (i < a.Length)
                {
                    if (i < b.Length)
                    {
                        if (a[i] != b[i])
                        {
                            result.AppendLine(string.Format("byte[] at pos {0} (of {1}): {2}",
                                i, a.Length, a[i]));
                            result.AppendLine(string.Format("byte[] at pos {0} (of {1}): {2}",
                                i, b.Length, b[i]));
                            break;
                        }
                    }
                    else
                    {
                        result.AppendLine(string.Format("byte[] at pos {0} (of {1}): {2}",
                                i, a.Length, a[i]));
                        result.AppendLine(string.Format("byte[] has only {0} bytes - no index {1} defined",
                            b.Length, i));
                        break;
                    }
                }
                else if( i < b.Length )
                {
                    result.AppendLine(string.Format("byte[] has only {0} bytes - no index {1} defined",
                        a.Length, i));
                    result.AppendLine(string.Format("byte[] at pos {0} (of {1}): {2}",
                                i, b.Length, b[i]));
                    break;
                }
            }
            
            result.AppendLine();
        }

        private void ReportKindMismatches(StringBuilder result)
        {
            if (KindMismatches.Count > 0)
            {
                if (KindMismatches.Count == 1)
                {
                    result.AppendFormat("1 type mismatch:\r\n");
                }
                else
                {
                    result.AppendFormat("{0} type mismatches:\r\n", DataMismatches.Count);
                }

                foreach (KindMismatch mismatch in KindMismatches)
                {
                    result.AppendFormat("- in {0}\r\n", mismatch.Key.Path);

                    if (mismatch.Value1.IsDefaultValue)
                    {
                        result.AppendFormat("-- default value: {0} <> {1}\r\n", mismatch.Value1.Kind, mismatch.Value2.Kind);
                    }
                    else
                    {
                        result.AppendFormat("-- value {0}: {1} <> {2}\r\n", mismatch.Value1.Name, mismatch.Value1.Kind, mismatch.Value2.Kind);
                    }
                }
                result.AppendLine();
            }
        }

        private static void ReportMissingValues(StringBuilder result, List<MissingValue> values, string name)
        {
            if (values.Count > 0)
            {
                if (values.Count == 1)
                {
                    result.AppendFormat("1 value missing in '{0}':\r\n", name);
                }
                else
                {
                    result.AppendFormat("{0} values missing in '{1}':\r\n", values.Count, name);
                }
                string lastKnownSectionName = null;
                foreach (MissingValue entry in values)
                {
                    string sectionName = entry.Key.Path;
                    if (string.IsNullOrEmpty(lastKnownSectionName) ||
                        !lastKnownSectionName.Equals(sectionName))
                    {
                        lastKnownSectionName = sectionName;
                        result.AppendFormat("- Key {0}:\r\n", sectionName);
                    }
                    if (entry.Value.IsDefaultValue)
                    {
                        result.AppendFormat("--- Default Value\r\n");
                    }
                    else
                    {
                        result.AppendFormat("--- Value {0}\r\n", entry.Value.Name);
                    }
                }
                result.AppendLine();
            }
        }

        /// <summary>
        /// Create a string description of this instance
        /// </summary>
        /// <returns>String description of this instance</returns>
        public override string ToString()
        {
            StringBuilder result = new StringBuilder();
            
            ReportMissingKeys(result, MissingKeysIn1, Name1);
            ReportMissingKeys(result, MissingKeysIn2, Name2);
            ReportMissingValues(result, MissingValuesIn1, Name1);
            ReportMissingValues(result, MissingValuesIn2, Name2);
            ReportDataMismatches(result);
            ReportKindMismatches(result);

            string text = result.ToString();
            if (string.IsNullOrEmpty(text))
                text = "- no differences found -";
            return text;
        }

        private void CompareRecursive(RegKeyEntry key1, RegKeyEntry key2)
        {
            // Acquire keys and sort them.
            List<string> sortedNames;
            
            sortedNames = key1.Keys.Keys.ToList();
            sortedNames.Sort();
            foreach (string keyName in sortedNames)
            {
                RegKeyEntry subkey1 = key1.Keys[keyName];
                if (key2.Keys.ContainsKey(keyName))
                {
                    RegKeyEntry subkey2 = key2.Keys[keyName];
                    CompareRecursive(subkey1, subkey2);
                }
                else
                {
                    // two forms are supported: either a single key (as in FOO=BAR), or a complete path (as in HKLM\BLA\BLUB=HKCU\SMA\BU)
                    // we have a mismatch. It may happen that the key needs to be renamed, and then compared again

                    if (Aliases.ContainsKey(keyName.ToLower()))
                    {
                        string aliasedName = Aliases[keyName.ToLower()].ToLower();
                        if (key2.Keys.ContainsKey(aliasedName))
                        {
                            RegKeyEntry subkey2 = key2.Keys[aliasedName];
                            CompareRecursive(subkey1, subkey2);
                        }
                        else
                        {
                            MissingKeysIn2.Add(subkey1);
                        }
                    }
                    else
                    {
                        MissingKeysIn2.Add(subkey1);
                    }
                }
            }

            
            if (key1.DefaultValue != null)
            {
                Debug.Assert(key1.DefaultValue.IsDefaultValue);
                if (key2.DefaultValue == null)
                {
                    MissingValuesIn2.Add(new MissingValue(key1, key1.DefaultValue));
                }
            }
            else if (key2.DefaultValue != null)
            {
                Debug.Assert(key2.DefaultValue.IsDefaultValue);
                MissingValuesIn1.Add(new MissingValue(key2, key2.DefaultValue));
            }


            sortedNames = key1.Values.Keys.ToList();
            sortedNames.Sort();
            foreach (string valueName in sortedNames)
            {
                RegValueEntry value1 = key1.Values[valueName];
                if (key2.Values.ContainsKey(valueName))
                {
                    CompareValues(key1, value1, key2.Values[valueName]);
                }
                else
                {
                    MissingValuesIn2.Add(new MissingValue(key1, value1)); 
                }
            }

            sortedNames = key2.Values.Keys.ToList();
            sortedNames.Sort();
            foreach (string valueName in sortedNames)
            {
                RegValueEntry value2 = key2.Values[valueName];
                if (!key1.Values.ContainsKey(valueName))
                {
                    MissingValuesIn1.Add(new MissingValue(key2, value2));
                }
            }

            sortedNames = key2.Keys.Keys.ToList();
            sortedNames.Sort();
            foreach (string keyName in sortedNames)
            {
                if (!key1.Keys.ContainsKey(keyName))
                {
                    if (Aliases.ContainsKey(keyName.ToLower()))
                    {
                        string aliasedName = Aliases[keyName.ToLower()].ToLower();
                        if (!key1.Keys.ContainsKey(aliasedName))
                        {
                            MissingKeysIn1.Add(key2.Keys[keyName]);
                        }
                    }
                    else
                    {
                        MissingKeysIn1.Add(key2.Keys[keyName]);
                    }
                }
            }
        }
    }
}

