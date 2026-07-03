using CsvHelper;
using CsvHelper.Configuration;
using log4net;
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
                CsvWriter writer = new CsvWriter(stream, CultureInfo.InvariantCulture);
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
            List<Dictionary<string, string>> loadedFields = new List<Dictionary<string, string>>();
            using (StreamReader reader = new StreamReader(stream))
            using (CsvReader csvReader = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null
            }))
            {
                csvReader.Read();
                csvReader.ReadHeader();
                string[] headers = csvReader.HeaderRecord;

                while (csvReader.Read())
                {
                    Dictionary<string, string> dictionary = new Dictionary<string, string>();
                    int minArrayLength = Math.Min(csvReader.ColumnCount, headers.Length);
                    if (csvReader.ColumnCount != headers.Length)
                    {
                        logger.Warn(
                            $"The number of columns in the row do not match the number of columns in the header " +
                            $"{csvReader.ColumnCount} != {headers.Length}");
                    }
                    for (int i = 0; i < minArrayLength; i++)
                    {
                        string field = csvReader.GetField(i);
                        if (!string.IsNullOrWhiteSpace(field))
                        {
                            dictionary[headers[i]] = field;
                        }
                    }
                    loadedFields.Add(dictionary);
                }
            }

            return loadedFields.Select(fields => BuildLinkFromData(fields)).ToList();
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
                    writer.WriteField(value);
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

        private static Link BuildLinkFromData(Dictionary<string, string> dictionary)
        {
            StringDictionary contextDictionary = new StringDictionary();
            foreach (KeyValuePair<string, string> entry in dictionary)
            {
                contextDictionary[entry.Key] = entry.Value;
            }
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