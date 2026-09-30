using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Sosi.Internal;

namespace Sosi
{
    /// <summary>Base class for the geometry of a <see cref="SosiFeature"/>.</summary>
    public abstract class Geometry
    {
        internal Geometry()
        {
        }
    }

    /// <summary>A point (SOSI PUNKT/TEKST, or a vertex of a line or polygon).</summary>
    public sealed class Point : Geometry
    {
        public Point(double x, double y)
            : this(x, y, null)
        {
        }

        public Point(double x, double y, double? z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>East coordinate.</summary>
        public double X { get; }

        /// <summary>North coordinate.</summary>
        public double Y { get; }

        /// <summary>Height, if given in the file (NØH).</summary>
        public double? Z { get; }

        /// <summary>True if the point is marked as a tie point (knutepunkt, <c>...KP</c>).</summary>
        public bool HasTiepoint { get; private set; }

        /// <summary>The tie point code (knutepunktkode) if <see cref="HasTiepoint"/>.</summary>
        public int? TiepointCode { get; private set; }

        /// <summary>Parses a coordinate line such as <c>23456 2345 ...KP 1</c>.</summary>
        internal static Point Parse(string line, Origin origo, double unit)
        {
            var coords = SosiText.SplitWhitespace(line);

            var numDecimals = 0;
            if (unit < 1)
            {
                numDecimals = -(int)Math.Floor(Math.Log(unit) / Math.Log(10));
            }

            var y = SosiText.Round(SosiText.ParseInt(coords.Length > 0 ? coords[0] : null) * unit + origo.Y, numDecimals);
            var x = SosiText.Round(SosiText.ParseInt(coords.Length > 1 ? coords[1] : null) * unit + origo.X, numDecimals);
            double? z = null;
            if (coords.Length > 2 && SosiText.IsNumeric(coords[2]))
            {
                z = SosiText.Round(SosiText.ParseInt(coords[2]) * unit, numDecimals);
            }

            var point = new Point(x, y, z);

            var kp = line.IndexOf(".KP", StringComparison.Ordinal);
            if (kp != -1)
            {
                var parts = SosiText.SplitWhitespace(line.Substring(kp));
                var code = SosiText.ParseInt(parts.Length > 1 ? parts[1] : null);
                point.HasTiepoint = true;
                point.TiepointCode = double.IsNaN(code) ? (int?)null : (int)code;
            }
            return point;
        }

        public override string ToString() => Z.HasValue
            ? string.Format(CultureInfo.InvariantCulture, "POINT Z ({0} {1} {2})", X, Y, Z.Value)
            : string.Format(CultureInfo.InvariantCulture, "POINT ({0} {1})", X, Y);
    }

    /// <summary>A line (SOSI KURVE/LINJE). In sosi.js the points are called <c>kurve</c>.</summary>
    public class LineString : Geometry
    {
        internal LineString(IList<Point> points, IList<Point> tiepoints)
        {
            Points = new ReadOnlyCollection<Point>(points);
            Tiepoints = new ReadOnlyCollection<Point>(tiepoints);
        }

        /// <summary>The vertices of the line (sosi.js: <c>kurve</c>).</summary>
        public IReadOnlyList<Point> Points { get; }

        /// <summary>The vertices marked as tie points (sosi.js: <c>knutepunkter</c>).</summary>
        public IReadOnlyList<Point> Tiepoints { get; }

        internal static LineString Parse(IEnumerable<string> lines, Origin origo, double unit)
        {
            var points = CoordinateLines(lines).Select(line => Point.Parse(line, origo, unit)).ToList();
            return new LineString(points, points.Where(p => p.HasTiepoint).ToList());
        }

        /// <summary>Removes the <c>..NØ</c>/<c>..NØH</c> marker lines, keeping only coordinate lines.</summary>
        internal static IEnumerable<string> CoordinateLines(IEnumerable<string> lines)
        {
            return lines.Where(line => line.IndexOf("NØ", StringComparison.Ordinal) == -1);
        }
    }

    /// <summary>
    /// An arc (SOSI BUEP) defined by three points on a circle, interpolated into a line.
    /// </summary>
    public sealed class LineStringFromArc : LineString
    {
        private LineStringFromArc(IList<Point> points, IList<Point> tiepoints)
            : base(points, tiepoints)
        {
        }

        internal static new LineStringFromArc Parse(IEnumerable<string> lines, Origin origo, double unit)
        {
            var p = CoordinateLines(lines).Select(line => Point.Parse(line, origo, unit)).ToList();
            if (p.Count != 3)
            {
                throw new SosiParseException("BUEP er ikke definert med 3 punkter");
            }

            // Same variable names as in sosi.js, which in turn copied them from its author's formulas.
            double e1 = p[0].X, e2 = p[1].X, e3 = p[2].X;
            double n1 = p[0].Y, n2 = p[1].Y, n3 = p[2].Y;

            // helper constants
            var p12 = (e1 * e1 - e2 * e2 + n1 * n1 - n2 * n2) / 2.0;
            var p13 = (e1 * e1 - e3 * e3 + n1 * n1 - n3 * n3) / 2.0;

            double dE12 = e1 - e2,
                dE13 = e1 - e3,
                dN12 = n1 - n2,
                dN13 = n1 - n3;

            // center of the circle
            var cE = (dN13 * p12 - dN12 * p13) / (dE12 * dN13 - dN12 * dE13);
            var cN = (dE13 * p12 - dE12 * p13) / (dN12 * dE13 - dE12 * dN13);

            // radius of the circle
            var r = Math.Sqrt(Math.Pow(e1 - cE, 2) + Math.Pow(n1 - cN, 2));

            // angles of points A and B (1 and 3)
            var th1 = Math.Atan2(n1 - cN, e1 - cE);
            var th3 = Math.Atan2(n3 - cN, e3 - cE);

            // interpolation step in radians
            var dth = th3 - th1;
            if (dth < 0)
            {
                dth += 2 * Math.PI;
            }
            if (dth > Math.PI)
            {
                dth = -2 * Math.PI + dth;
            }
            // NB: evaluates as (32 * dth / 2) * PI, exactly like sosi.js
            var npt = (int)Math.Floor(32 * dth / 2 * Math.PI);
            if (npt < 0)
            {
                npt = -npt;
            }
            if (npt < 3)
            {
                npt = 3;
            }

            dth = dth / (npt - 1);

            var points = new List<Point>(npt);
            for (var i = 0; i < npt; i++)
            {
                var x = cE + r * Math.Cos(th1 + dth * i);
                var y = cN + r * Math.Sin(th1 + dth * i);
                if (double.IsNaN(x))
                {
                    throw new SosiParseException("BUEP: Interpolated " + x + " for point " + i + " of " + npt + " in curve.");
                }
                points.Add(new Point(x, y));
            }

            return new LineStringFromArc(points, p.Where(point => point.HasTiepoint).ToList());
        }
    }

