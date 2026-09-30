using System;
using System.Collections.Generic;
using Sosi.Internal;

namespace Sosi
{
    /// <summary>A feature (object) in a SOSI file, e.g. <c>.KURVE 250:</c>.</summary>
    public sealed class SosiFeature
    {
        private readonly List<string> _geometryLines;
        private readonly string _refs;
        private readonly Origin _origo;
        private readonly double _unit;
        private Geometry _geometry;
        private bool _building;

        internal SosiFeature(int id, string geometryType, IEnumerable<string> lines, Origin origo, double unit)
        {
            Id = id;
            GeometryType = geometryType;
            _origo = origo;
            _unit = unit;

            var attributeLines = new List<string>();
            var refLines = new List<string>();
            _geometryLines = new List<string>();
            var foundGeometry = false;
            var foundRef = false;

            foreach (var original in lines)
            {
                var line = original;
                if (IsGeometryStart(line))
                {
                    // The coordinates may be on the same line as ..NØ[H] («..NØ[H] x y [h]»)
                    var token = SosiText.FirstToken(line);
                    if (token.Length < line.Length)
                    {
                        _geometryLines.Add(token);
                        _geometryLines.Add(line.Substring(token.Length).Trim());
                    }
                    foundGeometry = true;
                }
                if (foundGeometry)
                {
                    _geometryLines.Add(line);
                    continue;
                }
                if (IsRefStart(line))
                {
                    foundRef = true;
                    line = SosiText.ReplaceFirst(line, "..REF", string.Empty).Trim();
                }
                if (foundRef)
                {
                    if (line.Length > 0 && line[0] == '.')
                    {
                        foundRef = false;
                        attributeLines.Add(line);
                    }
                    else
                    {
                        refLines.Add(line);
                    }
                }
                else
                {
                    attributeLines.Add(line);
                }
            }

            var attributes = SosiText.ParseAttributes(attributeLines);
            if (refLines.Count > 0)
            {
                _refs = string.Join(" ", refLines);
                if (geometryType != "FLATE")
                {
                    // For FLATE the references are turned into the geometry instead
                    attributes["REF"] = _refs;
                }
            }
            object enhet;
            if (attributes.TryGetValue("ENHET", out enhet))
            {
                var featureUnit = SosiText.ParseFloat(SosiText.AsString(enhet));
                if (featureUnit != 0 && !double.IsNaN(featureUnit))
                {
                    _unit = featureUnit;
                }
            }
            Attributes = attributes;
        }

        /// <summary>The feature id (serial number), e.g. 250 for <c>.KURVE 250:</c>.</summary>
        public int Id { get; }

        /// <summary>The SOSI geometry type, e.g. "PUNKT", "KURVE", "BUEP" or "FLATE".</summary>
        public string GeometryType { get; }

        /// <summary>
        /// The attributes of the feature, keyed by SOSI name (e.g. "OBJTYPE"). Values are
        /// <see cref="string"/> (as written in the file, including any quotes),
        /// <see cref="IReadOnlyList{T}"/> of strings (repeated attributes) or
        /// <see cref="IReadOnlyDictionary{TKey,TValue}"/> of strings (grouped attributes).
        /// </summary>
        public IReadOnlyDictionary<string, object> Attributes { get; }

        /// <summary>
        /// The geometry: <see cref="Point"/>, <see cref="LineString"/> (incl. <see cref="LineStringFromArc"/>)
        /// or <see cref="Polygon"/>. It is built on first access, like in sosi.js.
        /// </summary>
        /// <exception cref="SosiParseException">The geometry is invalid or references missing features.</exception>
        /// <exception cref="NotSupportedException">The geometry type is not supported.</exception>
        public Geometry Geometry
        {
            get
            {
                if (_geometry == null)
                {
                    _geometry = BuildGeometry();
                }
                return _geometry;
            }
        }

        internal SosiFeatureCollection Owner { get; set; }

        /// <summary>Returns the attribute value as a string (lists are joined by a space), or null if missing.</summary>
        public string GetAttributeString(string key)
        {
            object value;
            return Attributes.TryGetValue(key, out value) ? SosiText.AsString(value) : null;
        }

        public override string ToString() => GeometryType + " " + Id;

        private Geometry BuildGeometry()
        {
            if (_building)
            {
                throw new SosiParseException("Circular reference while building the geometry of " + this);
            }
            _building = true;
            try
            {
                switch (GeometryType)
                {
                    case "PUNKT":
                    case "TEKST": // a point with extra styling hints - the geometry may consist of up to three points
                        if (_geometryLines.Count < 2)
                        {
                            throw new SosiParseException(this + " has no coordinates");
                        }
                        return Point.Parse(_geometryLines[1], _origo, _unit);
                    case "KURVE":
                    case "LINJE": // old 4.0 name for unsmoothed KURVE
                        return LineString.Parse(_geometryLines, _origo, _unit);
                    case "BUEP":
                        return LineStringFromArc.Parse(_geometryLines, _origo, _unit);
                    case "FLATE":
                        if (_refs == null)
                        {
                            throw new SosiParseException(this + " has no REF");
                        }
                        return Polygon.Build(_refs, _geometryLines, _origo, _unit, Owner);
                    default:
                        throw new NotSupportedException("GeometryType " + GeometryType + " is not handled (yet..?)");
                }
            }
            finally
            {
                _building = false;
            }
        }

        /// <summary>A line <c>..NØ</c> or <c>..NØH</c>, optionally followed by coordinates.</summary>
        private static bool IsGeometryStart(string line)
        {
            if (SosiText.CountStartingDots(line) != 2)
            {
                return false;
            }
            var token = SosiText.FirstToken(line);
            return token == "..NØ" || token == "..NØH";
        }

        private static bool IsRefStart(string line)
        {
            return SosiText.CountStartingDots(line) == 2 && SosiText.FirstToken(line) == "..REF"
                || line.StartsWith("..REF:", StringComparison.Ordinal);
        }
    }
}
