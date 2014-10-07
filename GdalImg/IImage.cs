using System.Drawing;
using GeoAPI.Geometries;
using OSGeo.GDAL;

namespace MngImg
{
    public interface IImage
    {
        void Warp(string sWellKnownGeogCS);

        double[] GetOverviewParams(int idOverview, int xoff, int yoff);

        //void GetGeoXY(int xPixel, int yLine, out double geoX, out double geoY);

        //void GetPixelXY(double geoX, double geoY, out int xPixel, out int yLine);

        bool IsSameCS(string sWellKnownGeogCS);

        Bitmap GetBitmap(Size size, int[] Order, int SD);

        string FullName { get; }

        string FileName { get; }

        int Width { get; }

        int Height { get; }

        double XResolution { get; }

        double YResolution { get; }

        int BandsNumber { get; }

        int NumberOverView { get; }

        string Projection { get; }

        string Type { get; }

        Dataset Dataset { get; }

        string Format { get; }

        Envelope Extent { get; }
    }
}