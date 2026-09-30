using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Sosi
{
    /// <summary>A coordinate reference system known to the parser.</summary>
    public sealed class CoordinateSystemInfo
    {
        internal CoordinateSystemInfo(string srid, string proj4)
        {
            Srid = srid;
            Proj4 = proj4;
        }

        /// <summary>The SRID, e.g. "EPSG:25832".</summary>
        public string Srid { get; }

        /// <summary>A proj4 definition of the coordinate system.</summary>
        public string Proj4 { get; }
    }

    /// <summary>Mapping from the SOSI-specific KOORDSYS and GEOSYS codes to SRIDs.</summary>
    public static class CoordinateSystems
    {
        /// <summary>Known GEOSYS codes (first value of <c>...GEOSYS</c>).</summary>
        public static IReadOnlyDictionary<int, CoordinateSystemInfo> Geosys { get; } =
            new ReadOnlyDictionary<int, CoordinateSystemInfo>(new Dictionary<int, CoordinateSystemInfo>
            {
                { 2, new CoordinateSystemInfo("EPSG:4326", "+proj=longlat +ellps=WGS84 +datum=WGS84 +no_defs ") }
            });

        /// <summary>Known KOORDSYS codes.</summary>
        public static IReadOnlyDictionary<int, CoordinateSystemInfo> Koordsys { get; } =
            new ReadOnlyDictionary<int, CoordinateSystemInfo>(new Dictionary<int, CoordinateSystemInfo>
            {
                { 1, new CoordinateSystemInfo("EPSG:27391", "+proj=tmerc +lat_0=58 +lon_0=-4.666666666666667 +k=1 +x_0=0 +y_0=0 +a=6377492.018 +b=6356173.508712696 +towgs84=278.3,93,474.5,7.889,0.05,-6.61,6.21 +pm=oslo +units=m +no_defs") },
                { 2, new CoordinateSystemInfo("EPSG:27392", "+proj=tmerc +lat_0=58 +lon_0=-2.333333333333333 +k=1 +x_0=0 +y_0=0 +a=6377492.018 +b=6356173.508712696 +towgs84=278.3,93,474.5,7.889,0.05,-6.61,6.21 +pm=oslo +units=m +no_defs") },
                { 3, new CoordinateSystemInfo("EPSG:27393", "+proj=tmerc +lat_0=58 +lon_0=0 +k=1 +x_0=0 +y_0=0 +a=6377492.018 +b=6356173.508712696 +towgs84=278.3,93,474.5,7.889,0.05,-6.61,6.21 +pm=oslo +units=m +no_defs") },
                { 4, new CoordinateSystemInfo("EPSG:27394", "+proj=tmerc +lat_0=58 +lon_0=2.5 +k=1 +x_0=0 +y_0=0 +a=6377492.018 +b=6356173.508712696 +towgs84=278.3,93,474.5,7.889,0.05,-6.61,6.21 +pm=oslo +units=m +no_defs") },
                { 5, new CoordinateSystemInfo("EPSG:27395", "+proj=tmerc +lat_0=58 +lon_0=6.166666666666667 +k=1 +x_0=0 +y_0=0 +a=6377492.018 +b=6356173.508712696 +towgs84=278.3,93,474.5,7.889,0.05,-6.61,6.21 +pm=oslo +units=m +no_defs") },
                { 6, new CoordinateSystemInfo("EPSG:27396", "+proj=tmerc +lat_0=58 +lon_0=10.16666666666667 +k=1 +x_0=0 +y_0=0 +a=6377492.018 +b=6356173.508712696 +towgs84=278.3,93,474.5,7.889,0.05,-6.61,6.21 +pm=oslo +units=m +no_defs") },
                { 7, new CoordinateSystemInfo("EPSG:27397", "+proj=tmerc +lat_0=58 +lon_0=14.16666666666667 +k=1 +x_0=0 +y_0=0 +a=6377492.018 +b=6356173.508712696 +towgs84=278.3,93,474.5,7.889,0.05,-6.61,6.21 +pm=oslo +units=m +no_defs") },
                { 8, new CoordinateSystemInfo("EPSG:27398", "+proj=tmerc +lat_0=58 +lon_0=18.33333333333333 +k=1 +x_0=0 +y_0=0 +a=6377492.018 +b=6356173.508712696 +towgs84=278.3,93,474.5,7.889,0.05,-6.61,6.21 +pm=oslo +units=m +no_defs") },
                { 9, new CoordinateSystemInfo("EPSG:4273", "+proj=longlat +a=6377492.018 +b=6356173.508712696 +towgs84=278.3,93,474.5,7.889,0.05,-6.61,6.21 +no_defs") },
                { 21, new CoordinateSystemInfo("EPSG:32631", "+proj=utm +zone=31 +ellps=WGS84 +datum=WGS84 +units=m +no_defs") },
                { 22, new CoordinateSystemInfo("EPSG:32632", "+proj=utm +zone=32 +ellps=WGS84 +datum=WGS84 +units=m +no_defs") },
                { 23, new CoordinateSystemInfo("EPSG:32633", "+proj=utm +zone=33 +ellps=WGS84 +datum=WGS84 +units=m +no_defs") },
                { 24, new CoordinateSystemInfo("EPSG:32634", "+proj=utm +zone=34 +ellps=WGS84 +datum=WGS84 +units=m +no_defs") },
                { 25, new CoordinateSystemInfo("EPSG:32635", "+proj=utm +zone=35 +ellps=WGS84 +datum=WGS84 +units=m +no_defs") },
                { 26, new CoordinateSystemInfo("EPSG:32636", "+proj=utm +zone=36 +ellps=WGS84 +datum=WGS84 +units=m +no_defs") },
                { 31, new CoordinateSystemInfo("EPSG:23031", "+proj=utm +zone=31 +ellps=intl +units=m +no_defs") },
                { 32, new CoordinateSystemInfo("EPSG:23032", "+proj=utm +zone=32 +ellps=intl +units=m +no_defs") },
                { 33, new CoordinateSystemInfo("EPSG:23033", "+proj=utm +zone=33 +ellps=intl +units=m +no_defs") },
                { 34, new CoordinateSystemInfo("EPSG:23034", "+proj=utm +zone=34 +ellps=intl +units=m +no_defs") },
                { 35, new CoordinateSystemInfo("EPSG:23035", "+proj=utm +zone=35 +ellps=intl +units=m +no_defs") },
                { 36, new CoordinateSystemInfo("EPSG:23036", "+proj=utm +zone=36 +ellps=intl +units=m +no_defs") },
                { 50, new CoordinateSystemInfo("EPSG:4230", "+proj=longlat +ellps=intl +no_defs") },
                { 72, new CoordinateSystemInfo("EPSG:4322", "+proj=longlat +ellps=WGS72 +no_defs ") },
                { 84, new CoordinateSystemInfo("EPSG:4326", "+proj=longlat +ellps=WGS84 +datum=WGS84 +no_defs ") },
                { 87, new CoordinateSystemInfo("EPSG:4231", "+proj=longlat +ellps=intl +no_defs ") }
            });
    }
}
