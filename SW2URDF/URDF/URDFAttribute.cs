using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Runtime.Serialization;
using System.Xml;

namespace SW2URDF.URDF
{
    [DataContract(IsReference = true, Name = "Attribute", Namespace = "http://schemas.datacontract.org/2004/07/SW2URDF")]
    public class URDFAttribute
    {
        public static readonly NumberFormatInfo URDFNumberFormat =
            CultureInfo.CreateSpecificCulture("en-US").NumberFormat;

        public static readonly NumberStyles URDFNumberStyle = NumberStyles.Float;

        [DataMember]
        private bool IsRequired;

        [DataMember]
        private readonly string AttributeType;

        [DataMember]
        public object Value;

        public URDFAttribute(string type, bool isRequired, object initialValue)
        {
            AttributeType = type;
            IsRequired = isRequired;
            Value = initialValue;
        }

        public void WriteURDF(XmlWriter writer)
        {
            if (Value == null)
            {
                if (IsRequired)
                {
                    throw new InvalidOperationException("Required attribute " + AttributeType + " has null value");
                }
                return;
            }
            ValidateValue(AttributeType);
            string valueString = "";
            if (Value.GetType() == typeof(double[]))
            {
                double[] valueArray = (double[])Value;
                foreach (double d in valueArray)
                {
                    valueString +=
                        d.ToString(URDFNumberFormat) + " ";
                }
                valueString = valueString.Trim();
            }
            else if (Value.GetType() == typeof(double))
            {
                valueString =
                    ((Double)Value).ToString(URDFNumberFormat);
            }
            else if (Value.GetType() == typeof(string))
            {
                valueString = (string)Value;
            }
            else if (Value != null)
            {
                throw new Exception("Unhandled object type in write attribute");
            }
            if (String.IsNullOrWhiteSpace(AttributeType))
            {
                throw new Exception("No type specified");
            }
            if (Value != null)
            {
                writer.WriteAttributeString(AttributeType, valueString);
            }
        }

        public void AppendToCSVDictionary(List<string> context, OrderedDictionary dictionary)
        {
            if (Value == null)
            {
                return;
            }
            if (Value.GetType() == typeof(double[]))
            {
                double[] values = (double[])Value;
                for (int i = 0; i < values.Length; i++)
                {
                    string contextString = string.Join(".", context) + "." + AttributeType + "." + AttributeType[i];
                    dictionary.Add(contextString, values[i]);
                }
            }
            else
            {
                string contextString = string.Join(".", context) + "." + AttributeType;
                dictionary.Add(contextString, Value);
            }
        }

        public void SetValueFromData(List<string> context, StringDictionary dictionary)
        {
            string contextString = string.Join(".", context) + "." + AttributeType;
            if (dictionary.ContainsKey(contextString))
            {
                string text = dictionary[contextString];
                if (IsTextAttribute())
                {
                    Value = text;
                }
                else if (AttributeType == "xyz" || AttributeType == "rpy" || AttributeType == "rgba")
                {
                    string[] fields = text.Split(';');
                    if (fields.Length != AttributeType.Length)
                    {
                        throw new FormatException("Invalid vector size for " + contextString);
                    }
                    double[] values = new double[fields.Length];
                    for (int i = 0; i < values.Length; i++)
                    {
                        values[i] = ParseDouble(fields[i], contextString + "." + AttributeType[i]);
                    }
                    Value = values;
                }
                else
                {
                    Value = ParseDouble(text, contextString);
                }
            }
            else if (AttributeType == "xyz" || AttributeType == "rpy" || AttributeType == "rgba")
            {
                // Arrays are represented by one CSV column per component.
                double[] values = Value as double[];
                if (values == null)
                {
                    values = new double[AttributeType.Length];
                }
                values = (double[])values.Clone();
                bool found = false;
                for (int i = 0; i < values.Length; i++)
                {
                    string key = contextString + "." + AttributeType[i];
                    if (dictionary.ContainsKey(key))
                    {
                        values[i] = ParseDouble(dictionary[key], key);
                        found = true;
                    }
                }
                if (found)
                {
                    Value = values;
                }
            }
        }

        private bool IsTextAttribute()
        {
            return AttributeType == "name" || AttributeType == "type" ||
                AttributeType == "filename" || AttributeType == "link" || AttributeType == "joint";
        }

        public static double ParseDouble(string text, string field)
        {
            if (!Double.TryParse(text, URDFNumberStyle, URDFNumberFormat, out double value) ||
                Double.IsNaN(value) || Double.IsInfinity(value))
            {
                throw new FormatException("Invalid number for " + field + ": '" + text +
                    "'. Use a finite number with a dot as the decimal separator.");
            }
            return value;
        }

        public void ValidateValue(string context)
        {
            if (Value == null)
            {
                return;
            }
            string field = context + "." + AttributeType;
            if (IsTextAttribute())
            {
                if (!(Value is string))
                {
                    throw new ArgumentException("Expected text for " + field);
                }
                return;
            }
            if (AttributeType == "xyz" || AttributeType == "rpy" || AttributeType == "rgba")
            {
                double[] values = Value as double[];
                if (values == null || values.Length != AttributeType.Length)
                {
                    throw new ArgumentException("Invalid vector size for " + field);
                }
                foreach (double value in values)
                {
                    ValidateFinite(value, field);
                }
                return;
            }
            if (!(Value is double))
            {
                throw new ArgumentException("Expected a number for " + field);
            }
            ValidateFinite((double)Value, field);
        }

        private static void ValidateFinite(double value, string field)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value))
            {
                throw new ArgumentException("A finite number is required for " + field);
            }
        }

        public static object GetValueFromString(string valueStr)
        {
            if (valueStr == null)
            {
                return null;
            }
            double resultDouble;
            if (valueStr.Contains(";"))
            {
                string[] valueFields = valueStr.Split(';');
                double[] arry = new double[valueFields.Length];
                for (int i = 0; i < valueFields.Length; i++)
                {
                    arry[i] = ParseDouble(valueFields[i], "CSV field");
                }
                return arry;
            }
            else
            {
                if (Double.TryParse(valueStr, URDFNumberStyle, URDFNumberFormat, out resultDouble))
                {
                    ValidateFinite(resultDouble, "CSV field");
                    return resultDouble;
                }
            }
            return valueStr;
        }

        public bool GetIsRequired()
        {
            return IsRequired;
        }

        public void SetRequired(bool required)
        {
            IsRequired = required;
        }

        public bool IsSet()
        {
            return Value != null;
        }

        public string GetTextFromDoubleValue(string format = "G")
        {
            string result = "";
            if (Value != null && Value.GetType() == typeof(double))
            {
                double dValue = (double)Value;
                result = dValue.ToString(format, URDFNumberFormat);
            }
            return result;
        }

        public string[] GetTextArrayFromDoubleArray(string format = "G")
        {
            string[] result = null;
            if (Value != null)
            {
                double[] dArray = (double[])Value;
                result = new string[dArray.Length];
                for (int i = 0; i < dArray.Length; i++)
                {
                    result[i] = dArray[i].ToString(format, URDFNumberFormat);
                }
            }
            return result;
        }

        public void SetDoubleValueFromString(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                Value = null;
                return;
            }
            Value = ParseDouble(text, AttributeType);
        }

        public void SetDoubleArrayFromStringArray(string[] textArray)
        {
            double[] dArray = new double[textArray.Length];
            for (int i = 0; i < textArray.Length; i++)
            {
                dArray[i] = ParseDouble(textArray[i], AttributeType + "." + i);
            }
            Value = dArray;
        }
    }
}
