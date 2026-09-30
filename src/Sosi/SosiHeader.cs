using System;
using System.Collections.Generic;
using System.Globalization;
using Sosi.Internal;

namespace Sosi
{
    /// <summary>Bounding box of the data set (from <c>..OMRÅDE</c>). X = east, Y = north.</summary>
    public sealed class BoundingBox
    {
        public BoundingBox(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }

        /// <summary>The box as <c>[minX, minY, maxX, maxY]</c> (same as <c>hode.bbox</c> in sosi.js).</summary>
        public double[] ToArray() => new[] { MinX, MinY, MaxX, MaxY };

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "[{0}, {1}, {2}, {3}]", MinX, MinY, MaxX, MaxY);
    }

    /// <summary>Origin that all coordinates in the file are relative to (from <c>...ORIGO-NØ</c>).</summary>
    public sealed class Origin
    {
        public Origin(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }

    /// <summary>The SOSI file header (<c>.HODE</c>), corresponding to <c>sosidata.hode</c> in sosi.js.</summary>
    public sealed class SosiHeader
    {
        private SosiHeader()
        {
        }

        /// <summary>EIER, with quotes removed. Empty string if missing.</summary>
        public string Owner { get; private set; }

        /// <summary>PRODUSENT, with quotes removed. Empty string if missing.</summary>
        public string Producer { get; private set; }

        /// <summary>OBJEKTKATALOG as written in the file, or null.</summary>
        public string ObjectCatalog { get; private set; }

        /// <summary>VERIFISERINGSDATO as written in the file (e.g. "19890623"), or null.</summary>
        public string VerificationDate { get; private set; }

        /// <summary>SOSI-VERSJON, or null if missing.</summary>
        public double? Version { get; private set; }

        /// <summary>SOSI-NIVÅ, or null if missing.</summary>
        public double? Level { get; private set; }

        /// <summary>KVALITET, with quotes removed. Empty string if missing.</summary>
        public string Quality { get; private set; }

        /// <summary>Bounding box from OMRÅDE (sosi.js: <c>hode.bbox</c>), or null if missing.</summary>
        public BoundingBox BoundingBox { get; private set; }

        /// <summary>TRANSPAR/ORIGO-NØ (sosi.js: <c>hode.origo</c>).</summary>
        public Origin Origin { get; private set; }

        /// <summary>TRANSPAR/ENHET, the coordinate unit (sosi.js: <c>hode.enhet</c>).</summary>
        public double Unit { get; private set; }

        /// <summary>TRANSPAR/VERT-DATUM, with quotes removed. Empty string if missing.</summary>
        public string VerticalDatum { get; private set; }

        /// <summary>The SRID as a string, e.g. "EPSG:27395" (from KOORDSYS or GEOSYS).</summary>
        public string Srid { get; private set; }

        /// <summary>The numeric EPSG code of <see cref="Srid"/>, e.g. 27395.</summary>
        public int EpsgCode { get; private set; }

        /// <summary>
        /// All header values as parsed from the file. Values are <see cref="string"/>,
        /// <see cref="IReadOnlyList{T}"/> of strings or <see cref="IReadOnlyDictionary{TKey,TValue}"/> of strings.
        /// </summary>
        public IReadOnlyDictionary<string, object> Attributes { get; private set; }

        internal static SosiHeader Parse(IEnumerable<string> lines)
        {
            var data = SosiText.ParseAttributes(lines);
            var header = new SosiHeader
            {
                Attributes = data,
                Owner = GetUnquoted(data, "EIER"),
                Producer = GetUnquoted(data, "PRODUSENT"),
                ObjectCatalog = GetRaw(data, "OBJEKTKATALOG"),
                VerificationDate = GetRaw(data, "VERIFISERINGSDATO"),
                Version = ToNullable(SosiText.ParseFloat(GetRaw(data, "SOSI-VERSJON"))),
                Level = ToNullable(SosiText.ParseFloat(GetRaw(data, "SOSI-NIVÅ"))),
                Quality = GetUnquoted(data, "KVALITET")
            };

            var area = GetGroup(data, "OMRÅDE");
            if (area != null)
            {
                header.BoundingBox = ParseBoundingBox(area);
            }

            var transpar = GetGroup(data, "TRANSPAR");
            if (transpar == null)
            {
                throw new SosiParseException("The header is missing TRANSPAR.");
            }

            string origo;
            if (!transpar.TryGetValue("ORIGO-NØ", out origo))
            {
                throw new SosiParseException("The header is missing TRANSPAR/ORIGO-NØ.");
            }
            var origoValues = SosiText.SplitWhitespace(origo);
            header.Origin = new Origin(
                SosiText.ParseFloat(origoValues.Length > 1 ? origoValues[1] : null),
                SosiText.ParseFloat(origoValues.Length > 0 ? origoValues[0] : null));

            string unit;
            if (!transpar.TryGetValue("ENHET", out unit))
            {
                throw new SosiParseException("The header is missing TRANSPAR/ENHET.");
            }
            header.Unit = SosiText.ParseFloat(unit);

            header.VerticalDatum = GetUnquoted(transpar, "VERT-DATUM");
            header.Srid = GetSrid(transpar);
            header.EpsgCode = int.Parse(header.Srid.Substring(header.Srid.IndexOf(':') + 1), CultureInfo.InvariantCulture);
            return header;
        }

        private static string GetSrid(IReadOnlyDictionary<string, string> transpar)
        {
            string koordsys;
            if (transpar.TryGetValue("KOORDSYS", out koordsys))
            {
                var code = SosiText.ParseInt(koordsys);
                CoordinateSystemInfo info;
                if (!double.IsNaN(code) && CoordinateSystems.Koordsys.TryGetValue((int)code, out info))
                {
                    return info.Srid;
                }
                throw new SosiParseException("KOORDSYS = " + koordsys + " not found!");
            }

            string geosys;
            if (transpar.TryGetValue("GEOSYS", out geosys))
            {
                var values = SosiText.SplitWhitespace(geosys);
                var code = SosiText.ParseInt(values.Length > 0 ? values[0] : null);
                CoordinateSystemInfo info;
                if (!double.IsNaN(code) && CoordinateSystems.Geosys.TryGetValue((int)code, out info))
                {
                    return info.Srid;
                }
                throw new SosiParseException("GEOSYS = " + geosys + " not found!");
            }

            throw new SosiParseException("The header has neither TRANSPAR/KOORDSYS nor TRANSPAR/GEOSYS.");
        }

        private static BoundingBox ParseBoundingBox(IReadOnlyDictionary<string, string> area)
        {
            string min, max;
            if (!area.TryGetValue("MIN-NØ", out min) || !area.TryGetValue("MAX-NØ", out max))
            {
                return null;
            }
            var ll = SosiText.SplitWhitespace(min);
            var ur = SosiText.SplitWhitespace(max);
            return new BoundingBox(
                SosiText.ParseFloat(ll.Length > 1 ? ll[1] : null),
                SosiText.ParseFloat(ll.Length > 0 ? ll[0] : null),
                SosiText.ParseFloat(ur.Length > 1 ? ur[1] : null),
                SosiText.ParseFloat(ur.Length > 0 ? ur[0] : null));
        }

        private static IReadOnlyDictionary<string, string> GetGroup(IReadOnlyDictionary<string, object> data, string key)
        {
            object value;
            return data.TryGetValue(key, out value) ? value as IReadOnlyDictionary<string, string> : null;
        }

        private static string GetRaw(IReadOnlyDictionary<string, object> data, string key)
        {
            object value;
            return data.TryGetValue(key, out value) ? SosiText.AsString(value) : null;
        }

        private static string GetUnquoted(IReadOnlyDictionary<string, object> data, string key)
        {
            return (GetRaw(data, key) ?? string.Empty).Replace("\"", string.Empty);
        }

        private static string GetUnquoted(IReadOnlyDictionary<string, string> data, string key)
        {
            string value;
            return data.TryGetValue(key, out value) ? value.Replace("\"", string.Empty) : string.Empty;
        }

        private static double? ToNullable(double value) => double.IsNaN(value) ? (double?)null : value;
    }
}