    /// <summary>A polygon (SOSI FLATE) built from the lines it references.</summary>
    public sealed class Polygon : Geometry
    {
        private Polygon(IList<Point> shell, IList<IReadOnlyList<Point>> holes, IList<int> shellRefs, IList<IReadOnlyList<int>> holeRefs, Point center)
        {
            Shell = new ReadOnlyCollection<Point>(shell);
            Holes = new ReadOnlyCollection<IReadOnlyList<Point>>(holes);
            ShellRefs = new ReadOnlyCollection<int>(shellRefs);
            HoleRefs = new ReadOnlyCollection<IReadOnlyList<int>>(holeRefs);
            Center = center;
        }

        /// <summary>The closed outer ring (sosi.js: <c>flate</c>).</summary>
        public IReadOnlyList<Point> Shell { get; }

        /// <summary>The closed inner rings (islands / holes).</summary>
        public IReadOnlyList<IReadOnlyList<Point>> Holes { get; }

        /// <summary>The referenced feature ids of the outer ring. Negative = reversed direction.</summary>
        public IReadOnlyList<int> ShellRefs { get; }

        /// <summary>The referenced feature ids of each inner ring. Negative = reversed direction.</summary>
        public IReadOnlyList<IReadOnlyList<int>> HoleRefs { get; }

        /// <summary>The representative point of the polygon (the FLATE's own <c>..NØ</c>), or null.</summary>
        public Point Center { get; }

        internal static Polygon Build(string refs, IList<string> geometryLines, Origin origo, double unit, SosiFeatureCollection features)
        {
            var shellText = refs;
            var holesText = string.Empty;
            var index = refs.IndexOf('(');
            if (index != -1)
            {
                shellText = refs.Substring(0, index);
                holesText = refs.Substring(index);
            }

            var shellRefs = ParseRefs(shellText);

            var holeTexts = new List<string>();
            foreach (var character in holesText)
            {
                if (character == '(')
                {
                    holeTexts.Add(string.Empty);
                }
                else if (character != ')')
                {
                    holeTexts[holeTexts.Count - 1] += character;
                }
            }
            var holeRefs = holeTexts.Select(ParseRefs).ToList();

            var shell = CreateRing(shellRefs, features);

            var holes = new List<IReadOnlyList<Point>>();
            foreach (var hole in holeRefs)
            {
                if (hole.Count == 1)
                {
                    // An island described by another FLATE
                    var feature = features.GetById(Math.Abs(hole[0]));
                    if (feature != null && feature.GeometryType == "FLATE")
                    {
                        holes.Add(((Polygon)feature.Geometry).Shell);
                        continue;
                    }
                }
                holes.Add(new ReadOnlyCollection<Point>(CreateRing(hole, features)));
            }

            var center = geometryLines.Count > 1 ? Point.Parse(geometryLines[1], origo, unit) : null;

            return new Polygon(
                shell,
                holes,
                shellRefs,
                holeRefs.Select(h => (IReadOnlyList<int>)new ReadOnlyCollection<int>(h)).ToList(),
                center);
        }

        private static List<Point> CreateRing(IList<int> refs, SosiFeatureCollection features)
        {
            var ring = new List<Point>();
            foreach (var reference in refs)
            {
                var id = Math.Abs(reference);
                var feature = features.GetById(id);
                if (feature == null)
                {
                    throw new SosiParseException("Fant ikke KURVE " + id + " for FLATE");
                }
                var line = feature.Geometry as LineString;
                if (line == null)
                {
                    throw new SosiParseException("Feature " + id + " referenced by FLATE is not a line (" + feature.GeometryType + ")");
                }
                var points = line.Points.ToList();
                if (reference < 0)
                {
                    points.Reverse();
                }
                // Skip the last point of every line since it is the first point of the next one
                ring.AddRange(points.Take(points.Count - 1));
            }
            if (ring.Count == 0)
            {
                throw new SosiParseException("FLATE has an empty ring");
            }
            ring.Add(ring[0]);
            return ring;
        }

        private static List<int> ParseRefs(string refs)
        {
            var result = new List<int>();
            foreach (var reference in SosiText.SplitWhitespace(refs))
            {
                var value = SosiText.ParseInt(SosiText.ReplaceFirst(reference, ":", string.Empty));
                if (double.IsNaN(value))
                {
                    throw new SosiParseException("Invalid REF value '" + reference + "'");
                }
                result.Add((int)value);
            }
            return result;
        }
    }
}
