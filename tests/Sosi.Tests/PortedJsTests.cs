using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Sosi.Tests.TestHelper;

namespace Sosi.Tests
{
    /// <summary>
    /// The tests from sosi.js/test, adapted to the "lite" version (attributes keep their SOSI
    /// names and raw string values, since the lite version has no datatype lookup table).
    /// </summary>
    [TestClass]
    public class ParserTests
    {
        [TestMethod]
        public void ShouldReadASosiFile()
        {
            var data = Parse("testfile1.sos");
            Assert.IsNotNull(data.Header);
            Assert.AreEqual(5, data.Features.Count);
        }

        [TestMethod]
        public void ShouldReadHeader()
        {
            var header = Parse("testfile1.sos").Header;
            Assert.AreEqual("Statens kartverk", header.Owner);
            Assert.AreEqual("SØRKART A/S", header.Producer);
            Assert.AreEqual("Eksempel 4.5", header.ObjectCatalog);
            Assert.AreEqual("19890623", header.VerificationDate);
            Assert.AreEqual(4.5, header.Version);
            Assert.AreEqual(5.0, header.Level);
            Assert.AreEqual("NN54 SJØ0", header.VerticalDatum);
            Assert.AreEqual("11 300", header.Quality);
        }

        [TestMethod]
        public void ShouldGetBounds()
        {
            var header = Parse("testfile1.sos").Header;
            CollectionAssert.AreEqual(new[] { 10000.0, 100000, 13200, 102400 }, header.BoundingBox.ToArray());
        }

        [TestMethod]
        public void ShouldGetOrigoEnhetAndSrid()
        {
            var header = Parse("testfile1.sos").Header;
            Assert.AreEqual(10000, header.Origin.X);
            Assert.AreEqual(100000, header.Origin.Y);
            Assert.AreEqual(0.01, header.Unit);
            Assert.AreEqual("EPSG:27395", header.Srid);
            Assert.AreEqual(27395, header.EpsgCode);
        }
    }

    [TestClass]
    public class PunktTests
    {
        [TestMethod]
        public void ShouldReadIdAttributesAndGeometry()
        {
            var feature = Parse("punkttest.sos").FileOrderedFeatures[0];
            Assert.AreEqual(1, feature.Id);
            Assert.AreEqual("Fastmerke", feature.Attributes["OBJTYPE"]);
            var point = (Point)feature.Geometry;
            Assert.AreEqual(10023.45, point.X);
            Assert.AreEqual(100234.56, point.Y);
        }

        [TestMethod]
        public void ShouldWriteGeoJson()
        {
            var json = ParseJson(Parse("punkttest.sos").Export(FileTypes.GeoJson));
            Assert.AreEqual("FeatureCollection", json["type"]);
            Assert.AreEqual(1, Arr((object)json["features"]).Length);
            var feature = Obj(Arr((object)json["features"])[0]);
            Assert.AreEqual("Feature", feature["type"]);
            Assert.AreEqual(1, feature["id"]);
            Assert.AreEqual("Fastmerke", Obj(feature["properties"])["OBJTYPE"]);
            var geometry = Obj(feature["geometry"]);
            Assert.AreEqual("Point", geometry["type"]);
            CollectionAssert.AreEqual(new[] { 10023.45, 100234.56 }, Arr(geometry["coordinates"]).Select(Num).ToArray());
            Assert.AreEqual("EPSG:27395", json["crs"]["properties"]["name"]);
        }

        [TestMethod]
        public void ShouldWriteTopoJson()
        {
            var json = ParseJson(Parse("punkttest.sos").Export(FileTypes.TopoJson, "testdata"));
            Assert.AreEqual("Topology", json["type"]);
            Assert.AreEqual("GeometryCollection", json["objects"]["testdata"]["type"]);
            var geometries = Arr((object)json["objects"]["testdata"]["geometries"]);
            Assert.AreEqual(1, geometries.Length);
            var geom = Obj(geometries[0]);
            Assert.AreEqual("Point", geom["type"]);
            Assert.AreEqual(1, Obj(geom["properties"])["id"]);
            Assert.AreEqual("Fastmerke", Obj(geom["properties"])["OBJTYPE"]);
            CollectionAssert.AreEqual(new[] { 10023.45, 100234.56 }, Arr(geom["coordinates"]).Select(Num).ToArray());
            Assert.IsFalse(Obj(json).ContainsKey("arcs"));
        }
    }

