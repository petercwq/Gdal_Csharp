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

    public class ImageWriteGdal : IImageWrite
    {
        private struct Options
        {
            public int[] OrderBands;
            public bool HaveOrderBands;

            public int xoff, yoff, xsize, ysize;
            public bool HaveSubset;

            public int NumStretchStardDesv;
            public bool HaveStretchStardDesv;

            public int ValueNullData;
            public bool HaveNullData;

            public int NumOverview;
            public bool HaveOverview;

            public byte ValueAlphaBand;
            public bool HaveAlphaBand;

            public void Init()
            {
                HaveOrderBands = HaveSubset = HaveStretchStardDesv =
                HaveNullData = HaveOverview = HaveAlphaBand = false;
            }

            public bool IsOriginal()
            {
                return
                    !HaveOrderBands && !HaveSubset && !HaveStretchStardDesv &&
                    !HaveNullData && !HaveOverview && !HaveAlphaBand;
            }
        };

        #region Private Members

        private Dataset _ds;
        private Options _opt;

        #endregion Private Members

        #region Public Methods

        public ImageWriteGdal(Dataset ds)
        {
            _ds = ds;
            _opt.Init();
        }

        #endregion Public Methods

        #region IImageWrite Members

        #region IImageWrite Members Options

        public void SetOptionOrder(int[] Order)
        {
            if (Order.Length > _ds.RasterCount)
            {
                string sMsg = string.Format(
                    "{0}/{1}: Number bands in 'order' params ({2}) is greater than number bands of image ({3})",
                    this.ToString(), "SetOptionOrder", Order.Length, _ds.RasterCount);

                throw (new Exception(sMsg));
            }

            _opt.HaveOrderBands = true;
            _opt.OrderBands = new int[Order.Length];
            Order.CopyTo(_opt.OrderBands, 0);
        }

        public void SetOptionSubset(int xoff, int yoff, int xsize, int ysize)
        {
            _opt.HaveSubset = true;
            _opt.xoff = xoff;
            _opt.yoff = yoff;
            _opt.xsize = xsize;
            _opt.ysize = ysize;
        }

        public void SetOptionStretchStardDesv(int nSD)
        {
            _opt.HaveStretchStardDesv = true;
            _opt.NumStretchStardDesv = nSD;
        }

        public void SetOptionNullData(int Value)
        {
            _opt.HaveNullData = true;
            _opt.ValueNullData = Value;
        }

        public void SetOptionOverview(int NumOverview)
        {
            _opt.HaveOverview = true;
            _opt.NumOverview = NumOverview;
        }

        public void SetOptionMakeAlphaBand(byte ValueAlphaBand)
        {
            _opt.HaveAlphaBand = true;
            _opt.ValueAlphaBand = ValueAlphaBand;
        }

        #endregion IImageWrite Members Options

        public void WriteFile(string sDrive, string sPathFileName)
        {
            Driver drv = Gdal.GetDriverByName(sDrive);

            Dataset dsOut;
            if (_opt.IsOriginal())
            {
                string[] aryOption = { "" };
                dsOut = drv.CreateCopy(sPathFileName, _ds, 0, aryOption, null, null);
            }
            else
            {
                dsOut = _opt.HaveSubset ? _CreateDSSubsetWrite(drv, sPathFileName)
                                        : _CreateDSWrite(drv, sPathFileName);
                _SetDSWriteFile(ref dsOut);
            }

            dsOut.FlushCache(); dsOut.Dispose();
        }

        #endregion IImageWrite Members

        #region Private Methods

        private void _SetNullDataImage(Dataset dsOut)
        {
            for (int nBand = 1; nBand < dsOut.RasterCount; nBand++)
                dsOut.GetRasterBand(nBand).SetNoDataValue(_opt.ValueNullData);
        }

        private void _PopulateAlphaPixels(ref byte[] pixels, ref byte[] pixelAlpha)
        {
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i] != _opt.ValueAlphaBand) pixelAlpha[i] = 255; // pixels = 0(nodata)
        }

        private Dataset _CreateDSWrite(Driver drv, string sPathFileName)
        {
            double[] _gt = new double[6];
            _ds.GetGeoTransform(_gt);

            int xSize = 0, ySize = 0;

            if (_opt.HaveOverview)
            {
                _ds.BuildOverviews("NEAREST", new int[] { _opt.NumOverview });

                xSize = _ds.GetRasterBand(1).GetOverview(0).XSize;
                ySize = _ds.GetRasterBand(1).GetOverview(0).YSize;

                _gt[1] *= _opt.NumOverview; _gt[5] *= _opt.NumOverview;
            }
            else
            {
                xSize = _ds.RasterXSize;
                ySize = _ds.RasterYSize;
            }

            string[] aryOption = { "" };

            int nBand = _opt.HaveAlphaBand ? _ds.RasterCount + 1 : _ds.RasterCount;

            Dataset dsOut = drv.Create(sPathFileName, xSize, ySize, nBand, _ds.GetRasterBand(1).DataType, aryOption);

            dsOut.SetProjection(_ds.GetProjection());
            dsOut.SetGeoTransform(_gt);

            return dsOut;
        }

        private Dataset _CreateDSSubsetWrite(Driver drv, string sPathFileName)
        {
            string[] aryOption = { "" };

            int nBand = _opt.HaveAlphaBand ? _ds.RasterCount + 1 : _ds.RasterCount;

            Dataset dsOut = drv.Create(sPathFileName, _opt.xsize, _opt.ysize, nBand, _ds.GetRasterBand(1).DataType, aryOption);

            double[] _gt = new double[6];
            _ds.GetGeoTransform(_gt);

            _gt[0] += _opt.xoff * _gt[1]; // X origin
            _gt[3] += _opt.yoff * _gt[5]; // Y origin

            dsOut.SetProjection(_ds.GetProjection());
            dsOut.SetGeoTransform(_gt);

            // Not Overview
            _opt.HaveOverview = false;

            return dsOut;
        }

        private void _SetDSWriteFile(ref Dataset dsOut)
        {
            if (_opt.HaveNullData)
                _SetNullDataImage(dsOut);

            // Band 8bits
            int xSize = dsOut.RasterXSize, ySize = dsOut.RasterYSize;

            byte[] pixelsAlpha = new byte[0];
            if (_opt.HaveAlphaBand)
            {
                pixelsAlpha = null;
                pixelsAlpha = new byte[xSize * ySize];
            }

            int idOrder = 0;
            for (int id = 0; id < _ds.RasterCount; id++)
            {
                Band bdOut = dsOut.GetRasterBand(id + 1);

                idOrder = _opt.HaveOrderBands ? _opt.OrderBands[id] : id + 1;

                Band bdIn = (_opt.HaveOverview)
                          ? _ds.GetRasterBand(idOrder).GetOverview(0) : _ds.GetRasterBand(idOrder);

                byte[] pixels = new byte[xSize * ySize];

                if (_opt.HaveSubset)
                    bdIn.ReadRaster(_opt.xoff, _opt.yoff, xSize, ySize, pixels, xSize, ySize, 0, 0);
                else bdIn.ReadRaster(0, 0, xSize, ySize, pixels, xSize, ySize, 0, 0);

                if (_opt.HaveStretchStardDesv)
                    ImageProcessing.SetStretchStardDevi(bdIn, ref pixels, _opt.NumStretchStardDesv);

                bdOut.WriteRaster(0, 0, xSize, ySize, pixels, xSize, ySize, 0, 0);

                if (_opt.HaveAlphaBand) _PopulateAlphaPixels(ref pixels, ref pixelsAlpha);

                bdIn.FlushCache(); bdOut.FlushCache();
                bdIn.Dispose(); bdOut.Dispose();
                id++;
                pixels = null;
            }

            if (_opt.HaveAlphaBand)
            {
                Band bdAlpha = dsOut.GetRasterBand(_ds.RasterCount + 1);
                bdAlpha.WriteRaster(0, 0, dsOut.RasterXSize, dsOut.RasterYSize, pixelsAlpha, dsOut.RasterXSize, dsOut.RasterYSize, 0, 0);
                bdAlpha.FlushCache(); bdAlpha.Dispose();
            }
        }

        #endregion Private Methods
    }

    public class ImageWriteTilesGdal
    {
        private struct Options
        {
            public int[] OrderBands;
            public bool HaveOrderBands;

            public int NumStretchStardDesv;
            public bool HaveStretchStardDesv;

            public int ValueNullData;
            public bool HaveNullData;

            public byte ValueAlphaBand;
            public bool HaveAlphaBand;

            public void Init()
            {
                HaveOrderBands = HaveStretchStardDesv = HaveNullData = HaveAlphaBand = false;
            }

            public bool IsOriginal()
            {
                return
                    !HaveOrderBands && !HaveStretchStardDesv && !HaveNullData && !HaveAlphaBand;
            }
        }

        #region Private Members

        private Dataset _ds;
        private string _path;
        private int _sizeTile;

        private Options _opt;
        private StatusText _funcStatusText;
        private StatusProgressBar _funcStatusProgressBar;

        #endregion Private Members

        #region Public Methods

        public ImageWriteTilesGdal(Dataset ds, int sizeTile, string path, StatusText funcST, StatusProgressBar funcSPB)
        {
            _ds = ds;
            _sizeTile = sizeTile;
            _path = path;

            _funcStatusText = funcST;
            _funcStatusProgressBar = funcSPB;
        }

        #region Methods Options

        public void SetOptionOrder(int[] Order)
        {
            if (Order.Length > _ds.RasterCount)
            {
                string sMsg = string.Format(
                    "{0}/{1}: Number bands in 'order' params ({2}) is greater than number bands of image ({3})",
                    this.ToString(), "SetOptionOrder", Order.Length, _ds.RasterCount);

                throw (new Exception(sMsg));
            }

            _opt.HaveOrderBands = true;
            _opt.OrderBands = new int[Order.Length];
            Order.CopyTo(_opt.OrderBands, 0);
        }

        public void SetOptionStretchStardDesv(int nSD)
        {
            _opt.HaveStretchStardDesv = true;
            _opt.NumStretchStardDesv = nSD;
        }

        public void SetOptionNullData(int Value)
        {
            _opt.HaveNullData = true;
            _opt.ValueNullData = Value;
        }

        public void SetOptionMakeAlphaBand(byte ValueAlphaBand)
        {
            _opt.HaveAlphaBand = true;
            _opt.ValueAlphaBand = ValueAlphaBand;
        }

        #endregion Methods Options

        public void Write()
        {
            int nLevel = 0;
            _SetOverviewSameLevel(ref nLevel);

            // Original: idOverview = -1 and Level is Max
            _WriteTiles(nLevel, -1);
            // int Level, int idOverview, int[] Order, int SizeTile, string sNameDirTile

            // Next Levels
            for (int i = 0; i < nLevel; i++)
                _WriteTiles(nLevel - (i + 1), i);
        }

        #endregion Public Methods

        #region Private Methods

        private void _SetOverviewSameLevel(ref int nLevel)
        {
            nLevel = ImageCalculus.GetTotalLevelForPyramid(_sizeTile, _ds.RasterXSize, _ds.RasterYSize);
            if (_ds.GetRasterBand(1).GetOverviewCount() < 1)
            {
                int[] aryOverview = new int[nLevel];
                for (int i = 0; i < nLevel; i++)
                    aryOverview[i] = (int)Math.Pow(2.0, (double)(i + 1));

                // Example:
                // Level = 3
                // aryOverview = { 2, 4, 8 }

                _ds.BuildOverviews("NEAREST", aryOverview);
            }
            else
                if (_ds.GetRasterBand(1).GetOverviewCount() != nLevel)
                {
                    string sMsg = string.Format(
                        "{0}/{1}: Number overview({2}) in image is different for Level({3})",
                        this.ToString(), "_SetOverviewSameLevel", _ds.GetRasterBand(1).GetOverviewCount(), nLevel);

                    throw (new Exception(sMsg));
                }
        }

        private void _WriteTiles(int Level, int idOverview)
        {
            int nTileX = 0, nTileY = 0, nPixelRemTileX, nPixelRemTileY;

            // nTile          -> number of tile with value
            // nPixelRemTileX -> Remainder pixel before nTile

            if (idOverview == -1) // Original
            {
                nTileX = Math.DivRem(_ds.RasterXSize, _sizeTile, out nPixelRemTileX);
                nTileY = Math.DivRem(_ds.RasterYSize, _sizeTile, out nPixelRemTileY);
            }
            else
            {
                nTileX = Math.DivRem(_ds.GetRasterBand(1).GetOverview(idOverview).XSize, _sizeTile, out nPixelRemTileX);
                nTileY = Math.DivRem(_ds.GetRasterBand(1).GetOverview(idOverview).YSize, _sizeTile, out nPixelRemTileY);
            }

            _funcStatusText(string.Format("Level {0} TileX 0->{1} TileY 0->{2} ...", Level, nTileX, nTileY));

            _funcStatusProgressBar(0, nTileX, 1, 0);
            // Pixels with full values in TileSize
            for (int idGridX = 0; idGridX < nTileX; idGridX++)
            {
                _funcStatusProgressBar(0, 0, 0, idGridX);
                for (int idGridY = 0; idGridY < nTileY; idGridY++)
                    _WriteTileEach(Level, idOverview, idGridX, idGridY, _sizeTile, _sizeTile);
            }

            // Pixels without full values in TileSize
            if (nPixelRemTileX > 0)
            {
                _funcStatusText(string.Format("Level {0} Remainder TileX 0->{1} ...", Level, nTileY));
                _funcStatusProgressBar(0, nTileY, 1, 0);
                for (int idGridY = 0; idGridY < nTileY; idGridY++)
                {
                    _funcStatusProgressBar(0, 0, 0, idGridY);
                    _WriteTileEach(Level, idOverview, nTileX, idGridY, nPixelRemTileX, _sizeTile);
                }
            }

            if (nPixelRemTileY > 0)
            {
                _funcStatusText(string.Format("Level {0} Remainder TileY 0->{1} ...", Level, nTileX));
                _funcStatusProgressBar(0, nTileX, 1, 0);
                for (int idGridX = 0; idGridX < nTileX; idGridX++)
                {
                    _funcStatusProgressBar(0, 0, 0, idGridX);
                    _WriteTileEach(Level, idOverview, idGridX, nTileY, _sizeTile, nPixelRemTileY);
                }
            }

            if (nPixelRemTileX > 0 && nPixelRemTileY > 0)
            {
                _funcStatusText("Remainder TileX & TileY ...");
                _WriteTileEach(Level, idOverview, nTileX, nTileY, nPixelRemTileX, nPixelRemTileY);
            }
        }

        private void _WriteTileEach(int Level, int idOverview, int idGridX, int idGridY, int nPixelX, int nPixelY)
        {
            int xoff = idGridX * _sizeTile;
            int yoff = idGridY * _sizeTile;

            Dataset dsOut = _CreateDSTilePNG(idOverview, xoff, yoff);

            _WriteTileEachDS(idOverview, ref dsOut, xoff, yoff, nPixelX, nPixelY);

            if (_opt.HaveNullData)
                _SetNullDataImage(dsOut);

            // Verificar como obter o caracter de diretorio! -- ****
            // Tiles: sPath\$[level]_$[y]_$[x].png
            string sPathNameTile = string.Format("{0}\\{1}_{2}_{3}.png", _path, Level, idGridX, idGridY);
            _DatasetPng2File(dsOut, sPathNameTile);

            dsOut.FlushCache(); dsOut.Dispose();
        }

        private Dataset _CreateDSTilePNG(int idOverview, int xoff, int yoff)
        {
            string[] aryOption = { "" };
            OSGeo.GDAL.Driver drv = Gdal.GetDriverByName("MEM");

            int nBand = _opt.HaveAlphaBand ? _ds.RasterCount + 1 : _ds.RasterCount;
            Dataset dsReturn = drv.Create
               ("filememory", _sizeTile, _sizeTile, nBand, _ds.GetRasterBand(1).DataType, aryOption);

            dsReturn.SetProjection(_ds.GetProjection());

            double[] _gt = new double[6];
            _ds.GetGeoTransform(_gt);

            if (idOverview != -1)
            {
                _gt[1] *= Math.Pow(2, (double)(idOverview + 1));
                _gt[5] *= Math.Pow(2, (double)(idOverview + 1));
            }

            _gt[0] += xoff * _gt[1]; // X origin
            _gt[3] += yoff * _gt[5]; // Y origin
            dsReturn.SetGeoTransform(_gt);

            return dsReturn;
        }

        private void _WriteTileEachDS(int idOverview, ref Dataset dsOut, int xoff, int yoff, int xsize, int ysize)
        {
            byte[] pixelsTile = new byte[_sizeTile * _sizeTile];

            byte[] pixelsAlpha = new byte[0];
            if (_opt.HaveAlphaBand)
            {
                pixelsAlpha = null;
                pixelsAlpha = new byte[_sizeTile * _sizeTile];
            }

            int idOrder = 0;

            for (int id = 0; id < _ds.RasterCount; id++)
            {
                Band bdOut = dsOut.GetRasterBand(id + 1);

                idOrder = _opt.HaveOrderBands ? _opt.OrderBands[id] : id + 1;

                Band bdIn = (idOverview != -1) ? _ds.GetRasterBand(idOrder).GetOverview(idOverview) : _ds.GetRasterBand(idOrder);

                if (xsize == _sizeTile)
                {
                    bdIn.ReadRaster(xoff, yoff, xsize, ysize, pixelsTile, xsize, ysize, 0, 0);
                    if (_opt.HaveStretchStardDesv)
                        ImageProcessing.SetStretchStardDevi(bdIn, ref pixelsTile, _opt.NumStretchStardDesv);
                }
                else
                {
                    byte[] pixels = new byte[xsize * ysize];
                    bdIn.ReadRaster(xoff, yoff, xsize, ysize, pixels, xsize, ysize, 0, 0);
                    if (_opt.HaveStretchStardDesv)
                        ImageProcessing.SetStretchStardDevi(bdIn, ref pixels, _opt.NumStretchStardDesv);

                    for (int iy = 0; iy < ysize; iy++)
                        System.Buffer.BlockCopy(pixels, iy * xsize, pixelsTile, iy * _sizeTile, xsize);
                }
                bdOut.WriteRaster(0, 0, _sizeTile, _sizeTile, pixelsTile, _sizeTile, _sizeTile, 0, 0);

                if (_opt.HaveAlphaBand)
                    ImageProcessing.PopulateAlphaPixels(ref pixelsTile, ref pixelsAlpha);

                bdOut.FlushCache(); bdIn.FlushCache();
                bdIn.Dispose(); bdOut.Dispose();
            }

            if (_opt.HaveAlphaBand)
            {
                Band bdAlpha = dsOut.GetRasterBand(_ds.RasterCount + 1);
                bdAlpha.WriteRaster(0, 0, _sizeTile, _sizeTile, pixelsAlpha, _sizeTile, _sizeTile, 0, 0);
                bdAlpha.FlushCache(); bdAlpha.Dispose();
            }
        }

        private void _SetNullDataImage(Dataset dsOut)
        {
            for (int nBand = 1; nBand < dsOut.RasterCount; nBand++)
                dsOut.GetRasterBand(nBand).SetNoDataValue(_opt.ValueNullData);
        }

        private void _DatasetPng2File(Dataset ds, string sFileName)
        {
            OSGeo.GDAL.Driver drv = Gdal.GetDriverByName("PNG");
            string[] aryOption = { "" };

            Dataset dsOut = drv.CreateCopy(sFileName, ds, 0, aryOption, null, null);
            dsOut.FlushCache(); dsOut.Dispose();
        }

        #endregion Private Methods
    }
}