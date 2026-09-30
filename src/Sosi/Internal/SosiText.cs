using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Sosi.Internal
{
    /// <summary>A top level section of a SOSI file, e.g. <c>.HODE</c> or <c>.KURVE 250:</c>.</summary>
    internal sealed class Section
    {
        public Section(string key)
        {
            Key = key;
            Lines = new List<string>();
        }

        /// <summary>The section key, e.g. "HODE" or "KURVE 250".</summary>
        public string Key { get; }

        /// <summary>All (cleaned) lines belonging to the section, excluding the section line itself.</summary>
        public List<string> Lines { get; }
    }

    /// <summary>
    /// Ordered key/value-list tree for one "dot level" of a SOSI file.
    /// Repeated keys are merged into the same value list (as in sosi.js).
    /// </summary>
    internal sealed class SosiTree
    {
        private readonly Dictionary<string, List<string>> _values = new Dictionary<string, List<string>>();

        public List<string> Keys { get; } = new List<string>();

        public List<string> this[string key] => _values[key];

        public void Add(string key, string value)
        {
            List<string> list;
            if (!_values.TryGetValue(key, out list))
            {
                list = new List<string>();
                _values.Add(key, list);
                Keys.Add(key);
            }
            list.Add(value);
        }
    }

    /// <summary>Low level text handling of the SOSI format (corresponds to util.js and parts of parser.js).</summary>
    internal static class SosiText
    {
        private static readonly char[] Whitespace = { ' ', '\t', '\r', '\n', '\f', '\v', ' ', '﻿' };
        private static readonly Regex FloatPrefix = new Regex(@"^[+-]?(Infinity|(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?)", RegexOptions.CultureInvariant);
        private static readonly Regex IntPrefix = new Regex(@"^[+-]?\d+", RegexOptions.CultureInvariant);

        /// <summary>Splits the file content into lines, strips comments and surrounding whitespace.</summary>
        public static List<string> SplitLines(string data)
        {
            var lines = new List<string>();
            foreach (var raw in data.Split('\n'))
            {
                lines.Add(StripComment(raw).Trim(Whitespace));
            }
            return lines;
        }

        /// <summary>
        /// Removes a trailing "!"-comment. Unlike sosi.js, an exclamation mark inside a quoted
        /// string value (e.g. <c>..NAVN "Hei!"</c>) is not treated as a comment.
        /// </summary>
        public static string StripComment(string line)
        {
            var quote = '\0';
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                    }
                }
                else if (c == '!')
                {
                    return line.Substring(0, i);
                }
                else if ((c == '"' || c == '\'') && (i == 0 || char.IsWhiteSpace(line[i - 1])))
                {
                    quote = c;
                }
            }
            return line;
        }

        public static int CountStartingDots(string line)
        {
            var count = 0;
            while (count < line.Length && line[count] == '.')
            {
                count++;
            }
            return count;
        }

        /// <summary>The first whitespace separated token of a line.</summary>
        public static string FirstToken(string line)
        {
            var index = line.IndexOfAny(Whitespace);
            return index == -1 ? line : line.Substring(0, index);
        }

        /// <summary>Everything after the first whitespace separated token, trimmed.</summary>
        public static string RestAfterFirstToken(string line)
        {
            var index = line.IndexOfAny(Whitespace);
            return index == -1 ? string.Empty : line.Substring(index + 1).Trim(Whitespace);
        }

        public static string[] SplitWhitespace(string value)
        {
            return value.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Splits the file into top level (single dot) sections. Sections with the same key
        /// are merged, like sosi.js does.
        /// </summary>
        public static List<Section> ParseSections(IEnumerable<string> lines)
        {
            var sections = new List<Section>();
            var byKey = new Dictionary<string, Section>();
            Section current = null;
            foreach (var line in lines)
            {
                if (line.Length == 0)
                {
                    continue;
                }
                if (CountStartingDots(line) == 1)
                {
                    var key = GetSectionKey(line.Substring(1));
                    if (!byKey.TryGetValue(key, out current))
                    {
                        current = new Section(key);
                        byKey.Add(key, current);
                        sections.Add(current);
                    }
                    continue;
                }
                if (current != null)
                {
                    current.Lines.Add(line);
                }
            }
            return sections;
        }

        private static string GetSectionKey(string body)
        {
            var colon = body.IndexOf(':');
            if (colon != -1)
            {
                // ".KURVE 250:" => "KURVE 250"
                return string.Join(" ", SplitWhitespace(body.Substring(0, colon)));
            }
            return FirstToken(body);
        }

        /// <summary>Groups lines by the keys found at the given dot level (sosi.js parseTree).</summary>
        public static SosiTree ParseTree(IEnumerable<string> lines, int level)
        {
            var tree = new SosiTree();
            string key = null;
            foreach (var line in lines)
            {
                if (line.Length == 0)
                {
                    continue;
                }
                var value = line;
                if (CountStartingDots(line) == level)
                {
                    key = FirstToken(line.Substring(level));
                    value = RestAfterFirstToken(line);
                }
                if (value.Length > 0 && key != null)
                {
                    tree.Add(key, value);
                }
            }
            return tree;
        }

        /// <summary>
        /// Parses attributes on level 2 (sosi.js util.parseFromLevel2). Values are either
        /// <see cref="string"/>, <see cref="IReadOnlyList{T}"/> of strings (repeated keys) or
        /// <see cref="IReadOnlyDictionary{TKey,TValue}"/> of strings (grouped attributes).
        /// </summary>
        public static Dictionary<string, object> ParseAttributes(IEnumerable<string> lines)
        {
            var tree = ParseTree(lines, 2);
            var result = new Dictionary<string, object>();
            foreach (var key in tree.Keys)
            {
                var values = tree[key];
                if (values[0][0] == '.')
                {
                    result[key] = ParseSubAttributes(values);
                }
                else if (values.Count > 1)
                {
                    result[key] = values.AsReadOnly();
                }
                else
                {
                    result[key] = values[0];
                }
            }
            return result;
        }

        private static IReadOnlyDictionary<string, string> ParseSubAttributes(IEnumerable<string> lines)
        {
            var tree = ParseTree(lines, 3);
            var result = new Dictionary<string, string>();
            foreach (var key in tree.Keys)
            {
                result[key] = tree[key][0];
            }
            return result;
        }

        /// <summary>Converts an attribute value to a string (lists are joined by a space).</summary>
        public static string AsString(object value)
        {
            if (value == null)
            {
                return null;
            }
            var s = value as string;
            if (s != null)
            {
                return s;
            }
            var list = value as IEnumerable<string>;
            if (list != null && !(value is IReadOnlyDictionary<string, string>))
            {
                return string.Join(" ", list);
            }
            return value.ToString();
        }

        /// <summary>Replaces the first occurrence of <paramref name="search"/> (JavaScript String.replace semantics).</summary>
        public static string ReplaceFirst(string value, string search, string replacement)
        {
            var index = value.IndexOf(search, StringComparison.Ordinal);
            return index == -1 ? value : value.Substring(0, index) + replacement + value.Substring(index + search.Length);
        }

        /// <summary>JavaScript parseFloat: parses the longest numeric prefix, NaN if none.</summary>
        public static double ParseFloat(string value)
        {
            if (value == null)
            {
                return double.NaN;
            }
            var match = FloatPrefix.Match(value.TrimStart(Whitespace));
            if (!match.Success)
            {
                return double.NaN;
            }
            var text = match.Value;
            if (text.EndsWith("Infinity", StringComparison.Ordinal))
            {
                return text[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
            }
            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        /// <summary>JavaScript parseInt(value, 10): parses the longest integer prefix, NaN if none.</summary>
        public static double ParseInt(string value)
        {
            if (value == null)
            {
                return double.NaN;
            }
            var match = IntPrefix.Match(value.TrimStart(Whitespace));
            if (!match.Success)
            {
                return double.NaN;
            }
            return double.Parse(match.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        /// <summary>JavaScript isNaN(string) negated, i.e. "is the whole string a number".</summary>
        public static bool IsNumeric(string value)
        {
            double ignored;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out ignored);
        }

        /// <summary>sosi.js util.round: Math.round(number * 10^n) / 10^n.</summary>
        public static double Round(double number, int numDecimals)
        {
            var pow = Math.Pow(10, numDecimals);
            return JsRound(number * pow) / pow;
        }

        /// <summary>JavaScript Math.round (halfway values are rounded towards +Infinity).</summary>
        private static double JsRound(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return value;
            }
            var floor = Math.Floor(value);
            return value - floor >= 0.5 ? floor + 1 : floor;
        }
    }
}
