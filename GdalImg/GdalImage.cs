using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using GDAL.Config;
using GeoAPI.Geometries;
using OSGeo.GDAL;
using OSGeo.OSR;

namespace MngImg
{
    public delegate void StatusText(string message);

    public delegate void StatusProgressBar(int Min, int Max, int Step, int id);

    public static class ImageCalculus
    {
        private static bool _IsSizeTilePower2(int SizeTile)
        {
            double dbSizeTile = Math.Log(SizeTile, 2);
            return (dbSizeTile - Math.Floor(dbSizeTile) != 0) ? false : true;
        }

        public static int GetTotalLevelForPyramid(int SizeTile, int RasterXSize, int RasterYSize)
        {
            if (!_IsSizeTilePower2(SizeTile))
            {
                string sMsg = string.Format("{0}/{1}: SizeTile({2}) not power 2 ", "ImageCalculus", "GetTotalLevelForPyramid", SizeTile);
                throw (new Exception(sMsg));
            }

            double xLevel = Math.Log((double)(RasterXSize / SizeTile), 2), yLevel = Math.Log((double)(RasterYSize / SizeTile), 2);
            return Math.Max((int)Math.Ceiling(xLevel), (int)Math.Ceiling(yLevel));
        }
    }

    public static class ImageProcessing
    {
        public static void SetStretchMinMax(Band bd, ref byte[] pixels)
        {
            // DN = ((DN - Min) / (Max - Min)) * 255 ->
            // DN = cF * (DN - Min)
            // If Max == Min  cF = 255
            // Else           cF = 255/(Max-Min)

            double[] Statist = new double[4];

            //get min, max, mean, std dev
            bd.GetStatistics(0, 1, out Statist[0], out Statist[1], out Statist[2], out Statist[3]);
            double cF = Statist[0] == Statist[1] ? 255.0 : 255.0 / (Statist[1] - Statist[0]);

            byte LimMin = 1, LimMax = 254;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = _CalcMinMax(pixels[i], cF, Statist[0], LimMin, LimMax);
        }

        public static void SetStretchStardDevi(Band bd, ref byte[] pixels, int nSD)
        {
            // Stretch with N StandardDeviation
            //
            // Max = Mean + nSD * SD
            // Min = Mean - nSD * SD
            //
            // if DN < Min -> DNe = 0
            // if DN > Max -> DNe = 255
            // Else DNe = 0 to 255 -> F(DN, Min, Max)
            //
            // DNe = ((DN - Min) / (Max - Min)) * 255 ->
            // DNe = cF * (DN - Min)
            //
            // If Max == Min  cF = 255
            // Else           cF = 255/(Max-Min)

            double[] Statist = new double[4];

            //get min, max, mean, std dev
            bd.GetStatistics(0, 1, out Statist[0], out Statist[1], out Statist[2], out Statist[3]);

            double Min = Statist[2] - (nSD * Statist[3]), // Mean - (nSD * SD)
                   Max = Statist[2] + (nSD * Statist[3]); // Mean + (nSD * SD)

            if (Min < Statist[0]) Min = Statist[0];
            if (Max > Statist[1]) Max = Statist[1];

            double cF = Statist[0] == Statist[1] ? 255.0 : 255.0 / (Max - Min);

            byte LimMin = 0, LimMax = 255;

            for (int i = 0; i < pixels.Length; i++)
            {
                if ((double)pixels[i] < Min)
                    pixels[i] = LimMin;
                else if ((double)pixels[i] > Max)
                    pixels[i] = LimMax;
                else
                    pixels[i] = _CalcMinMax(pixels[i], cF, Min, LimMin, LimMax);
            }
        }

