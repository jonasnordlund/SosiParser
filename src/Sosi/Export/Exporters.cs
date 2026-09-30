using System;
using System.Collections.Generic;
using System.Linq;
using Sosi.Internal;

namespace Sosi.Export
{
    internal static class GeoJsonExporter
    {
        public static JsonObject Build(SosiData data)
        {
            var features = new List<object>();
            foreach (var feature in data.Features)
            {
                var geometry = WriteGeometry(feature.Geometry);
                features.Add(new JsonObject
                {
                    { "type", "Feature" },
                    { "id", feature.Id },
                    { "properties", feature.Attributes },
                    { "geometry", geometry }
                });
            }

            return new JsonObject
            {
                { "type", "FeatureCollection" },
                { "features", features },
                { "crs", new JsonObject
                    {
                        { "type", "name" },
                        { "properties", new JsonObject { { "name", data.Header.Srid } } }
                    }
                }
            };
        }

        private static JsonObject WriteGeometry(Geometry geometry)
        {
            var point = geometry as Point;
            if (point != null)
            {
                return new JsonObject { { "type", "Point" }, { "coordinates", WritePoint(point) } };
            }

            var line = geometry as LineString;
            if (line != null)
            {
                return new JsonObject { { "type", "LineString" }, { "coordinates", WritePoints(line.Points) } };
            }

            var polygon = geometry as Polygon;
            if (polygon != null)
            {
                var rings = new List<object> { WritePoints(polygon.Shell) };
                rings.AddRange(polygon.Holes.Select(WritePoints));
                return new JsonObject { { "type", "Polygon" }, { "coordinates", rings } };
            }

            throw new NotSupportedException("cannot write geometry!");
        }

        internal static List<double> WritePoint(Point point)
        {
            var coordinates = new List<double> { point.X, point.Y };
            if (point.Z.HasValue)
            {
                coordinates.Add(point.Z.Value);
            }
            return coordinates;
        }

        internal static List<object> WritePoints(IEnumerable<Point> points)
        {
            return points.Select(p => (object)WritePoint(p)).ToList();
        }
    }

    internal static class TopoJsonExporter
    {
        private sealed class Line
        {
            public JsonObject Geometry;
            public List<object> Arc;
            public int Index;
        }

        public static JsonObject Build(SosiData data, string name)
        {
            var all = data.Features.ToList();

            var points = all
                .Where(f => f.Geometry is Point)
                .Select(f => (object)new JsonObject
                {
                    { "type", "Point" },
                    { "properties", PropertiesWithId(f) },
                    { "coordinates", GeoJsonExporter.WritePoint((Point)f.Geometry) }
                })
                .ToList();

            var lines = new Dictionary<int, Line>();
            var lineFeatures = all.Where(f => f.Geometry is LineString).ToList();
            for (var index = 0; index < lineFeatures.Count; index++)
            {
                var feature = lineFeatures[index];
                lines[feature.Id] = new Line
                {
                    Geometry = new JsonObject
                    {
                        { "type", "LineString" },
                        { "properties", PropertiesWithId(feature) },
                        { "arcs", new List<int> { index } }
                    },
                    Arc = GeoJsonExporter.WritePoints(((LineString)feature.Geometry).Points),
                    Index = index
                };
            }

            var polygons = all
                .Where(f => f.Geometry is Polygon)
                .Select(f => (object)WritePolygon(f, lines, data.Features))
                .ToList();

            var geometries = new List<object>();
            geometries.AddRange(points);
            geometries.AddRange(lineFeatures.Select(f => (object)lines[f.Id].Geometry));
            geometries.AddRange(polygons);

            var result = new JsonObject
            {
                { "type", "Topology" },
                { "objects", new JsonObject
                    {
                        { name, new JsonObject { { "type", "GeometryCollection" }, { "geometries", geometries } } }
                    }
                }
            };

            if (lines.Count > 0)
            {
                result.Add("arcs", lines.Values.OrderBy(l => l.Index).Select(l => (object)l.Arc).ToList());
            }
            return result;
        }

        private static JsonObject WritePolygon(SosiFeature feature, Dictionary<int, Line> lines, SosiFeatureCollection features)
        {
            var polygon = (Polygon)feature.Geometry;
            var arcs = new List<object> { MapArcs(polygon.ShellRefs, lines) };
            foreach (var hole in polygon.HoleRefs)
            {
                if (hole.Count == 1)
                {
                    var island = features.GetById(Math.Abs(hole[0]));
                    var islandPolygon = island != null ? island.Geometry as Polygon : null;
                    if (islandPolygon != null)
                    {
                        arcs.Add(MapArcs(islandPolygon.ShellRefs, lines));
                        continue;
                    }
                }
                arcs.Add(MapArcs(hole, lines));
            }

            return new JsonObject
            {
                { "type", "Polygon" },
                { "properties", PropertiesWithId(feature) },
                { "arcs", arcs }
            };
        }

        private static List<int> MapArcs(IEnumerable<int> refs, Dictionary<int, Line> lines)
        {
            var result = new List<int>();
            foreach (var reference in refs)
            {
                Line line;
                if (!lines.TryGetValue(Math.Abs(reference), out line))
                {
                    throw new SosiParseException("Fant ikke KURVE " + Math.Abs(reference) + " for FLATE");
                }
                // A reversed arc is referenced by its one's complement (TopoJSON spec)
                result.Add(reference > 0 ? line.Index : -(line.Index + 1));
            }
            return result;
        }

        private static Dictionary<string, object> PropertiesWithId(SosiFeature feature)
        {
            var properties = new Dictionary<string, object>();
            foreach (var attribute in feature.Attributes)
            {
                properties.Add(attribute.Key, attribute.Value);
            }
            properties["id"] = feature.Id;
            return properties;
        }
    }
}
