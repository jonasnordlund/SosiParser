using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Sosi.Internal
{
    /// <summary>A JSON object that keeps the insertion order of its members.</summary>
    internal sealed class JsonObject : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly List<KeyValuePair<string, object>> _members = new List<KeyValuePair<string, object>>();

        public void Add(string key, object value)
        {
            _members.Add(new KeyValuePair<string, object>(key, value));
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _members.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Minimal JSON serializer producing the same output as JavaScript's JSON.stringify
    /// for the object graphs created by the exporters (no external dependencies needed).
    /// </summary>
    internal static class JsonWriter
    {
        public static string Serialize(object value, bool indented)
        {
            var sb = new StringBuilder();
            Write(sb, value, indented, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, bool indented, int depth)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            var s = value as string;
            if (s != null)
            {
                WriteString(sb, s);
                return;
            }

            if (value is bool)
            {
                sb.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is int || value is long || value is short || value is byte)
            {
                sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is double || value is float || value is decimal)
            {
                sb.Append(FormatNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture)));
                return;
            }

            var objectMembers = value as IEnumerable<KeyValuePair<string, object>>;
            if (objectMembers != null)
            {
                var members = new List<KeyValuePair<string, object>>(objectMembers);
                WriteObject(sb, members, indented, depth);
                return;
            }

            var stringMembers = value as IEnumerable<KeyValuePair<string, string>>;
            if (stringMembers != null)
            {
                var members = new List<KeyValuePair<string, object>>();
                foreach (var member in stringMembers)
                {
                    members.Add(new KeyValuePair<string, object>(member.Key, member.Value));
                }
                WriteObject(sb, members, indented, depth);
                return;
            }

            var items = value as IEnumerable;
            if (items != null)
            {
                WriteArray(sb, items, indented, depth);
                return;
            }

            WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder sb, List<KeyValuePair<string, object>> members, bool indented, int depth)
        {
            if (members.Count == 0)
            {
                sb.Append("{}");
                return;
            }
            sb.Append('{');
            for (var i = 0; i < members.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                NewLine(sb, indented, depth + 1);
                WriteString(sb, members[i].Key);
                sb.Append(indented ? ": " : ":");
                Write(sb, members[i].Value, indented, depth + 1);
            }
            NewLine(sb, indented, depth);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, IEnumerable items, bool indented, int depth)
        {
            var first = true;
            sb.Append('[');
            foreach (var item in items)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                NewLine(sb, indented, depth + 1);
                Write(sb, item, indented, depth + 1);
                first = false;
            }
            if (!first)
            {
                NewLine(sb, indented, depth);
            }
            sb.Append(']');
        }

        private static void NewLine(StringBuilder sb, bool indented, int depth)
        {
            if (indented)
            {
                sb.Append('\n').Append(' ', depth * 2);
            }
        }

        /// <summary>Formats a number like JavaScript (NaN/Infinity become null, as in JSON.stringify).</summary>
        internal static string FormatNumber(double number)
        {
            if (double.IsNaN(number) || double.IsInfinity(number))
            {
                return "null";
            }
            if (number == Math.Floor(number) && Math.Abs(number) < 1e15)
            {
                return ((long)number).ToString(CultureInfo.InvariantCulture);
            }
            var text = number.ToString("R", CultureInfo.InvariantCulture);
            var e = text.IndexOf('E');
            if (e == -1)
            {
                return text;
            }
            // .NET "1E-07" => JavaScript "1e-7"
            var exponent = int.Parse(text.Substring(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            return text.Substring(0, e) + "e" + (exponent < 0 ? "-" : "+") + Math.Abs(exponent).ToString(CultureInfo.InvariantCulture);
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
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
                        var loneSurrogate =
                            (char.IsHighSurrogate(c) && (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))) ||
                            (char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(value[i - 1])));
                        if (c < 0x20 || loneSurrogate)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
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