        public static void PopulateAlphaPixels(ref byte[] pixels, ref byte[] pixelAlpha)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                // pixels = 0 (nodata)
                if (pixels[i] != (byte)0)
                    pixelAlpha[i] = 255;
            }
        }

        static private byte _CalcMinMax(byte pixel, double cF, double Min, byte LimMin, byte LimMax)
        {
            double vPixel = cF * ((double)pixel - Min);

            // use LimMin instead of 0
            if (vPixel < LimMin) vPixel = LimMin;
            // use LimMax instead of 255
            else if (vPixel > LimMax) vPixel = LimMax;
            return (byte)Math.Round(vPixel);
        }
    }

    public class GdalImage : IImage
    {
        #region statics
        /// <summary>
        /// used to check if the file is supported by Gdal
        /// </summary>
        /// <param name="sPathFileName"></param>
        /// <returns></returns>
        public static bool IsValidImage(string sPathFileName)
        {
            return Gdal.IdentifyDriver(sPathFileName, null) == null ? false : true;
        }

        static GdalImage()
        {
            GdalConfiguration.ConfigureGdal();
        }

        #endregion

        #region private members

        private Dataset _ds;
        private string _fullName;

        //_gt[0] top left x
        //_gt[1] w-e pixel resolution
        //_gt[2] rotation, 0 if image is "north up"
        //_gt[3] top left y
        //_gt[4] rotation, 0 if image is "north up"
        //_gt[5] n-s pixel resolution (is negative)
        private double[] _gt;

        private Envelope _envelope;
        private readonly object _sync = new object();

        #endregion

        #region IImage Properties

        public string FullName
        {
            get
            {
                return _fullName;
            }
        }

        public string FileName
        {
            get
            {
                return Path.GetFileName(_fullName);
            }
        }

        /// <summary>
        /// Gets the number of bands
        /// </summary>
        public int BandsNumber { get; private set; }

        public Dataset Dataset
        {
            get
            {
                return _ds;
            }
        }

        public int Width
        {
            get
            {
                return _ds.RasterXSize;
            }
        }

        public int Height
        {
            get
            {
                return _ds.RasterYSize;
            }
        }

        public double XResolution
        {
            get
            {
                return _gt[1];
            }
        }

        public double YResolution
        {
            get
            {
                return _gt[5] * -1.0;
            }
        }

        public int NumberOverView
        {
            get
            {
                return _ds.GetRasterBand(1).GetOverviewCount();
            }
        }

        public string Format
        {
            get
            {
                return _ds.GetDriver().LongName;
            }
        }

        public string Projection { get; private set; }

        public string Type
        {
            get
            {
                return _ds.GetRasterBand(1).DataType.ToString();
            }
        }

        /// <summary>
        /// Get box of the image north[0], south[1], west[2], east[3]
        /// </summary>
        public Envelope Extent { get { return _envelope.Clone(); } }
        //public double[] Extent
        //{
        //    get
        //    {
        //        //double[] box = new double[4];
        //        //if (box.Length != 4)
        //        //{
        //        //    string sMsg = string.Format("{0}/{1}: Array of box({2}) not equal size 4", this.ToString(), "WriteBox", box.Length);
        //        //    throw (new Exception(sMsg));
        //        //}

        //        //box[0] = _gt[3]; // north
        //        ////box[1] = _gt[3] + (_gt[5] * _ds.RasterYSize); // south
        //        //box[2] = _gt[0]; // west
        //        ////box[3] = _gt[0] + (_gt[1] * _ds.RasterXSize); // east

        //        //GetGeoXY(_ds.RasterXSize, _ds.RasterYSize, out box[3], out box[1]);
        //        //return box;

        //        //return GetExtent();

        //        return new double[4] { _envelope.Top(), _envelope.Bottom(), _envelope.Left(), _envelope.Right() };
        //    }
        //}

        /// <summary>
        /// Gets or sets the bit depth of the raster file
        /// </summary>
        public int BitDepth { get; set; }

        public Color TransparentColor { get; set; }

        public Color NoDataInitColor { get; set; }

        public ColorBlend ColorBlend { get; set; }

        public bool ColorCorrect { get; set; }

        /// <summary>
        /// Gets or sets to display clip
        /// </summary>
        public bool ShowClip { get; set; }

        /// <summary>
        /// Gets or sets to display IR Band
        /// </summary>
        public bool DisplayIR { get; set; }

        /// <summary>
        /// Gets or sets to display color InfraRed
        /// </summary>
        public bool DisplayCIR { get; set; }

        #endregion IImage Properties

        #region private methods

        private void UpdateDataset(Dataset nds)
        {
            lock (_sync)
            {
                _gt = null;
                Projection = "";
                if (_ds != null)
                    // Free original dataset
                    _ds.Dispose();
                _ds = nds;
                if (_ds != null)
                {
                    _gt = new double[6];
                    _ds.GetGeoTransform(_gt);
                    // have gdal read the projection
                    Projection = _ds.GetProjectionRef();
                    // no projection info found in the image...check for a prj
                    if (Projection == "" && File.Exists(Path.ChangeExtension(_fullName, ".prj")))
                    {
                        Projection = File.ReadAllText(Path.ChangeExtension(_fullName, ".prj"));
                    }
                    BandsNumber = _ds.RasterCount;
                    _envelope = GetExtent();
                }
            }
        }

        private bool IsValid
        {
            get { return _ds != null && _gt != null; }
        }

        // get boundary of raster
        private Envelope GetExtent()
        {
            if (_ds != null)
            {
                var geoTrans = new double[6];
                _ds.GetGeoTransform(geoTrans);

                // no rotation...use default transform
                if (DoublesAreEqual(geoTrans[0], 0) && DoublesAreEqual(geoTrans[3], 0))
                    geoTrans = new[] { 999.5, 1, 0, 1000.5, 0, -1 };

                var geoTransform = new GeoTransform(geoTrans);

                // image pixels
                var dblW = (double)Width;
                var dblH = (double)Height;

                var left = geoTransform.EnvelopeLeft(dblW, dblH);
                var right = geoTransform.EnvelopeRight(dblW, dblH);
                var top = geoTransform.EnvelopeTop(dblW, dblH);
                var bottom = geoTransform.EnvelopeBottom(dblW, dblH);

                return new Envelope(left, right, bottom, top);
            }

            return null;
        }

        /// <summary>
        /// Method to get a scalar by which to scale raster values
        /// </summary>
        /// <returns>A scale value dependant on <see cref="BitDepth"/></returns>
        private double GetBitScalar()
        {
            switch (BitDepth)
            {
                case 12:
                    return 16.0;

                case 16:
                    return 256.0;

                case 32:
                    return 16777216.0;
            }
            return 1;
        }

        protected unsafe void WritePixel(double x, double[] intVal, int iPixelSize, int[] ch, byte* row)
        {
            // write out pixels
            // black and white
            Int32 offsetX = (int)Math.Round(x) * iPixelSize;
            if (BandsNumber == 1 && BitDepth != 32)
            {
                if (ch[0] < 4)
                {
                    if (ShowClip)
                    {
                        if (DoublesAreEqual(intVal[0], 0))
                        {
                            row[offsetX++] = 255;
                            row[offsetX++] = 0;
                            row[offsetX] = 0;
                        }
                        else if (DoublesAreEqual(intVal[0], 255))
                        {
                            row[offsetX++] = 0;
                            row[offsetX++] = 0;
                            row[offsetX] = 255;
                        }
                        else
                        {
                            row[offsetX++] = (byte)intVal[0];
                            row[offsetX++] = (byte)intVal[0];
                            row[offsetX] = (byte)intVal[0];
                        }
                    }
                    else
                    {
                        row[offsetX++] = (byte)intVal[0];
                        row[offsetX++] = (byte)intVal[0];
                        row[offsetX] = (byte)intVal[0];
                    }
                }
                else
                {
                    row[offsetX++] = (byte)intVal[0];
                    row[offsetX++] = (byte)intVal[1];
                    row[offsetX] = (byte)intVal[2];
                }
            }
            // IR grayscale
            else if (DisplayIR && BandsNumber == 4)
            {
                for (int i = 0; i < BandsNumber; i++)
                {
                    if (ch[i] == 3)
                    {
                        if (ShowClip)
                        {
                            if (DoublesAreEqual(intVal[3], 0))
                            {
                                row[(int)Math.Round(x) * iPixelSize] = 255;
                                row[(int)Math.Round(x) * iPixelSize + 1] = 0;
                                row[(int)Math.Round(x) * iPixelSize + 2] = 0;
                            }
                            else if (DoublesAreEqual(intVal[3], 255))
                            {
                                row[(int)Math.Round(x) * iPixelSize] = 0;
                                row[(int)Math.Round(x) * iPixelSize + 1] = 0;
                                row[(int)Math.Round(x) * iPixelSize + 2] = 255;
                            }
                            else
                            {
                                row[(int)Math.Round(x) * iPixelSize] = (byte)intVal[i];
                                row[(int)Math.Round(x) * iPixelSize + 1] = (byte)intVal[i];
                                row[(int)Math.Round(x) * iPixelSize + 2] = (byte)intVal[i];
                            }
                        }
                        else
                        {
                            row[(int)Math.Round(x) * iPixelSize] = (byte)intVal[i];
                            row[(int)Math.Round(x) * iPixelSize + 1] = (byte)intVal[i];
                            row[(int)Math.Round(x) * iPixelSize + 2] = (byte)intVal[i];
                        }
                    }
                }
            }
            // CIR
            else if (DisplayCIR && BandsNumber == 4)
            {
                if (ShowClip)
                {
                    if (DoublesAreEqual(intVal[0], 0) && DoublesAreEqual(intVal[1], 0) && DoublesAreEqual(intVal[3], 0))
                    {
                        intVal[3] = intVal[0] = 0;
                        intVal[1] = 255;
                    }
                    else if (DoublesAreEqual(intVal[0], 255) && DoublesAreEqual(intVal[1], 255) && DoublesAreEqual(intVal[3], 255))
                        intVal[1] = intVal[0] = 0;
                }

                for (int i = 0; i < BandsNumber; i++)
                {
                    if (ch[i] != 0 && ch[i] != -1)
                        row[(int)Math.Round(x) * iPixelSize + ch[i] - 1] = (byte)intVal[i];
                }
            }
            // RGB
            else
            {
                if (ShowClip)
                {
                    if (DoublesAreEqual(intVal[0], 0) && DoublesAreEqual(intVal[1], 0) && DoublesAreEqual(intVal[2], 0))
                    {
                        intVal[0] = intVal[1] = 0;
                        intVal[2] = 255;
                    }
                    else if (DoublesAreEqual(intVal[0], 255) && DoublesAreEqual(intVal[1], 255) && DoublesAreEqual(intVal[2], 255))
                        intVal[1] = intVal[2] = 0;
                }

                for (int i = 0; i < 3; i++)
                {
                    if (ch[i] != 3 && ch[i] != -1)
                        row[(int)Math.Round(x) * iPixelSize + ch[i]] = (byte)intVal[i];
                }
            }
        }

        private bool DoublesAreEqual(double val1, double val2)
        {
            return Math.Abs(val1 - val2) < double.Epsilon;
        }

        #endregion private members and methods

        #region constructor & destructor

        public GdalImage(string sPathFileName)
        {
            _fullName = sPathFileName;
            UpdateDataset(Gdal.Open(sPathFileName, Access.GA_Update));

            BitDepth = 8;
            TransparentColor = Color.Empty;
            NoDataInitColor = Color.Magenta;
            ColorCorrect = true;
        }

        ~GdalImage()
        {
            UpdateDataset(null);
        }

        #endregion

        #region IImage Members

        /// <summary>
        /// Use well known GeogCS such as : "EPSG:4326"
        /// </summary>
        /// <param name="sWellKnownGeogCS"></param>
        public void Warp(string sWellKnownGeogCS)
        {
            if (_ds == null)
            {
                string sMsg = string.Format("{0}/{1}: Not initialize Dataset", this.ToString(), "Warp");
                throw (new Exception(sMsg));
            }

            string dst_wkt;
            SpatialReference oSR = new SpatialReference("");
            oSR.SetWellKnownGeogCS(sWellKnownGeogCS);
            oSR.ExportToWkt(out dst_wkt);

            string src_wkt = _ds.GetProjection();

            // The VRT driver is a format driver for GDAL that allows a virtual GDAL dataset to be composed from other GDAL datasets with repositioning, and algorithms potentially applied as well as various kinds of metadata altered or added. VRT descriptions of datasets can be saved in an XML format normally given the extension .vrt.

            // 'NearestNeighbour', 'Bilinear', 'Cubic', or 'CubicSpline'
            Dataset dsWarp = Gdal.AutoCreateWarpedVRT(_ds, src_wkt, dst_wkt, ResampleAlg.GRA_NearestNeighbour, 0);
            UpdateDataset(dsWarp);
        }

        /// <summary>
        /// Get the params of overview [GeoOriginalX, GeoOriginalY, ResX, ResY]
        /// </summary>
        /// <param name="idOverview"></param>
        /// <param name="xoff"></param>
        /// <param name="yoff"></param>
        /// <returns></returns>
        public double[] GetOverviewParams(int idOverview, int xoff, int yoff)
        {
            double gt1 = _gt[1], gt5 = _gt[5];
            if (idOverview != -1)
            {
                gt1 *= Math.Pow(2, (double)(idOverview + 1));
                gt5 *= Math.Pow(2, (double)(idOverview + 1));
            }

            return new double[4]
            {
                _gt[0] + xoff * gt1, // GeoOriginalX
                _gt[3] + yoff * gt5, // GeoOriginalY
                gt1, // ResX
                -1 * gt5 // ResY
            };
        }

        //public void GetGeoXY(int xPixel, int yLine, out double geoX, out double geoY)
        //{
        //    if (IsValid)
        //    {
        //        // ignore rotation
        //        geoX = _gt[0] + xPixel * _gt[1] /*+ yLine * _gt[2]*/;
        //        geoY = _gt[3] + /*xPixel * _gt[4] +*/ yLine * _gt[5];
        //    }
        //    else
        //    {
        //        geoX = 0d;
        //        geoY = 0d;
        //    }
        //}

        //public void GetPixelXY(double geoX, double geoY, out int xPixel, out int yLine)
        //{
        //    if (IsValid)
        //    {
        //        xPixel = (int)Math.Round((geoX - _gt[0]) / _gt[1]);
        //        yLine = (int)Math.Round((geoY - _gt[3]) / _gt[5]);
        //    }
        //    else
        //    {
        //        xPixel = 0;
        //        yLine = 0;
        //    }
        //}

        public bool IsSameCS(string sWellKnownGeogCS)
        {
            if (!IsValid) return false;

            SpatialReference srDS = new SpatialReference(_ds.GetProjectionRef());

            SpatialReference sr = new SpatialReference("");
            sr.SetWellKnownGeogCS(sWellKnownGeogCS);

            return srDS.IsSame(sr) == 1 ? true : false;
        }

        public Bitmap GetBitmap(Size size, int[] Order, int SD)
        {
            /*
             Original source from:
             * http://www.codeplex.com/SharpMap/WorkItem/AttachmentDownload.ashx?WorkItemId=8873&FileAttachmentId=3384
             * Namefile: GdalRasterLayer.cs
             * date download 10/04/2008

             Modify original source
             * Test only case in :
             * " else // Assume Palette Interpretation Name is RGB" - last conditional
             * in this case had hard changes.
             */
            int DsWidth = _ds.RasterXSize;
            int DsHeight = _ds.RasterYSize;

            Bitmap bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb);
            int iPixelSize = 3; //Format24bppRgb = byte[b,g,r]
            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, size.Width, size.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
            try
            {
                unsafe
                {
                    for (int idBand = 1; idBand <= (_ds.RasterCount > iPixelSize ? iPixelSize : _ds.RasterCount); ++idBand)
                    {
                        byte[] buffer = new byte[size.Width * size.Height];

                        Band band = _ds.GetRasterBand(Order == null ? idBand : Order[idBand - 1]);

                        //band.ReadRaster(x1, y1, x1width, y1height, buffer, size.Width, size.Height, (int)GT.HorizontalPixelResolution, (int)GT.VerticalPixelResolution);
                        band.ReadRaster(0, 0, _ds.RasterXSize, _ds.RasterYSize, buffer, size.Width, size.Height, 0, 0);
                        if (SD != 0)
                            ImageProcessing.SetStretchStardDevi(band, ref buffer, SD);

                        int p_indx = 0;
                        ColorInterp ci = band.GetRasterColorInterpretation();

                        //int ch = 0;
                        //if (ci == ColorInterp.GCI_BlueBand) ch = 0;
                        //if (ci == ColorInterp.GCI_GreenBand) ch = 1;
                        //if (ci == ColorInterp.GCI_RedBand) ch = 2;

                        if (ci == ColorInterp.GCI_GrayIndex) //8bit Grayscale
                        {
                            for (int y = 0; y < size.Height; y++)
                            {
                                byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
                                for (int x = 0; x < size.Width; x++, p_indx++)
                                {
                                    row[x * iPixelSize] = buffer[p_indx];
                                    row[x * iPixelSize + 1] = buffer[p_indx];
                                    row[x * iPixelSize + 2] = buffer[p_indx];
                                }
                            }
                        }
                        else if (ci == ColorInterp.GCI_PaletteIndex)
                        {
                            // If raster type is Palette then the raster is an offset into the image's colour table
                            // Palettes can be of type: Gray, RGB, CMYK (Cyan/Magenta/Yellow/Black) and HLS (Hue/Light/Saturation)
                            // This code deals with only the Gray and RGB types
                            // For Grayscale the colour table contains: c1-Gray value
                            // For RGB the colour table contains: c1-Red c2-Green c3-Blue c4-Alpha
                            ColorTable table = band.GetRasterColorTable();
                            if (Gdal.GetPaletteInterpretationName(table.GetPaletteInterpretation()) == "Gray")
                            {
                                // Palette Grayscale
                                for (int y = 0; y < size.Height; y++)
                                {
                                    byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
                                    for (int x = 0; x < size.Width; x++, p_indx++)
                                    {
                                        row[x * iPixelSize] = (byte)table.GetColorEntry(buffer[p_indx]).c1;
                                        row[x * iPixelSize + 1] = (byte)table.GetColorEntry(buffer[p_indx]).c1;
                                        row[x * iPixelSize + 2] = (byte)table.GetColorEntry(buffer[p_indx]).c1;
                                    }
                                }
                            }
                            else // Assume Palette Interpretation Name is RGB
                            {
                                for (int y = 0; y < size.Height; y++)
                                {
                                    byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
                                    for (int x = 0; x < size.Width; x++, p_indx++)
                                    {
                                        row[x * iPixelSize] = (byte)table.GetColorEntry(buffer[p_indx]).c3;
                                        row[x * iPixelSize + 1] = (byte)table.GetColorEntry(buffer[p_indx]).c2;
                                        row[x * iPixelSize + 2] = (byte)table.GetColorEntry(buffer[p_indx]).c1;
                                    }
                                }
                            }
                        }
                        else  //Normal RGB
                        {
                            // Make changes for ShapMap(original)
                            for (int y = 0; y < size.Height; y++)
                            {
                                byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);

                                for (int x = 0; x < size.Width; x++, p_indx++)
                                {
                                    row[x * iPixelSize + (iPixelSize - idBand)] = buffer[p_indx];
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }

        public Bitmap GetNonRotatedPreview(Size size, Envelope bbox)
        {
            if (!IsValid) return null;

            var geoTrans = (double[])_gt.Clone();

            // default transform
            if (DoublesAreEqual(geoTrans[0], 0) && DoublesAreEqual(geoTrans[3], 0))
                geoTrans = new[] { 999.5, 1, 0, 1000.5, 0, -1 };
            Bitmap bitmap = null;
            var geoTransform = new GeoTransform(geoTrans);
            BitmapData bitmapData = null;
            var intVal = new double[BandsNumber];

            const int iPixelSize = 3; //Format24bppRgb = byte[b,g,r]

            //check if image is in bounding box
            if ((bbox.MinX > _envelope.MaxX) || (bbox.MaxX < _envelope.MinX) || (bbox.MaxY < _envelope.MinY) || (bbox.MinY > _envelope.MaxY))
                return null;

            double left = Math.Max(bbox.MinX, _envelope.MinX);
            double top = Math.Min(bbox.MaxY, _envelope.MaxY);
            double right = Math.Min(bbox.MaxX, _envelope.MaxX);
            double bottom = Math.Max(bbox.MinY, _envelope.MinY);

            double x1 = Math.Abs(geoTransform.PixelX(left));
            double y1 = Math.Abs(geoTransform.PixelY(top));
            double imgPixWidth = geoTransform.PixelXwidth(right - left);
            double imgPixHeight = geoTransform.PixelYwidth(bottom - top);

            //get screen pixels image should fill
            double dblBBoxW = bbox.Width;
            double dblBBoxtoImgPixX = imgPixWidth / dblBBoxW;
            double dblImginMapW = size.Width * dblBBoxtoImgPixX * geoTransform.HorizontalPixelResolution;

            double dblBBoxH = bbox.Height;
            double dblBBoxtoImgPixY = imgPixHeight / dblBBoxH;
            double dblImginMapH = size.Height * dblBBoxtoImgPixY * -geoTransform.VerticalPixelResolution;

            if ((DoublesAreEqual(dblImginMapH, 0)) || (DoublesAreEqual(dblImginMapW, 0)))
                return null;

            //// ratios of bounding box to image ground space
            //double dblBBoxtoImgX = size.Width / dblBBoxW;
            //double dblBBoxtoImgY = size.Height / dblBBoxH;

            //double dblLocX = 0, dblLocY = 0;

            //// set where to display bitmap in Map
            //if (!DoublesAreEqual(bbox.MinX, left))
            //{
            //    if (!DoublesAreEqual(bbox.MaxX, right))
            //        dblLocX = (_envelope.MinX - bbox.MinX) * dblBBoxtoImgX;
            //    else
            //        dblLocX = size.Width - dblImginMapW;
            //}
            //if (!DoublesAreEqual(bbox.MaxY, top))
            //{
            //    if (!DoublesAreEqual(bbox.MinY, bottom))
            //        dblLocY = (bbox.MaxY - _envelope.MaxY) * dblBBoxtoImgY;
            //    else
            //        dblLocY = size.Height - dblImginMapH;
            //}

            double bitScalar = GetBitScalar();

            try
            {
                bitmap = new Bitmap((int)Math.Round(dblImginMapW), (int)Math.Round(dblImginMapH), PixelFormat.Format24bppRgb);
                bitmapData = bitmap.LockBits(new Rectangle(0, 0, (int)Math.Round(dblImginMapW), (int)Math.Round(dblImginMapH)), ImageLockMode.ReadWrite, bitmap.PixelFormat);

                byte cr = NoDataInitColor.R;
                byte cg = NoDataInitColor.G;
                byte cb = NoDataInitColor.B;

                var noDataValues = new double[BandsNumber];
                var scales = new double[BandsNumber];

                ColorTable colorTable = null;
                unsafe
                {
                    var buffer = new double[BandsNumber][];
                    var band = new Band[BandsNumber];
                    var ch = new int[BandsNumber];
                    // get data from image
                    for (int i = 0; i < BandsNumber; i++)
                    {
                        buffer[i] = new double[(int)Math.Round(dblImginMapW) * (int)Math.Round(dblImginMapH)];
                        band[i] = _ds.GetRasterBand(i + 1);

                        //get nodata value if present
                        Int32 hasVal;
                        band[i].GetNoDataValue(out noDataValues[i], out hasVal);
                        if (hasVal == 0) noDataValues[i] = Double.NaN;
                        band[i].GetScale(out scales[i], out hasVal);
                        if (hasVal == 0) scales[i] = 1.0;

                        band[i].ReadRaster((int)Math.Round(x1), (int)Math.Round(y1), (int)Math.Round(imgPixWidth),
                                           (int)Math.Round(imgPixHeight),
                                           buffer[i], (int)Math.Round(dblImginMapW), (int)Math.Round(dblImginMapH),
                                           0, 0);

                        switch (band[i].GetRasterColorInterpretation())
                        {
                            case ColorInterp.GCI_BlueBand:
                                ch[i] = 0;
                                break;

                            case ColorInterp.GCI_GreenBand:
                                ch[i] = 1;
                                break;

                            case ColorInterp.GCI_RedBand:
                                ch[i] = 2;
                                break;

                            case ColorInterp.GCI_Undefined:
                                if (BandsNumber > 1)
                                    ch[i] = 3; // infrared
                                else
                                {
                                    ch[i] = 4;
                                    if (ColorBlend == null)
                                    {
                                        Double dblMin, dblMax;
                                        band[i].GetMinimum(out dblMin, out hasVal);
                                        if (hasVal == 0) dblMin = Double.NaN;
                                        band[i].GetMaximum(out dblMax, out hasVal);
                                        if (hasVal == 0) dblMax = double.NaN;
                                        if (Double.IsNaN(dblMin) || Double.IsNaN(dblMax))
                                        {
                                            double dblMean, dblStdDev;
                                            band[i].GetStatistics(0, 1, out dblMin, out dblMax, out dblMean, out dblStdDev);
                                            //double dblRange = dblMax - dblMin;
                                            //dblMin -= 0.1*dblRange;
                                            //dblMax += 0.1*dblRange;
                                        }
                                        var minmax = new float[] { Convert.ToSingle(dblMin), 0.5f * Convert.ToSingle(dblMin + dblMax), Convert.ToSingle(dblMax) };
                                        var colors = new Color[] { Color.Blue, Color.Yellow, Color.Red };
                                        ColorBlend = new ColorBlend(colors, minmax);
                                    }
                                    intVal = new Double[3];
                                }
                                break;

                            case ColorInterp.GCI_GrayIndex:
                                ch[i] = 0;
                                break;

                            case ColorInterp.GCI_PaletteIndex:
                                colorTable = band[i].GetRasterColorTable();
                                ch[i] = 5;
                                intVal = new Double[3];
                                break;

                            default:
                                ch[i] = -1;
                                break;
                        }
                    }

                    if (BitDepth == 32)
                        ch = new[] { 0, 1, 2 };

                    int pIndx = 0;
                    for (int y = 0; y < Math.Round(dblImginMapH); y++)
                    {
                        byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);
                        for (int x = 0; x < Math.Round(dblImginMapW); x++, pIndx++)
                        {
                            for (int i = 0; i < BandsNumber; i++)
                            {
                                intVal[i] = buffer[i][pIndx] / bitScalar;
                                Double imageVal = intVal[i] = intVal[i] / bitScalar;
                                if (ch[i] == 4)
                                {
                                    if (!DoublesAreEqual(imageVal, noDataValues[i]))
                                    {
                                        Color color = ColorBlend.GetColor(Convert.ToSingle(imageVal));
                                        intVal[0] = color.B;
                                        intVal[1] = color.G;
                                        intVal[2] = color.R;
                                        //intVal[3] = ce.c4;
                                    }
                                    else
                                    {
                                        intVal[0] = cb;
                                        intVal[1] = cg;
                                        intVal[2] = cr;
                                    }
                                }
                                else if (ch[i] == 5 && colorTable != null)
                                {
                                    if (!DoublesAreEqual(imageVal, noDataValues[i]))
                                    {
                                        ColorEntry ce = colorTable.GetColorEntry(Convert.ToInt32(imageVal));
                                        intVal[0] = ce.c3;
                                        intVal[1] = ce.c2;
                                        intVal[2] = ce.c1;
                                        //intVal[3] = ce.c4;
                                    }
                                    else
                                    {
                                        intVal[0] = cb;
                                        intVal[1] = cg;
                                        intVal[2] = cr;
                                    }
                                }
                                else
                                {
                                    if (ColorCorrect)
                                    {
                                        // TODO
                                        // intVal[i] = ApplyColorCorrection(intVal[i], 0, ch[i], 0, 0);
                                    }
                                }

                                if (intVal[i] > 255)
                                    intVal[i] = 255;
                            }

                            WritePixel(x, intVal, iPixelSize, ch, row);
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (bitmapData != null)
                    bitmap.UnlockBits(bitmapData);
            }

            if (TransparentColor != Color.Empty)
                bitmap.MakeTransparent(TransparentColor);
            //g.DrawImage(bitmap, new Point((int)Math.Round(dblLocX), (int)Math.Round(dblLocY)));
            return bitmap;
        }

        #endregion IImage Members
    }


}