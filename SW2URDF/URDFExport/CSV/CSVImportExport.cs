using CsvHelper;
using log4net;
using Microsoft.VisualBasic.FileIO;
using SW2URDF.URDF;
using SW2URDF.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SW2URDF.URDFExport.CSV
{
    /// <summary>
    /// Class to perform exporting to CSV and eventually importing of data from a CSV file
    /// </summary>
    public static class ImportExport
    {
        private static readonly ILog logger = Logger.GetLogger();

        #region Public Methods

        /// <summary>
        /// Method to write a full URDF robot to a CSV
        /// </summary>
        /// <param name="robot">URDF robot tree</param>
        /// <param name="filename">Fully qualified string name to write to</param>
        public static void WriteRobotToCSV(Robot robot, string filename)
        {
            logger.Info("Writing CSV file " + filename);
            using (StreamWriter stream = new StreamWriter(filename))
            {
                CsvWriter writer = new CsvWriter(stream);
                WriteHeaderToCSV(writer);
                WriteLinkToCSV(writer, robot.BaseLink);
            }
        }

        /// <summary>
        /// Loads a list of URDF Links from a CSV file
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static List<Link> LoadURDFRobotFromCSV(Stream stream)
        {
            List<Link> loadedLinks = new List<Link>();
            using (TextFieldParser csvParser = new TextFieldParser(stream))
            {
                csvParser.SetDelimiters(new string[] { "," });

                string[] headers = csvParser.ReadFields();
                if (headers == null || headers.Length == 0 ||
                    headers.Any(string.IsNullOrWhiteSpace) ||
                    headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
                {
                    throw new InvalidDataException("CSV headers must be non-empty and unique.");
                }
                int row = 1;
                while (!csvParser.EndOfData)
                {
                    row++;
                    string[] fields = csvParser.ReadFields();
                    StringDictionary dictionary = new StringDictionary();
                    
                    if (fields == null || fields.Length != headers.Length)
                    {
                        throw new InvalidDataException("CSV row " + row + " does not match the header column count.");
                    }
                    for (int i = 0; i < headers.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(fields[i]))
                        {
                            dictionary[headers[i]] = fields[i];
                        }
                    }
                    try
                    {
                        loadedLinks.Add(BuildLinkFromData(dictionary));
                    }
                    catch (FormatException error)
                    {
                        throw new InvalidDataException("CSV row " + row + ": " + error.Message, error);
                    }
                }

                return loadedLinks;
            }
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// Iterates through the column names and writes them to a file stream
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        private static void WriteHeaderToCSV(CsvWriter writer)
        {
            foreach (DictionaryEntry entry in ContextToColumns.Dictionary)
            {
                string columnName = (string)entry.Value;
                writer.WriteField(columnName);
            }
            writer.NextRecord();
        }

        /// <summary>
        /// Appends a line to an open CSV document of a URDF's Link properties
        /// </summary>
        /// <param name="stream">Stream representing opened CSV file</param>
        /// <param name="dictionary">Dictionary of values</param>
        private static void WriteValuesToCSV(CsvWriter writer, OrderedDictionary dictionary)
        {
            foreach (DictionaryEntry entry in ContextToColumns.Dictionary)
            {
                string context = (string)entry.Key;
                if (dictionary.Contains(context))
                {
                    object value = dictionary[context];
                    writer.WriteField(value is double
                        ? ((double)value).ToString("R", CultureInfo.InvariantCulture) : value);
                }
                else
                {
                    writer.WriteField(null);
                }
            }

            writer.NextRecord();
            HashSet<string> keys1 = new HashSet<string>(ContextToColumns.Dictionary.Keys.Cast<string>());
            HashSet<string> keys2 = new HashSet<string>(dictionary.Keys.Cast<string>());

            StringBuilder missingColumns = new StringBuilder();
            foreach (string missing in keys2.Except(keys1))
            {
                missingColumns.Append(missing).Append(",");
            }
            if (missingColumns.Length > 0)
            {
                logger.Error("The following columns were not written to the CSV: " + missingColumns.ToString());
            }
        }

        /// <summary>
        /// Converts a URDF Link to a dictionary of values and writes them to a CSV
        /// </summary>
        /// <param name="stream">StreamWriter of opened CSV document</param>
        /// <param name="link">URDF link to append to the file</param>
        private static void WriteLinkToCSV(CsvWriter writer, Link link)
        {
            OrderedDictionary dictionary = new OrderedDictionary();
            link.AppendToCSVDictionary(new List<string>(), dictionary);
            WriteValuesToCSV(writer, dictionary);

            foreach (Link child in link.Children)
            {
                WriteLinkToCSV(writer, child);
            }
        }

        private static Link BuildLinkFromData(StringDictionary dictionary)
        {
            StringDictionary contextDictionary = new StringDictionary();
            foreach (DictionaryEntry entry in ContextToColumns.Dictionary)
            {
                string context = (string)entry.Key;
                string columnName = (string)entry.Value;

                if (dictionary.ContainsKey(columnName))
                {
                    contextDictionary[context] = dictionary[columnName];
                }
            }

            Link link = new Link();
            link.SetElementFromData(new List<string>(), contextDictionary);
            return link;
        }

        #endregion Private Methods
    }
}
