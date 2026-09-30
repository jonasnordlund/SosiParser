using System;
using System.Collections.Generic;
using System.Linq;
using Sosi.Export;
using Sosi.Internal;

namespace Sosi
{
    /// <summary>Output formats supported by <see cref="SosiData.Export"/>.</summary>
    public enum FileTypes
    {
        GeoJson,
        TopoJson
    }

    /// <summary>A parsed SOSI file.</summary>
    public sealed class SosiData
    {
        private static readonly HashSet<string> NonFeatureSections = new HashSet<string> { "HODE", "HODE 0", "DEF", "OBJDEF", "SLUTT" };

        internal SosiData(IList<Section> sections)
        {
            var head = sections.FirstOrDefault(s => s.Key == "HODE" || s.Key == "HODE 0");
            if (head == null)
            {
                throw new SosiParseException("The SOSI data has no .HODE section.");
            }
            Header = SosiHeader.Parse(head.Lines);

            var features = new List<SosiFeature>();
            foreach (var section in sections.Where(s => !NonFeatureSections.Contains(s.Key)))
            {
                var key = SosiText.SplitWhitespace(section.Key);
                var id = SosiText.ParseInt(key.Length > 1 ? key[1] : null);
                if (double.IsNaN(id))
                {
                    throw new SosiParseException("Feature must have ID! (." + section.Key + ")");
                }
                features.Add(new SosiFeature((int)id, key[0], section.Lines, Header.Origin, Header.Unit));
            }
            Features = new SosiFeatureCollection(features);
        }

        /// <summary>The file header (sosi.js: <c>sosidata.hode</c>).</summary>
        public SosiHeader Header { get; }

        /// <summary>All features ordered by id (sosi.js: <c>sosidata.features.all()</c>).</summary>
        public SosiFeatureCollection Features { get; }

        /// <summary>All features in file order (sosi.js: <c>sosidata.features.all(true)</c>).</summary>
        public IReadOnlyList<SosiFeature> FileOrderedFeatures => Features.InFileOrder;

        /// <summary>
        /// Exports the data as JSON (sosi.js: <c>sosidata.dumps(format, name)</c>).
        /// </summary>
        /// <param name="format">The output format.</param>
        /// <param name="objectName">TopoJSON only: the name of the object collection. Defaults to "features".</param>
        /// <param name="indented">True to pretty print the JSON.</param>
        public string Export(FileTypes format, string objectName = null, bool indented = false)
        {
            object json;
            switch (format)
            {
                case FileTypes.GeoJson:
                    json = GeoJsonExporter.Build(this);
                    break;
                case FileTypes.TopoJson:
                    json = TopoJsonExporter.Build(this, objectName ?? "features");
                    break;
                default:
                    throw new NotSupportedException("Outputformat " + format + " is not supported!");
            }
            return JsonWriter.Serialize(json, indented);
        }
    }
}
