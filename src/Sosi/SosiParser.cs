using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Sosi.Internal;

namespace Sosi
{
    /// <summary>
    /// Parser for SOSI files, the .NET counterpart of <c>SOSI.Parser</c> in sosi.js (lite).
    /// </summary>
    /// <example>
    /// <code>
    /// var parser = new SosiParser();
    /// var sosidata = parser.Parse(text);
    /// var srid = sosidata.Header.Srid;
    /// string geojson = sosidata.Export(FileTypes.GeoJson);
    /// </code>
    /// </example>
    public class SosiParser
    {
        private static readonly Regex CharsetPattern = new Regex(@"\.\.TEGNSETT\s+(\S+)", RegexOptions.CultureInvariant);

        /// <summary>The supported output formats (sosi.js: <c>parser.getFormats()</c>).</summary>
        public static IReadOnlyList<FileTypes> Formats { get; } = (FileTypes[])Enum.GetValues(typeof(FileTypes));

        /// <summary>Parses SOSI data (newline separated file content).</summary>
        /// <exception cref="SosiParseException">The data is not valid SOSI.</exception>
        public SosiData Parse(string data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            return new SosiData(SosiText.ParseSections(SosiText.SplitLines(data)));
        }

        /// <summary>Reads (see <see cref="ReadAllText"/>) and parses a SOSI file.</summary>
        public SosiData ParseFile(string path)
        {
            return Parse(ReadAllText(path));
        }

        /// <summary>
        /// Reads a SOSI file as text. The encoding is detected from the byte order mark, otherwise
        /// UTF-8 is used if the content is valid UTF-8, otherwise the encoding given by
        /// <c>..TEGNSETT</c> (ISO8859-1, ISO8859-10, ANSI, DOSN8, ND7/DECN7, ...) is used.
        /// </summary>
        public static string ReadAllText(string path)
        {
            return Decode(File.ReadAllBytes(path));
        }

        /// <summary>Decodes the bytes of a SOSI file, see <see cref="ReadAllText"/>.</summary>
        public static string Decode(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                // Not UTF-8, use the declared character set
            }

            var head = Encoding.GetEncoding(28591).GetString(bytes, 0, Math.Min(bytes.Length, 4096));
            var match = CharsetPattern.Match(head);
            var charset = match.Success ? match.Groups[1].Value.ToUpperInvariant() : "ANSI";

            if (charset == "ND7" || charset == "DECN7")
            {
                // 7-bit Norwegian: [\]{|} are used for ÆØÅæøå
                return Encoding.ASCII.GetString(bytes)
                    .Replace('[', 'Æ').Replace('\\', 'Ø').Replace(']', 'Å')
                    .Replace('{', 'æ').Replace('|', 'ø').Replace('}', 'å');
            }
            return GetEncoding(charset).GetString(bytes);
        }

        private static Encoding GetEncoding(string charset)
        {
            switch (charset)
            {
                case "ISO8859-1":
                    return Encoding.GetEncoding(28591);
                case "ISO8859-10":
                    try
                    {
                        return Encoding.GetEncoding("iso-8859-10");
                    }
                    catch (ArgumentException)
                    {
                        // Not available on Windows; ÆØÅæøå have the same code points as in ISO8859-1
                        return Encoding.GetEncoding(28591);
                    }
                case "ISO8859-15":
                    return Encoding.GetEncoding(28605);
                case "DOSN8":
                    return Encoding.GetEncoding(865);
                default: // ANSI and unknown
                    return Encoding.GetEncoding(1252);
            }
        }
    }
}
