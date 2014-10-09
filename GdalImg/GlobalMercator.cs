using System;
using System.Collections.Generic;
using System.Text;

namespace MngImg
{
    /// <summary>
    /// Reference to a Tile X, Y index
    /// </summary>
    public struct TileAddress
    {
        public TileAddress(int x, int y, int zoom)
            : this()
        {
            X = x;
            Y = y;
            Zoom = zoom;
        }

        public int X { get; private set; }
        public int Y { get; private set; }
        public int Zoom { get; private set; }

        /// <summary>
        /// Converts TMS tile coordinates to Microsoft QuadTree
        /// </summary>
        /// <param name="tx"></param>
        /// <param name="ty"></param>
        /// <param name="zoom"></param>
        /// <returns></returns>
        public string ToQuadTree()
        {
            StringBuilder quadKey = new StringBuilder();
            var ty = ((1 << Zoom) - 1) - Y;
            for (var i = Zoom; i >= 1; i--)
            {
                char digit = '0';
                int mask = 1 << (i - 1);
                if ((X & mask) != 0)
                {
                    digit++;
                }
                if ((ty & mask) != 0)
                {
                    digit++;
                    digit++;
                }
                quadKey.Append(digit);
            }
            return quadKey.ToString();
        }
    }

    public struct Point
    {
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }
        public int X;
        public int Y;
    }

    public struct LatLon
    {
        public LatLon(double lat, double lon)
        {
            Latitude = lat;
            Longitude = lon;
        }

        public double Latitude;
        public double Longitude;
    }

    public struct GeoExtent
    {
        public double Left;
        public double Bottom;
        public double Right;
        public double Top;

        public override string ToString()
        {
            return string.Format("{0:F8},{1:F8},{2:F8},{3:F8}", Left, Bottom, Right, Top);
        }

        public GeoExtent(GeoPoint lu, GeoPoint rb)
        {
            Left = lu.X;
            Top = lu.Y;
            Right = rb.X;
            Bottom = rb.Y;
        }

        public GeoExtent(LatLon lu, LatLon rb)
        {
            Left = lu.Longitude;
            Top = lu.Latitude;
            Right = rb.Longitude;
            Bottom = rb.Latitude;
        }

        public GeoExtent(string values)
        {
            var vs = values.Split(',');
            Left = Convert.ToDouble(vs[0]);
            Bottom = Convert.ToDouble(vs[1]);
            Right = Convert.ToDouble(vs[2]);
            Top = Convert.ToDouble(vs[3]);
        }
    }

    public struct GeoPoint
    {
        public double X;
        public double Y;
        public double Z;

        public override string ToString()
        {
            return string.Format("{0:F8},{1:F8},{2:F8}", X, Y, Z);
        }

        public GeoPoint(double x, double y, double z = double.NaN)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public GeoPoint(string values)
        {
            var vs = values.Split(',');
            X = Convert.ToDouble(vs[0]);
            Y = Convert.ToDouble(vs[1]);
            if (vs.Length > 2)
                Z = Convert.ToDouble(vs[2]);
            else
                Z = Double.NaN;
        }
    }

    /// <summary>
    /// Conversion routines for Google, TMS, and Microsoft Quadtree tile representations, derived from
    /// http://www.maptiler.org/google-maps-coordinates-tile-bounds-projection/ 
    /// </summary>
    /// <remarks>
    /// Spherical Mercator EPSG:900913 (EPSG:3857) and WGS84 Datum: 
    /// The coordinates you use in the Google Maps API and which are presented to the users is Latitude/Longitude in WGS84 Datum (when directly projected by Platte Carre then it is referenced as EPSG:4326).
    /// But for map publishing in the form compatible with all the popular interactive maps and especially for ground tile overlays you need to use Mercator map projection. Interactive web maps are using "Spherical Mercator" system which uses Mercator projection on the sphere instead of WGS84 ellipsoid. It is defined as EPSG:900913 or EPSG:3857 (deprecated EPSG:3785). 
    /// 
    /// Google - described in the Google Maps API documentation, http://code.google.com/apis/maps/documentation/overlays.html#Google_Maps_Coordinates
    /// TMS an variant of Google tile addressing, which is used in open-source projects like OpenLayers or TileCache. This system is described in the OSGEO Tile Map Service (TMS) Specification, http://wiki.osgeo.org/wiki/Tile_Map_Service_Specification
    /// QuadTree - with documentation in the Virtal Earth Tile System from Microsoft, http://msdn.microsoft.com/en-us/library/bb259689.aspx
    /// </remarks>
    public class GlobalMercator
    {
        private const int TileSize = 256;
        private const int EarthRadius = 6378137;
        private const double InitialResolution = 2 * Math.PI * EarthRadius / TileSize;
        private const double OriginShift = 2 * Math.PI * EarthRadius / 2;

        //Converts given lat/lon in WGS84 Datum to XY in Spherical Mercator EPSG:900913
        public static GeoPoint LatLonToMeters(LatLon latlon)
        {
            var p = new GeoPoint();
            p.X = latlon.Longitude * OriginShift / 180;
            p.Y = Math.Log(Math.Tan((90 + latlon.Latitude) * Math.PI / 360)) / (Math.PI / 180);
            p.Y = p.Y * OriginShift / 180;
            return p;
        }

        //Converts XY point from Spherical Mercator EPSG:900913 to lat/lon in WGS84 Datum
        public static LatLon MetersToLatLon(GeoPoint m)
        {
            var lon = (m.X / OriginShift) * 180;
            var lat = (m.Y / OriginShift) * 180;
            lat = 180 / Math.PI * (2 * Math.Atan(Math.Exp(lat * Math.PI / 180)) - Math.PI / 2);
            return new LatLon(lat, lon);
        }

        //Converts pixel coordinates in given zoom level of pyramid to EPSG:900913
        public static GeoPoint PixelsToMeters(Point p, int zoom)
        {
            var res = Resolution(zoom);
            var met = new GeoPoint();
            met.X = p.X * res - OriginShift;
            met.Y = p.Y * res - OriginShift;
            return met;
        }


        //Converts EPSG:900913 to pyramid pixel coordinates in given zoom level
        public static Point MetersToPixels(GeoPoint m, int zoom)
        {
            var res = Resolution(zoom);
            var pix = new Point();
            pix.X = (int)((m.X + OriginShift) / res);
            pix.Y = (int)((m.Y + OriginShift) / res);
            return pix;
        }

        public static Point PixelsToRaster(Point p, int zoom)
        {
            var mapSize = TileSize << zoom;
            return new Point(p.X, mapSize - p.Y);
        }

        //Returns a TMS (NOT Google!) tile covering region in given pixel coordinates
        public static TileAddress PixelsToTile(Point p, int zoom)
        {
            return new TileAddress((int)Math.Ceiling(p.X / (double)TileSize) - 1, (int)Math.Ceiling(p.Y / (double)TileSize) - 1, zoom);
        }

        //Returns tile for given Mercator coordinates
        public static TileAddress MetersToTile(GeoPoint m, int zoom)
        {
            var p = MetersToPixels(m, zoom);
            return PixelsToTile(p, zoom);
        }

        public static TileAddress LatLonToTile(LatLon latlon, int zoom)
        {
            var dp = LatLonToMeters(latlon);
            return MetersToTile(dp, zoom);
        }

        // Switch to Google Tile representation from TMS
        public static TileAddress ToGoogleTile(TileAddress t)
        {
            return new TileAddress(t.X, ((int)Math.Pow(2, t.Zoom) - 1) - t.Y, t.Zoom);
        }

        // Switch to TMS Tile representation from Google
        public static TileAddress ToTmsTile(TileAddress t)
        {
            return new TileAddress(t.X, ((int)Math.Pow(2, t.Zoom) - 1) - t.Y, t.Zoom);
        }

        //Converts a quad tree to tile coordinates
        public static TileAddress QuadTreeToTile(string quadtree, int zoom)
        {
            int tx = 0, ty = 0;

            for (var i = zoom; i >= 1; i--)
            {
                var ch = quadtree[zoom - i];
                var mask = 1 << (i - 1);

                var digit = ch - '0';

                if ((digit & 1) != 0)
                    tx += mask;

                if ((digit & 2) != 0)
                    ty += mask;
            }

            ty = ((1 << zoom) - 1) - ty;

            return new TileAddress(tx, ty, zoom);
        }


        //Converts a Quadtree location into a latitude/longitude bounding rectangle
        public static GeoExtent QuadTreeToLatLon(string quadtree)
        {
            TileAddress t = QuadTreeToTile(quadtree, quadtree.Length);
            return TileLatLonBounds(t);
        }

        //Returns bounds of the given tile in EPSG:900913 coordinates
        public static GeoExtent TileBounds(TileAddress t)
        {
            var min = PixelsToMeters(new Point(t.X * TileSize, t.Y * TileSize), t.Zoom);
            var max = PixelsToMeters(new Point((t.X + 1) * TileSize, (t.Y + 1) * TileSize), t.Zoom);
            return new GeoExtent(min, max);
        }

        //Returns bounds of the given tile in latitude/longitude using WGS84 datum
        public static GeoExtent TileLatLonBounds(TileAddress t)
        {
            var bound = TileBounds(t);
            var min = MetersToLatLon(new GeoPoint(bound.Left, bound.Top));
            var max = MetersToLatLon(new GeoPoint(bound.Right, bound.Bottom));
            return new GeoExtent(min, max);
        }


        //Resolution (meters/pixel) for given zoom level (measured at Equator)
        public static double Resolution(int zoom)
        {
            return InitialResolution / (Math.Pow(2, zoom));
        }

        public static double ZoomForPixelSize(double pixelSize)
        {
            for (var i = 0; i < 30; i++)
                if (pixelSize > Resolution(i))
                    return i != 0 ? i - 1 : 0;
            throw new InvalidOperationException();
        }

        //Converts a latitude and longitude to quadtree at the specified zoom level 
        public static string LatLonToQuadTree(LatLon latLon, int zoom)
        {
            GeoPoint m = LatLonToMeters(latLon);
            TileAddress t = MetersToTile(m, zoom);
            return t.ToQuadTree();
        }

        //Returns a list of all of the quadtree locations at a given zoom level within a latitude/longitude box
        public static List<string> GetQuadTreeList(int zoom, LatLon latLonMin, LatLon latLonMax)
        {
            if (latLonMax.Latitude < latLonMin.Latitude || latLonMax.Longitude < latLonMin.Longitude)
                return null;

            GeoPoint mMin = LatLonToMeters(latLonMin);
            TileAddress tmin = MetersToTile(mMin, zoom);
            GeoPoint mMax = LatLonToMeters(latLonMax);
            TileAddress tmax = MetersToTile(mMax, zoom);

            var arr = new List<string>();

            for (var ty = tmin.Y; ty <= tmax.Y; ty++)
            {
                for (var tx = tmin.X; tx <= tmax.X; tx++)
                {
                    arr.Add(new TileAddress(tx, ty, zoom).ToQuadTree());
                }
            }
            return arr;
        }

        public static bool IsSizeTilePower2(int tileSize)
        {
            // ignore 0
            // return (tileSize & (tileSize - 1)) == 0;

            // consider 0
            return (tileSize != 0) && ((tileSize & (tileSize - 1)) == 0);
        }

        public static int GetTotalLevel(int tileSize, int rasterXSize, int rasterYSize)
        {
            double xLevel = Math.Log((double)(rasterXSize / tileSize), 2), yLevel = Math.Log((double)(rasterYSize / tileSize), 2);
            return Math.Max((int)Math.Ceiling(xLevel), (int)Math.Ceiling(yLevel));
        }
    }
}
