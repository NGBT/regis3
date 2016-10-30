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
using System.IO;
using System.Xml;

namespace ngbt.regis3
{
    /// <summary>
    /// This is an exporter that takes a registry key and exports it in XML format
    /// </summary>
    public class XmlRegFileExporter : IRegistryExporter
    {
        /// <summary>
        /// Export the key to a given file
        /// </summary>
        /// <param name="key">Existing key</param>
        /// <param name="filename">Name for .XML file</param>
        /// <param name="options">Export options</param>
        public void Export(RegKeyEntry key, string filename, RegFileExportOptions options)
        {
            using (StreamWriter sw = new StreamWriter(File.Open(filename, FileMode.Create), Encoding.UTF8))
            {
                try
                {
                    Export(key, sw, options);
                }
                finally
                {
                    sw.Close();
                }
            }
        }

        /// <summary>
        /// Export the key to a given output stream
        /// </summary>
        /// <param name="key">Existing key</param>
        /// <param name="file">Output stream</param>
        /// <param name="options">Export options</param>
        public void Export(RegKeyEntry key, TextWriter file, RegFileExportOptions options)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;

            XmlWriter Writer = XmlWriter.Create(file, settings);
            Writer.WriteStartElement("registry");
            Writer.WriteAttributeString("version", "2");
           
            WriteXmlFileFormat(Writer, key);
            
            Writer.WriteEndElement();
            Writer.Close();
        }

        private static bool IsValidXmlString(string s)
        {
            foreach (char current in s)
            {
                if ( ((current >= 0x20) && (current <= 0xD7FF)) ||
                     ((current >= 0xE000) && (current <= 0xFFFD)) )
                {
                    // is valid
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private static string EncodeBase64(string str)
        {
            return Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(str));
        }

        private static void EncodeStringValue(XmlWriter Writer, RegValueEntry value)
        {
            string content = value.Value.ToString();

            if ((content.Trim() != "") && IsValidXmlString(content))
            {
                Writer.WriteValue(content);
            }
            else
            {
                Writer.WriteAttributeString("encoding", "base-64");
                Writer.WriteValue(EncodeBase64(content));
            }
        }

        private static void EncodeMultiStringValue(XmlWriter Writer, RegValueEntry value)
        {
            string[] content = (string[]) value.Value;

            foreach (string line in content)
            {
                Writer.WriteStartElement("line");
                if ((line.Trim() != "") && IsValidXmlString(line))
                {
                    Writer.WriteValue(line);
                }
                else
                {
                    Writer.WriteAttributeString("encoding", "base-64");
                    Writer.WriteValue(EncodeBase64(line));
                }
                Writer.WriteEndElement();
            }
        }

        private static void WriteXmlFileFormat(XmlWriter Writer, RegKeyEntry key)
        {
            List<string> names;

            if (key.RemoveFlag)
            {
                if (key.Name != null)
                {
                    Writer.WriteStartElement("remove-key");
                    Writer.WriteAttributeString("name", key.Name);
                    Writer.WriteEndElement();
                }
            }
            else
            {
                if (key.Name != null)
                {
                    Writer.WriteStartElement("key");
                    Writer.WriteAttributeString("name", key.Name);
                }

                names = key.Keys.Keys.ToList<string>();
                names.Sort();
                foreach (string name in names)
                {
                    WriteXmlFileFormat(Writer, key.Keys[name]);
                }

                if (key.DefaultValue != null)
                {
                    WriteXmlFileFormat(Writer, key.DefaultValue);
                }

                names = key.Values.Keys.ToList<string>();
                names.Sort();
                foreach (string name in names)
                {
                    WriteXmlFileFormat(Writer, key.Values[name]);
                }
                if (key.Name != null)
                {
                    Writer.WriteEndElement();
                }
            }
        }

        private static void WriteHexEncodedValue(XmlWriter writer, RegValueEntryKind kind, IEnumerable<byte> bytes)
        {
            StringBuilder buffer = new StringBuilder();
            foreach (byte b in bytes)
            {
                buffer.Append(b.ToString("X2"));
            }
            writer.WriteValue(buffer.ToString());
        }

        private static void WriteXmlFileFormat(XmlWriter writer, RegValueEntry value)
        {
            if (value.RemoveFlag)
            {
                if (value.IsDefaultValue)
                {
                    writer.WriteStartElement("remove-default-value");
                }
                else
                {
                    writer.WriteStartElement("remove-value");
                    writer.WriteAttributeString("name", value.Name);
                }
            }
            else
            {
                writer.WriteStartElement(value.Kind.ToString());
                if( !value.IsDefaultValue )
                {
                    writer.WriteAttributeString("name", value.Name);
                }

                switch (value.Kind)
                {
                    case RegValueEntryKind.DWord:
                    case RegValueEntryKind.QWord:
                        writer.WriteValue(value.Value.ToString());
                        break;

                    case RegValueEntryKind.SZ:
                    case RegValueEntryKind.ExpandSZ:
                        EncodeStringValue(writer, value);
                        break;

                    case RegValueEntryKind.MultiSZ:
                        EncodeMultiStringValue(writer, value);
                        break;

                    default:
                        if ((value.Value != null) && (value.Value is byte[]))
                        {
                            WriteHexEncodedValue(writer, value.Kind, value.Value as byte[]);
                        }
                        else if( ( value.Kind != RegValueEntryKind.Unknown ) && ( value.Kind != RegValueEntryKind.None ) )
                        {
                            throw new Exception(string.Format("ERROR, XmlRegFileExporter() isn't prepared to handle data of type {0}", value.Kind));
                        }
                        break;
                }
            }
            writer.WriteEndElement();
        }
    }
}
