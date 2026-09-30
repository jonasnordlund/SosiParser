using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace Sosi.Tests
{
    internal static class TestHelper
    {
        public static string DataPath(string fileName) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", fileName);

        public static SosiData Parse(string fileName) => new SosiParser().ParseFile(DataPath(fileName));

        /// <summary>Parses JSON into Dictionary&lt;string, object&gt; / object[] / numbers / strings.</summary>
        public static dynamic ParseJson(string json)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return serializer.DeserializeObject(json);
        }

        public static double Num(object value) => Convert.ToDouble(value);

        public static IDictionary<string, object> Obj(object value) => (IDictionary<string, object>)value;

        public static object[] Arr(object value) => (object[])value;
    }
}
