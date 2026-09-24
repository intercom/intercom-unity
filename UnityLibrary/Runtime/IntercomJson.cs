using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Intercom
{
    internal static class IntercomJson
    {
        internal static string Serialize(IDictionary<string, object> map)
        {
            if (map == null || map.Count == 0)
            {
                return "{}";
            }

            var sb = new StringBuilder();
            AppendMembers(sb, map);
            return sb.ToString();
        }

        internal static string Serialize(IDictionary<string, string> map)
        {
            if (map == null || map.Count == 0)
            {
                return "{}";
            }

            var sb = new StringBuilder();
            AppendMembers(sb, map);
            return sb.ToString();
        }

        private static void AppendMembers<T>(StringBuilder sb, IEnumerable<KeyValuePair<string, T>> entries)
        {
            sb.Append('{');
            bool first = true;
            foreach (var kvp in entries)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                first = false;
                AppendString(sb, kvp.Key);
                sb.Append(':');
                AppendValue(sb, kvp.Value);
            }
            sb.Append('}');
        }

        private static void AppendArray(StringBuilder sb, IEnumerable items)
        {
            sb.Append('[');
            bool first = true;
            foreach (var item in items)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                first = false;
                AppendValue(sb, item);
            }
            sb.Append(']');
        }

        private static void AppendValue(StringBuilder sb, object value)
        {
            if (value == null)
            {
                sb.Append("null");
            }
            else if (value is string str)
            {
                AppendString(sb, str);
            }
            else if (value is bool boolValue)
            {
                sb.Append(boolValue ? "true" : "false");
            }
            else if (IsIntegerType(value))
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            else if (value is float floatValue)
            {
                // Some locales use a comma decimal separator; without InvariantCulture this would
                // produce invalid JSON (e.g. "1,5" instead of "1.5").
                sb.Append(floatValue.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value is double doubleValue)
            {
                sb.Append(doubleValue.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value is decimal decimalValue)
            {
                sb.Append(decimalValue.ToString(CultureInfo.InvariantCulture));
            }
            else if (value is DateTime dateTime)
            {
                sb.Append(ToUnixSeconds(dateTime));
            }
            else if (value is DateTimeOffset dateTimeOffset)
            {
                sb.Append(dateTimeOffset.ToUnixTimeSeconds());
            }
            else if (value is IDictionary<string, object> nested)
            {
                AppendMembers(sb, nested);
            }
            else if (value is IEnumerable enumerable)
            {
                AppendArray(sb, enumerable);
            }
            else
            {
                AppendString(sb, value.ToString());
            }
        }

        private static bool IsIntegerType(object value)
        {
            return value is sbyte || value is byte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong;
        }

        private static long ToUnixSeconds(DateTime dateTime)
        {
            var utc = dateTime.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                : dateTime.ToUniversalTime();
            return new DateTimeOffset(utc).ToUnixTimeSeconds();
        }

        private static void AppendString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
