using OSGeo.GDAL;

namespace MngImg
{
    public interface IImage
    {
        #region Methods

        void Warp(string sKnowGCS);

        void WriteBox(ref double[] box);

        double[] GetParamsOverview(int idOverview, int xoff, int yoff);

        bool IsSameCS(string sWellKnownGeogCS);

        System.Drawing.Bitmap GetBitmap(System.Drawing.Size size, int[] Order, int SD);

        #endregion Methods

        #region Properties

        string Path { get; }

        string FileName { get; }

        int XSize { get; }

        int YSize { get; }

        double XResolution { get; }

        double YResolution { get; }

        int NumberBand { get; }

        int NumberOverView { get; }

        string SpatialReference { get; }

        string Type { get; }

        Dataset Dataset { get; }

        string Format { get; }

        #endregion Properties
    }

    public interface IImageWrite
    {
        #region Methods

        void SetOptionOrder(int[] Order);

        void SetOptionSubset(int xoff, int yoff, int xsize, int ysize);

        void SetOptionStretchStardDesv(int nSD);

        void SetOptionNullData(int Value);

        void SetOptionOverview(int NumOverview);

        void SetOptionMakeAlphaBand(byte ValueAlphaBand);

        void WriteFile(string sDrive, string sPathFileName);

        #endregion Methods
    }
}