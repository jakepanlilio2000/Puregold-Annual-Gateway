using System;
using System.Data;

namespace LocatorAutoPrint.Helpers
{
    public static class SqlDataExtensions
    {
        public static string GetStringSafe(this IDataRecord reader, string columnName, string defaultValue = "")
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                return reader.IsDBNull(ordinal) ? defaultValue : (reader.GetValue(ordinal)?.ToString()?.Trim() ?? defaultValue);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static double GetDoubleSafe(this IDataRecord reader, string columnName, double defaultValue = 0.0)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal)) return defaultValue;
                var val = reader.GetValue(ordinal);
                return Convert.ToDouble(val);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static int GetInt32Safe(this IDataRecord reader, string columnName, int defaultValue = 0)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal)) return defaultValue;
                var val = reader.GetValue(ordinal);
                return Convert.ToInt32(val);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static bool GetBooleanSafe(this IDataRecord reader, string columnName, bool defaultValue = false)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal)) return defaultValue;
                var val = reader.GetValue(ordinal);
                if (val is bool b) return b;
                string s = val.ToString().Trim();
                return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static string GetDecimalStringSafe(this IDataRecord reader, string columnName, string format = "0", string defaultValue = "")
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal)) return defaultValue;
                var val = reader.GetValue(ordinal);
                if (val != null && decimal.TryParse(val.ToString(), out decimal dec))
                {
                    return dec.ToString(format);
                }
                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        public static DateTime? GetDateTimeSafe(this IDataRecord reader, string columnName)
        {
            try
            {
                int ordinal = reader.GetOrdinal(columnName);
                if (reader.IsDBNull(ordinal)) return null;
                var val = reader.GetValue(ordinal);
                if (val is DateTime dt) return dt;
                if (DateTime.TryParse(val?.ToString(), out DateTime parsed)) return parsed;
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
