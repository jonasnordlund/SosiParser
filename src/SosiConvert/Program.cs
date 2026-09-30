using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Sosi;

namespace SosiConvert
{
    internal static class Program
    {
        private const string Usage =
@"usage: SosiConvert <format> <infile.sos> [outfile] [--name <objects>] [--indent]

where: format     : one of [geojson, topojson, info]
       infile.sos : a file in SOSI format
       outfile    : an output file name, omit for stdout
       --name     : name of the TopoJSON object collection (default: features)
       --indent   : pretty print the JSON";

        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
            var indent = args.Contains("--indent");
            string name = null;
            var nameIndex = Array.IndexOf(args, "--name");
            if (nameIndex != -1)
            {
                if (nameIndex + 1 >= args.Length)
                {
                    return Fail("--name requires a value");
                }
                name = args[nameIndex + 1];
                positional.Remove(name);
            }

            if (positional.Count < 2 || positional.Count > 3)
            {
                Console.Error.WriteLine(Usage);
                return 1;
            }

            var format = positional[0].ToLowerInvariant();
            var inFile = positional[1];
            var outFile = positional.Count > 2 ? positional[2] : null;

            if (format != "geojson" && format != "topojson" && format != "info")
            {
                return Fail("Unknown format '" + positional[0] + "'\n\n" + Usage);
            }

            try
            {
                var data = new SosiParser().ParseFile(inFile);
                string output;
                switch (format)
                {
                    case "geojson":
                        output = data.Export(FileTypes.GeoJson, indented: indent);
                        break;
                    case "topojson":
                        output = data.Export(FileTypes.TopoJson, name, indent);
                        break;
                    default:
                        output = Describe(data);
                        break;
                }

                if (outFile == null)
                {
                    var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
                    stdout.Write(output);
                    stdout.Flush();
                }
                else
                {
                    File.WriteAllText(outFile, output, new UTF8Encoding(false));
                }
                return 0;
            }
            catch (Exception ex) when (ex is SosiParseException || ex is NotSupportedException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return Fail(ex.Message);
            }
        }

        private static string Describe(SosiData data)
        {
            var header = data.Header;
            var sb = new StringBuilder();
            sb.AppendLine("Owner:          " + header.Owner);
            sb.AppendLine("Producer:       " + header.Producer);
            sb.AppendLine("SOSI version:   " + header.Version);
            sb.AppendLine("SOSI level:     " + header.Level);
            sb.AppendLine("SRID:           " + header.Srid);
            sb.AppendLine("Bounding box:   " + header.BoundingBox);
            sb.AppendLine("Unit:           " + header.Unit);
            sb.AppendLine("Features:       " + data.Features.Count);
            foreach (var group in data.Features.GroupBy(f => f.GeometryType).OrderBy(g => g.Key))
            {
                sb.AppendLine("  " + group.Key.PadRight(14) + group.Count());
            }
            return sb.ToString();
        }

        private static int Fail(string message)
        {
            Console.Error.WriteLine("Error: " + message);
            return 1;
        }
    }
}