    [TestClass]
    public class KurveTests
    {
        [TestMethod]
        public void ShouldReadIdAndAttributes()
        {
            var feature = Parse("kurvetest.sos").FileOrderedFeatures[0];
            Assert.AreEqual(250, feature.Id);
            Assert.AreEqual("EiendomsGrense", feature.Attributes["OBJTYPE"]);
            Assert.AreEqual("40 58", feature.Attributes["KVALITET"]);
        }

        [TestMethod]
        public void ShouldReadGeometry()
        {
            var line = (LineString)Parse("kurvetest.sos").FileOrderedFeatures[0].Geometry;
            var expected = new[]
            {
                new[] { 10023.45, 100234.56 }, new[] { 10023.45, 100234.60 }, new[] { 10023.46, 100234.70 },
                new[] { 10023.47, 100234.80 }, new[] { 10023.50, 100234.90 }, new[] { 10023.66, 100235.00 },
                new[] { 10023.45, 100235.12 }, new[] { 10023.70, 100235.65 }, new[] { 10023.56, 100234.60 },
                new[] { 10023.50, 100235.00 }
            };
            Assert.AreEqual(expected.Length, line.Points.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i][0], line.Points[i].X, "x of point " + i);
                Assert.AreEqual(expected[i][1], line.Points[i].Y, "y of point " + i);
            }
        }

        [TestMethod]
        public void ShouldReadTiepoints()
        {
            var line = (LineString)Parse("kurvetest.sos").FileOrderedFeatures[0].Geometry;
            Assert.AreEqual(1, line.Tiepoints.Count);
            Assert.AreEqual(1, line.Tiepoints[0].TiepointCode);
            Assert.AreEqual(10023.56, line.Tiepoints[0].X);
            Assert.AreEqual(100234.60, line.Tiepoints[0].Y);
        }

        [TestMethod]
        public void ShouldWriteGeoJson()
        {
            var json = ParseJson(Parse("kurvetest.sos").Export(FileTypes.GeoJson));
            var feature = Obj(Arr((object)json["features"])[0]);
            Assert.AreEqual(250, feature["id"]);
            Assert.AreEqual("40 58", Obj(feature["properties"])["KVALITET"]);
            var coordinates = Arr(Obj(feature["geometry"])["coordinates"]);
            Assert.AreEqual("LineString", Obj(feature["geometry"])["type"]);
            Assert.AreEqual(10, coordinates.Length);
            Assert.AreEqual(10023.45, Num(Arr(coordinates[0])[0]));
            Assert.AreEqual(100234.56, Num(Arr(coordinates[0])[1]));
            Assert.AreEqual(10023.50, Num(Arr(coordinates[9])[0]));
            Assert.AreEqual(100235.00, Num(Arr(coordinates[9])[1]));
        }

        [TestMethod]
        public void ShouldWriteTopoJson()
        {
            var json = ParseJson(Parse("kurvetest.sos").Export(FileTypes.TopoJson, "testdata"));
            var geometries = Arr((object)json["objects"]["testdata"]["geometries"]);
            Assert.AreEqual(1, geometries.Length);
            var geom = Obj(geometries[0]);
            Assert.AreEqual("LineString", geom["type"]);
            Assert.AreEqual(250, Obj(geom["properties"])["id"]);
            CollectionAssert.AreEqual(new object[] { 0 }, Arr(geom["arcs"]));
            var arcs = Arr((object)json["arcs"]);
            Assert.AreEqual(1, arcs.Length);
            Assert.AreEqual(10023.45, Num(Arr(Arr(arcs[0])[0])[0]));
            Assert.AreEqual(100234.56, Num(Arr(Arr(arcs[0])[0])[1]));
            Assert.AreEqual(10023.50, Num(Arr(Arr(arcs[0])[9])[0]));
            Assert.AreEqual(100235.00, Num(Arr(Arr(arcs[0])[9])[1]));
        }
    }

    [TestClass]
    public class FlateTests
    {
        [TestMethod]
        public void ShouldReadAttributes()
        {
            var feature = Parse("flatetest.sos").Features.GetById(651);
            Assert.AreEqual("Tank", feature.Attributes["OBJTYPE"]);
            Assert.AreEqual("82", feature.Attributes["KVALITET"]);
            Assert.AreEqual("\"FKB\" \"3.4 eller eldre\"", feature.Attributes["REGISTRERINGSVERSJON"]);
            Assert.IsFalse(feature.Attributes.ContainsKey("REF"), "REF is turned into the geometry");
        }

        [TestMethod]
        public void ShouldGetCenterPoint()
        {
            var polygon = (Polygon)Parse("flatetest.sos").Features.GetById(651).Geometry;
            Assert.AreEqual(341822.16, polygon.Center.X);
            Assert.AreEqual(7661351.84, polygon.Center.Y);
        }

        [TestMethod]
        public void ShouldReadGeometry()
        {
            var polygon = (Polygon)Parse("flatetest.sos").Features.GetById(651).Geometry;
            var expected = new[]
            {
                new[] { 341824.03, 7661347.45 }, new[] { 341817.18, 7661352.50 }, new[] { 341817.16, 7661352.49 },
                new[] { 341817.23, 7661353.33 }, new[] { 341820.91, 7661356.85 }, new[] { 341826.38, 7661351.01 },
                new[] { 341826.90, 7661350.95 }, new[] { 341826.78, 7661350.28 }, new[] { 341824.03, 7661347.45 }
            };
            Assert.AreEqual(9, polygon.Shell.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i][0], polygon.Shell[i].X, "x of point " + i);
                Assert.AreEqual(expected[i][1], polygon.Shell[i].Y, "y of point " + i);
            }
            Assert.AreEqual(368.15, polygon.Shell[0].Z);
        }

        [TestMethod]
        public void ShouldWriteGeoJson()
        {
            var json = ParseJson(Parse("flatetest.sos").Export(FileTypes.GeoJson));
            var features = Arr((object)json["features"]);
            Assert.AreEqual(5, features.Length);
            var feature = Obj(features[4]);
            Assert.AreEqual(651, feature["id"]);
            Assert.AreEqual("Tank", Obj(feature["properties"])["OBJTYPE"]);
            Assert.AreEqual("20030702", Obj(feature["properties"])["DATAFANGSTDATO"]);
            var geometry = Obj(feature["geometry"]);
            Assert.AreEqual("Polygon", geometry["type"]);
            var shell = Arr(Arr(geometry["coordinates"])[0]);
            Assert.AreEqual(9, shell.Length);
            CollectionAssert.AreEqual(new[] { 341824.03, 7661347.45, 368.15 }, Arr(shell[0]).Select(Num).ToArray());
            Assert.AreEqual(341824.03, Num(Arr(shell[8])[0]));
            Assert.AreEqual(7661347.45, Num(Arr(shell[8])[1]));
        }

        [TestMethod]
        public void ShouldWriteTopoJson()
        {
            var json = ParseJson(Parse("flatetest.sos").Export(FileTypes.TopoJson, "testdata"));
            var geometries = Arr((object)json["objects"]["testdata"]["geometries"]).Select(Obj).ToList();
            Assert.AreEqual(5, geometries.Count);
            var polygon = geometries.Single(g => (int)Obj(g["properties"])["id"] == 651);
            Assert.AreEqual("Polygon", polygon["type"]);
            Assert.AreEqual("Tank", Obj(polygon["properties"])["OBJTYPE"]);
            Assert.AreEqual(1, Arr(polygon["arcs"]).Length);
            Assert.AreEqual(4, Arr(Arr(polygon["arcs"])[0]).Length);
            Assert.AreEqual(4, Arr((object)json["arcs"]).Length);
        }
    }

    [TestClass]
    public class ArcTests
    {
        [TestMethod]
        public void ShouldReadBuer()
        {
            var bue26 = Parse("buer.sos").Features.GetById(26);
            Assert.IsInstanceOfType(bue26.Geometry, typeof(LineStringFromArc));
            Assert.AreEqual(56, ((LineString)bue26.Geometry).Points.Count);
        }

        [TestMethod]
        public void BuerHaveJoints()
        {
            var line = (LineString)Parse("buer.sos").Features.GetById(26).Geometry;
            Assert.AreEqual(2, line.Tiepoints.Count);
            Assert.AreEqual(474237.85, line.Tiepoints[1].X);
        }

        [TestMethod]
        public void ShouldExport()
        {
            var data = Parse("buer.sos");
            Assert.IsNotNull(ParseJson(data.Export(FileTypes.GeoJson)));
            Assert.IsNotNull(ParseJson(data.Export(FileTypes.TopoJson, "testdata")));
        }
    }

    [TestClass]
    public class IssueTests
    {
        [TestMethod]
        public void Issue2_ShouldReadAllFeaturesAndExport()
        {
            var data = Parse("testfile2.sos");
            Assert.IsTrue(data.Features.All(f => f.Geometry != null));
            Assert.AreEqual("*", data.Features.GetById(5).Attributes["KVALITET"]);
            Assert.IsNotNull(data.Features.GetById(606));
            Assert.IsNotNull(ParseJson(data.Export(FileTypes.TopoJson, "testdata")));
            Assert.IsNotNull(ParseJson(data.Export(FileTypes.GeoJson)));
        }

        [TestMethod]
        public void Issue4_ShouldReadTransparThatIsNotShorthand()
        {
            var header = Parse("testfile_issue4.sos").Header;
            Assert.AreEqual("EPSG:32632", header.Srid);
            Assert.AreEqual(0, header.Origin.X);
            Assert.AreEqual(0, header.Origin.Y);
            Assert.AreEqual(0.01, header.Unit);
        }

        [TestMethod]
        public void Issue4_ShouldGetRepeatedValuesAsList()
        {
            var kurve = Parse("testfile_issue4.sos").Features.GetById(119);
            CollectionAssert.AreEqual(
                new[] { "4002", "4003", "4011", "4005", "4019" },
                ((IReadOnlyList<string>)kurve.Attributes["LTEMA"]).ToArray());
        }

        [TestMethod]
        public void NonLinearOrder_KurveDefinedAfterFlate()
        {
            var polygon = (Polygon)Parse("non-linear.sos").Features.GetById(500).Geometry;
            AssertRing(polygon.Shell, 300010, 7000010, 300010, 7000020, 300020, 7000020, 300020, 7000010, 300010, 7000010);
        }

        [TestMethod]
        public void PunktCoordinate_NoehAndCoordinatesOnSameLine()
        {
            var data = Parse("punktcoordinate.sos");
            var point = (Point)data.FileOrderedFeatures[0].Geometry;
            Assert.AreEqual(10116.68, point.X);
            Assert.AreEqual(100029.8, point.Y);

            var line = (LineString)data.FileOrderedFeatures[1].Geometry;
            Assert.AreEqual(9, line.Points.Count);
            Assert.AreEqual(10116.68, line.Points[0].X);
            Assert.AreEqual(100029.8, line.Points[0].Y);
            Assert.AreEqual(9.95, line.Points[0].Z);
            Assert.AreEqual(10116.75, line.Points[8].X);
            Assert.AreEqual(100029.87, line.Points[8].Y);
            Assert.AreEqual(10.02, line.Points[8].Z);
        }

        [TestMethod]
        public void Grouped_ShouldReadFastmerke()
        {
            var fastmerke = Parse("fastmerke.sos").Features.GetById(1);
            Assert.AreEqual("TB", fastmerke.Attributes["FMSREF"]);
            Assert.AreEqual("1 * 1 *", fastmerke.Attributes["FMTYPE"]);
            Assert.AreEqual("0.020", fastmerke.Attributes["HOB"]);
            StringAssert.EndsWith((string)fastmerke.Attributes["PUNKTBESKR"], "BIL.\"");
            Assert.AreEqual(0.001, Parse("fastmerke.sos").Header.Unit);
            Assert.AreEqual(969.988, ((Point)fastmerke.Geometry).Z);
        }

        [TestMethod]
        public void RealLife_ShouldReadNaturvernomraade()
        {
            var data = Parse("naturvernomraade.sos");
            Assert.AreEqual("Direktoratet for naturforvaltning", data.Header.Owner);
            Assert.IsNotNull(data.Features.GetById(50));
        }

        internal static void AssertRing(IReadOnlyList<Point> ring, params double[] xy)
        {
            Assert.AreEqual(xy.Length / 2, ring.Count);
            for (var i = 0; i < ring.Count; i++)
            {
                Assert.AreEqual(xy[2 * i], ring[i].X, "x of point " + i);
                Assert.AreEqual(xy[2 * i + 1], ring[i].Y, "y of point " + i);
            }
        }
    }

    [TestClass]
    public class IslandTests
    {
        [TestMethod]
        public void ShouldReadOuterRing()
        {
            var polygon = (Polygon)Parse("flate_oy.sos").Features.GetById(400).Geometry;
            IssueTests.AssertRing(polygon.Shell, 300000, 7000000, 300000, 7001000, 301000, 7001000, 301000, 7000000, 300000, 7000000);
        }

        [TestMethod]
        public void ShouldReadIslandDescribedAsAnotherFlate()
        {
            var polygon = (Polygon)Parse("flate_oy.sos").Features.GetById(400).Geometry;
            Assert.AreEqual(1, polygon.Holes.Count);
            IssueTests.AssertRing(polygon.Holes[0], 300010, 7000010, 300010, 7000020, 300020, 7000020, 300020, 7000010, 300010, 7000010);
        }

        [TestMethod]
        public void ShouldReadIslandDescribedByKurve()
        {
            var polygon = (Polygon)Parse("flate_oy.sos").Features.GetById(600).Geometry;
            Assert.AreEqual(5, polygon.Shell.Count);
            Assert.AreEqual(1, polygon.Holes.Count);
            Assert.AreEqual(5, polygon.Holes[0].Count);
        }

        [TestMethod]
        public void ShouldWriteGeoJson()
        {
            var json = ParseJson(Parse("flate_oy.sos").Export(FileTypes.GeoJson));
            var features = Arr((object)json["features"]);
            Assert.AreEqual(11, features.Length);
            var feature = Obj(features[10]);
            Assert.AreEqual(600, feature["id"]);
            Assert.AreEqual("Mahogney", Obj(feature["properties"])["OBJTYPE"]);
            var coordinates = Arr(Obj(feature["geometry"])["coordinates"]);
            Assert.AreEqual(2, coordinates.Length);
            Assert.AreEqual(5, Arr(coordinates[0]).Length);
            Assert.AreEqual(5, Arr(coordinates[1]).Length);
            Assert.AreEqual(300000, Num(Arr(Arr(coordinates[0])[0])[0]));
            Assert.AreEqual(300010, Num(Arr(Arr(coordinates[1])[0])[0]));
        }

        [TestMethod]
        public void ShouldWriteTopoJson()
        {
            var json = ParseJson(Parse("flate_oy.sos").Export(FileTypes.TopoJson, "testdata"));
            var geometries = Arr((object)json["objects"]["testdata"]["geometries"]).Select(Obj).ToList();
            Assert.AreEqual(11, geometries.Count);

            foreach (var id in new[] { 400, 600 })
            {
                var polygon = geometries.Single(g => (int)Obj(g["properties"])["id"] == id);
                Assert.AreEqual("Polygon", polygon["type"]);
                var arcs = Arr(polygon["arcs"]);
                Assert.AreEqual(2, arcs.Length);
                CollectionAssert.AreEqual(new object[] { 0, 1, 2, 3 }, Arr(arcs[0]));
                CollectionAssert.AreEqual(new object[] { 4, 5, 6, 7 }, Arr(arcs[1]));
            }
            Assert.AreEqual(600, Obj(geometries[10]["properties"])["id"]);

            var expectedArcs = new[]
            {
                new[] { 300000, 7000000, 300000, 7001000 }, new[] { 300000, 7001000, 301000, 7001000 },
                new[] { 301000, 7001000, 301000, 7000000 }, new[] { 301000, 7000000, 300000, 7000000 },
                new[] { 300010, 7000010, 300010, 7000020 }, new[] { 300010, 7000020, 300020, 7000020 },
                new[] { 300020, 7000020, 300020, 7000010 }, new[] { 300020, 7000010, 300010, 7000010 }
            };
            var jsonArcs = Arr((object)json["arcs"]);
            Assert.AreEqual(8, jsonArcs.Length);
            for (var i = 0; i < expectedArcs.Length; i++)
            {
                var actual = Arr(jsonArcs[i]).SelectMany(p => Arr(p).Select(Num)).ToArray();
                CollectionAssert.AreEqual(expectedArcs[i].Select(v => (double)v).ToArray(), actual, "arc " + i);
            }
        }
    }
}
