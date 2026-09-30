using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sosi.Internal;
using static Sosi.Tests.TestHelper;

namespace Sosi.Tests
{
    /// <summary>Tests of the .NET specific API and of deviations from sosi.js.</summary>
    [TestClass]
    public class DotNetApiTests
    {
        private const string Minimal =
@".HODE
..TEGNSETT UTF-8
..TRANSPAR
...KOORDSYS 22
...ORIGO-NØ 0 0
...ENHET 0.01
.PUNKT 20:
..OBJTYPE Test
..NAVN ""Hei! Hallå"" ! a real comment
..NØ 100 200
.PUNKT 10:
..OBJTYPE Test
..NØ
300 400
.SLUTT
";

        [TestMethod]
        public void FeaturesAreOrderedByIdAndFileOrderIsAvailable()
        {
            var data = Parse("flatetest.sos");
            CollectionAssert.AreEqual(new[] { 134, 135, 138, 633, 651 }, data.Features.Select(f => f.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 633, 134, 138, 135, 651 }, data.FileOrderedFeatures.Select(f => f.Id).ToArray());
            Assert.AreSame(data.Features.GetById(633), data.FileOrderedFeatures[0]);
            Assert.AreSame(data.Features.GetById(651), data.Features.FirstOrDefault(f => f.Id == 651));
            Assert.IsNull(data.Features.GetById(4711));
        }

        [TestMethod]
        public void ParsesFromString()
        {
            var data = new SosiParser().Parse(Minimal);
            Assert.AreEqual("EPSG:32632", data.Header.Srid);
            Assert.IsNull(data.Header.BoundingBox);
            Assert.AreEqual(2, data.Features.Count);
            Assert.AreEqual(10, data.Features[0].Id);
            Assert.AreEqual(20, data.FileOrderedFeatures[0].Id);

            var point = (Point)data.Features.GetById(20).Geometry;
            Assert.AreEqual(2, point.X);
            Assert.AreEqual(1, point.Y);
        }

        [TestMethod]
        public void ExclamationMarkInsideQuotesIsNotAComment()
        {
            var data = new SosiParser().Parse(Minimal);
            Assert.AreEqual("\"Hei! Hallå\"", data.Features.GetById(20).Attributes["NAVN"]);
        }

        [TestMethod]
        public void GeoJsonOutputIsExactlyLikeSosiJs()
        {
            var geojson = new SosiParser().Parse(Minimal).Export(FileTypes.GeoJson);
            Assert.AreEqual(
                "{\"type\":\"FeatureCollection\",\"features\":[" +
                "{\"type\":\"Feature\",\"id\":10,\"properties\":{\"OBJTYPE\":\"Test\"},\"geometry\":{\"type\":\"Point\",\"coordinates\":[4,3]}}," +
                "{\"type\":\"Feature\",\"id\":20,\"properties\":{\"OBJTYPE\":\"Test\",\"NAVN\":\"\\\"Hei! Hallå\\\"\"},\"geometry\":{\"type\":\"Point\",\"coordinates\":[2,1]}}]," +
                "\"crs\":{\"type\":\"name\",\"properties\":{\"name\":\"EPSG:32632\"}}}",
                geojson);
        }

        [TestMethod]
        public void TopoJsonUsesDefaultObjectName()
        {
            var json = ParseJson(new SosiParser().Parse(Minimal).Export(FileTypes.TopoJson));
            Assert.IsTrue(Obj((object)json["objects"]).ContainsKey("features"));
        }

        [TestMethod]
        public void MissingHeaderThrows()
        {
            Assert.ThrowsException<SosiParseException>(() => new SosiParser().Parse(".PUNKT 1:\n..NØ\n1 1\n"));
        }

        [TestMethod]
        public void UnknownKoordsysThrows()
        {
            var text = Minimal.Replace("KOORDSYS 22", "KOORDSYS 4711");
            Assert.ThrowsException<SosiParseException>(() => new SosiParser().Parse(text));
        }

        [TestMethod]
        public void AllSampleFilesCanBeExported()
        {
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(DataPath("x")), "*.sos"))
            {
                var data = new SosiParser().ParseFile(file);
                Assert.IsTrue(data.Features.Count > 0, file);
                Assert.IsNotNull(ParseJson(data.Export(FileTypes.GeoJson)), file);
                Assert.IsNotNull(ParseJson(data.Export(FileTypes.TopoJson, "x", indented: true)), file);
            }
        }

        [TestMethod]
        public void DecodesLatin1UsingTegnsett()
        {
            var text = Minimal.Replace("UTF-8", "ISO8859-1");
            var bytes = Encoding.GetEncoding(28591).GetBytes(text);
            var data = new SosiParser().Parse(SosiParser.Decode(bytes));
            Assert.AreEqual("\"Hei! Hallå\"", data.Features.GetById(20).Attributes["NAVN"]);
        }

        [TestMethod]
        public void DecodesNd7()
        {
            var bytes = Encoding.ASCII.GetBytes("..TEGNSETT ND7\n..NAVN \"Bj|rn \\stre ]ker\"\nÿ");
            bytes[bytes.Length - 1] = 0xFF; // make it invalid UTF-8
            StringAssert.Contains(SosiParser.Decode(bytes), "\"Bjørn Østre Åker\"");
        }

        [TestMethod]
        public void NumbersAreFormattedLikeJavaScript()
        {
            Assert.AreEqual("10023.45", JsonWriter.FormatNumber(10023.45));
            Assert.AreEqual("-3", JsonWriter.FormatNumber(-3.0));
            Assert.AreEqual("0", JsonWriter.FormatNumber(-0.0));
            Assert.AreEqual("1e-7", JsonWriter.FormatNumber(1e-7));
            Assert.AreEqual("null", JsonWriter.FormatNumber(double.NaN));
        }

        [TestMethod]
        public void JsRoundingIsReproduced()
        {
            Assert.AreEqual(3, SosiText.Round(2.5, 0));
            Assert.AreEqual(-2, SosiText.Round(-2.5, 0));
            Assert.AreEqual(0, SosiText.Round(0.49999999999999994, 0));
            Assert.AreEqual(double.NaN, SosiText.ParseInt("abc"));
            Assert.AreEqual(12, SosiText.ParseInt("12.9"));
            Assert.AreEqual(0.01, SosiText.ParseFloat("0.010xyz"));
        }

        [TestMethod]
        public void AttributeValueTypes()
        {
            var data = Parse("buer.sos");
            var kurve = data.Features.GetById(6);
            var kopidata = (IReadOnlyDictionary<string, string>)kurve.Attributes["KOPIDATA"];
            Assert.AreEqual("0618", kopidata["OMRÅDEID"]);
            Assert.AreEqual("\"Hemsedal kommune\"", kopidata["ORIGINALDATAVERT"]);
            Assert.AreEqual("RpGrense", kurve.GetAttributeString("OBJTYPE"));
        }
    }
}
