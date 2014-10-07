/******************************************************************************
 * $Id: ImageGdal.cs 00002 2008-04-17  luiz $
 *
 * Name:     ImageGdal.cs
 * Project:  Manager Image from GDAL
 * Purpose:  Wrapper image from GDAL.
 * Author:   Luiz Motta, luizmottanet@hotmail.com
 *
 ******************************************************************************
 * Copyright (c) 2008, Luiz Motta
 *
 * Permission is hereby granted, free of charge, to any person obtaining a
 * copy of this software and associated documentation files (the "Software"),
 * to deal in the Software without restriction, including without limitation
 * the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included
 * in all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
 * OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 *****************************************************************************/

/*
 * References (GDAL):
 * - gdal_csharp
 * - gdalconst_csharp
 * - osr_csharp
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Xml;
using OSGeo.GDAL;
using OSGeo.OSR;

namespace MngImg
{
    public delegate void StatusText(string message);

    public delegate void StatusProgressBar(int Min, int Max, int Step, int id);

    static public class ImageCalculus
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
                string sMsg = string.Format(
                    "{0}/{1}: SizeTile({2}) not power 2 ",
                    "ImageCalculus", "GetTotalLevelForPyramid", SizeTile);

                throw (new Exception(sMsg));
            }

            double xLevel = Math.Log((double)(RasterXSize / SizeTile), 2),
                   yLevel = Math.Log((double)(RasterYSize / SizeTile), 2);

            double xLevelFloor = Math.Floor(xLevel),
                   yLevelFloor = Math.Floor(yLevel);

            int xLevelInt = (int)xLevelFloor,
                yLevelInt = (int)yLevelFloor;

            if (xLevelFloor < xLevel) xLevelInt++;
            if (yLevelFloor < yLevel) yLevelInt++;

            return xLevelInt > yLevelInt ? xLevelInt : yLevelInt;
        }
    }

    static public class BandGdal
    {
        #region Static Public Methods

        static public void SetStretchMinMaxByte(Band bd, ref byte[] pixels)
        {
            // DN = ((DN - Min) / (Max - Min)) * 255
            // DN = cF * (DN - Min)
            // If Max == Min  cF = 255
            // Else           cF = 255/(Max-Min)

            double[] Statist = new double[4];

            bd.GetStatistics(0, 1, out Statist[0], out Statist[1], out Statist[2], out Statist[3]);
            // min, max, mean, stddev
            double cF = Statist[0] == Statist[1] ? 255.0 : 255.0 / (Statist[1] - Statist[0]);

            byte LimMin = 1, LimMax = 254;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = _CalcMinMax(pixels[i], cF, Statist[0], LimMin, LimMax);
        }

        static public void SetStretchStardDesvByte(Band bd, ref byte[] pixels, int nSD)
        {
            // Strech with N StandardDeviation
            //
            // Max = Mean + nSD * SD
            // Min = Mean - nSD * SD
            //
            // if DN < Min -> DNe = 0
            // if DN > Max -> DNe = 255
            // Else DNe = 0 to 255 -> F(DN, Min, Max)
            //
            // DNe = ((DN - Min) / (Max - Min)) * 255
            // DNe = cF * (DN - Min)
            //
            // If Max == Min  cF = 255
            // Else           cF = 255/(Max-Min)

            double[] Statist = new double[4];
            // GetStatistics(out double min, out double max, out double mean, out double stddev);

            bd.GetStatistics(0, 1, out Statist[0], out Statist[1], out Statist[2], out Statist[3]);
            // min, max, mean, stddev
            double Min = Statist[2] - (nSD * Statist[3]), // Mean - (nSD * SD)
                   Max = Statist[2] + (nSD * Statist[3]); // Mean + (nSD * SD)

            if (Min < Statist[0]) Min = Statist[0];
            if (Max > Statist[1]) Max = Statist[1];

            double cF = Statist[0] == Statist[1] ? 255.0 : 255.0 / (Max - Min);

            byte LimMin = 0, LimMax = 255;

            for (int i = 0; i < pixels.Length; i++)
            {
                if ((double)pixels[i] < Min) pixels[i] = LimMin;
                else
                    if ((double)pixels[i] > Max) pixels[i] = LimMax;
                    else
                        pixels[i] = _CalcMinMax(pixels[i], cF, Min, LimMin, LimMax);
            }
        }

        static public void PopulateAlphaPixels(ref byte[] pixels, ref byte[] pixelAlpha)
        {
            for (int i = 0; i < pixels.Length; i++)
                if (pixels[i] != (byte)0) pixelAlpha[i] = 255; // pixels = 0(nodata)
        }

        #endregion Static Public Methods

        #region Private Methods

        static private byte _CalcMinMax(byte pixel, double cF, double Min, byte LimMin, byte LimMax)
        {
            double vPixel = cF * ((double)pixel - Min);

            if (vPixel < 0) vPixel = LimMin;
            else if (vPixel > 255) vPixel = LimMax;

            double vReturn = Math.Round(vPixel);

            return (byte)vReturn;
        }

        #endregion Private Methods
    }

    public class ImageGdal : IImage
    {
        #region Static Methods

        public static bool IsValidImage(string sPathFileName)
        {
            return Gdal.IdentifyDriver(sPathFileName, null) == null ? false : true;
        }

        #endregion Static Methods

        #region Private Members

        private Dataset _ds;
        private string _PathFileName;

        #endregion Private Members



        #region Public Methods

        public ImageGdal(string sPathFileName)
        {
            _PathFileName = sPathFileName;

            _ds = Gdal.Open(sPathFileName, Access.GA_Update);
        }

        ~ImageGdal()
        {
            _ds.Dispose();
        }

        #endregion Public Methods

        #region IImage Members

        public void Warp(string sKnowGCS)
        {
            if (_ds == null)
            {
                string sMsg = string.Format(
                    "{0}/{1}: Not inicialize Dataset", this.ToString(), "Warp");

                throw (new Exception(sMsg));
            }

            string dst_wkt;
            SpatialReference oSR = new SpatialReference("");
            oSR.SetWellKnownGeogCS(sKnowGCS);
            oSR.ExportToWkt(out dst_wkt);

            string src_wkt = _ds.GetProjection();

            Dataset dsWarp = Gdal.AutoCreateWarpedVRT(_ds, src_wkt, dst_wkt, ResampleAlg.GRA_NearestNeighbour, 0);

            // Free m_dsIn
            _ds.Dispose();

            _ds = dsWarp;
        }

        public void WriteBox(ref double[] box)
        {
            // Array -> north[0], south[1], west[2], east[3];

            if (box.Length != 4)
            {
                string sMsg = string.Format(
                    "{0}/{1}: Array of box({2}) not equal size 4",
                    this.ToString(), "WriteBox", box.Length);

                throw (new Exception(sMsg));
            }

            double[] argTransform = new double[6];
            _ds.GetGeoTransform(argTransform);

            //argTransform[0] top left x
            //argTransform[1] w-e pixel resolution
            //argTransform[2] rotation, 0 if image is "north up"
            //argTransform[3] top left y
            //argTransform[4] rotation, 0 if image is "north up"
            //argTransform[5] n-s pixel resolution (is negative)

            box[0] = argTransform[3]; // north
            box[1] = argTransform[3] + (argTransform[5] * _ds.RasterYSize); // south
            box[2] = argTransform[0]; // west
            box[3] = argTransform[0] + (argTransform[1] * _ds.RasterXSize); // east
        }

        public double[] GetParamsOverview(int idOverview, int xoff, int yoff)
        {
            // ParamsImg = ULTieX, ULTieY, ResX, ResY
            double[] ParamsImg = new double[4];

            double[] argTransform = new double[6];
            _ds.GetGeoTransform(argTransform);

            //argTransform[0] /* top left x */
            //argTransform[1] /* w-e pixel resolution */
            //argTransform[2] /* rotation, 0 if image is "north up" */
            //argTransform[3] /* top left y */
            //argTransform[4] /* rotation, 0 if image is "north up" */
            //argTransform[5] /* n-s pixel resolution */

            if (idOverview != -1)
            {
                argTransform[1] *= Math.Pow(2, (double)(idOverview + 1));
                argTransform[5] *= Math.Pow(2, (double)(idOverview + 1));
            }

            argTransform[0] += xoff * argTransform[1]; // X origin
            argTransform[3] += yoff * argTransform[5]; // Y origin

            ParamsImg[0] = argTransform[0]; // ULTieX
            ParamsImg[1] = argTransform[3]; // ULTieY
            ParamsImg[2] = argTransform[1]; // ResX
            ParamsImg[3] = -1 * argTransform[5]; // ResY

            return ParamsImg;
        }

        public bool IsSameCS(string sWellKnownGeogCS)
        {
            SpatialReference srDS = new SpatialReference(_ds.GetProjectionRef());

            SpatialReference sr = new SpatialReference("");
            sr.SetWellKnownGeogCS(sWellKnownGeogCS);

            return srDS.IsSame(sr) == 1 ? true : false;
        }

        public Bitmap GetBitmap(System.Drawing.Size size, int[] Order, int SD)
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
                            BandGdal.SetStretchStardDesvByte(band, ref buffer, SD);

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

        #endregion IImage Members

        #region IImage Properties

        public string Path
        {
            get
            {
                System.IO.FileInfo fi = new System.IO.FileInfo(_PathFileName);
                return fi.DirectoryName;
            }
        }

        public string FileName
        {
            get
            {
                System.IO.FileInfo fi = new System.IO.FileInfo(_PathFileName);
                return fi.Name;
            }
        }

        public int XSize
        {
            get
            {
                return _ds.RasterXSize;
            }
        }

        public int YSize
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
                double[] argTransform = new double[6];
                _ds.GetGeoTransform(argTransform);

                return argTransform[1];
            }
        }

        public double YResolution
        {
            get
            {
                double[] argTransform = new double[6];
                _ds.GetGeoTransform(argTransform);

                return argTransform[5] * -1.0;
            }
        }

        public int NumberBand
        {
            get
            {
                return _ds.RasterCount;
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

        public string SpatialReference
        {
            get
            {
                string ProjectionRef = _ds.GetProjectionRef();
                return ProjectionRef.Length == 0 ? "Unknown" : ProjectionRef;
            }
        }

        public string Type
        {
            get
            {
                return _ds.GetRasterBand(1).DataType.ToString();
            }
        }

        public Dataset Dataset
        {
            get
            {
                return _ds;
            }
        }

        #endregion IImage Properties
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
            double[] argTransform = new double[6];
            _ds.GetGeoTransform(argTransform);

            int xSize = 0, ySize = 0;

            if (_opt.HaveOverview)
            {
                _ds.BuildOverviews("NEAREST", new int[] { _opt.NumOverview });

                xSize = _ds.GetRasterBand(1).GetOverview(0).XSize;
                ySize = _ds.GetRasterBand(1).GetOverview(0).YSize;

                argTransform[1] *= _opt.NumOverview; argTransform[5] *= _opt.NumOverview;
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
            dsOut.SetGeoTransform(argTransform);

            return dsOut;
        }

        private Dataset _CreateDSSubsetWrite(Driver drv, string sPathFileName)
        {
            string[] aryOption = { "" };

            int nBand = _opt.HaveAlphaBand ? _ds.RasterCount + 1 : _ds.RasterCount;

            Dataset dsOut = drv.Create(sPathFileName, _opt.xsize, _opt.ysize, nBand, _ds.GetRasterBand(1).DataType, aryOption);

            double[] argTransform = new double[6];
            _ds.GetGeoTransform(argTransform);

            argTransform[0] += _opt.xoff * argTransform[1]; // X origin
            argTransform[3] += _opt.yoff * argTransform[5]; // Y origin

            dsOut.SetProjection(_ds.GetProjection());
            dsOut.SetGeoTransform(argTransform);

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
                    BandGdal.SetStretchStardDesvByte(bdIn, ref pixels, _opt.NumStretchStardDesv);

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

            double[] argTransform = new double[6];
            _ds.GetGeoTransform(argTransform);

            if (idOverview != -1)
            {
                argTransform[1] *= Math.Pow(2, (double)(idOverview + 1));
                argTransform[5] *= Math.Pow(2, (double)(idOverview + 1));
            }

            argTransform[0] += xoff * argTransform[1]; // X origin
            argTransform[3] += yoff * argTransform[5]; // Y origin
            dsReturn.SetGeoTransform(argTransform);

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
                        BandGdal.SetStretchStardDesvByte(bdIn, ref pixelsTile, _opt.NumStretchStardDesv);
                }
                else
                {
                    byte[] pixels = new byte[xsize * ysize];
                    bdIn.ReadRaster(xoff, yoff, xsize, ysize, pixels, xsize, ysize, 0, 0);
                    if (_opt.HaveStretchStardDesv)
                        BandGdal.SetStretchStardDesvByte(bdIn, ref pixels, _opt.NumStretchStardDesv);

                    for (int iy = 0; iy < ysize; iy++)
                        System.Buffer.BlockCopy(pixels, iy * xsize, pixelsTile, iy * _sizeTile, xsize);
                }
                bdOut.WriteRaster(0, 0, _sizeTile, _sizeTile, pixelsTile, _sizeTile, _sizeTile, 0, 0);

                if (_opt.HaveAlphaBand)
                    BandGdal.PopulateAlphaPixels(ref pixelsTile, ref pixelsAlpha);

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

    public class KMLTile : IComparable
    {
        #region Private members

        private double _TieX, _TieY, _ResX, _ResY;
        private int _SizeTile, _SizeTileValidX, _SizeTileValidY;
        // Tie -> Upper left
        // m_SizeTile -> Size X/Y imagen
        // m_SizeTileValid -> Valid pixes X or Y

        private int _Level, _IdGridX, _IdGridY;

        #endregion Private members

        #region Properties

        public int Level
        {
            get { return _Level; }
        }

        public string FileName
        {
            get { return ToString() + ".png"; }
        }

        public string FileNameKML
        {
            get { return ToString() + ".kml"; }
        }

        #endregion Properties

        #region Public Methods

        public override string ToString()
        {
            return string.Format("{0}_{1}_{2}", _Level, _IdGridX, _IdGridY);
        }

        public bool IsTile(KMLTile imgLevelUp)
        {
            int iniX = 2 * imgLevelUp._IdGridX, iniY = 2 * imgLevelUp._IdGridY;

            return (_IdGridX >= iniX && _IdGridX <= (iniX + 1) &&
                     _IdGridY >= iniY && _IdGridY <= (iniY + 1))
                   ? true : false;
        }

        public void SetParamsSize(ref double[] aryParamsTieRes, int sizeTile)
        {
            // aryParamsTieRes -> m_TieX[0], m_TieY[1], m_ResX[2] & m_ResY[3]
            if (aryParamsTieRes.Length != 4)
            {
                string sMsg = string.Format(
                    "{0}/{1}: Number of params for tie ({2})is different for 4 ",
                    this.ToString(), "SetParamsSize", aryParamsTieRes.Length);

                throw (new Exception(sMsg));
            }

            _TieX = aryParamsTieRes[0]; _TieY = aryParamsTieRes[1];
            _ResX = aryParamsTieRes[2]; _ResY = aryParamsTieRes[3];

            _SizeTile = _SizeTileValidX = _SizeTileValidY = sizeTile;
        }

        public void SetParamsSize(ref double[] aryParamsTieRes, ref int[] aryParamsSize)
        {
            // aryParamsTieRes -> m_TieX[0], m_TieY[1], m_ResX[2] & m_ResY[3]
            // aryParamsSize   -> m_SizeTile[0], m_SizeTileValidX[1], m_SizeTileValidY[3]
            if (aryParamsTieRes.Length != 4)
            {
                string sMsg = string.Format(
                    "{0}/{1}: Number of params Tie for tie ({2})is different for 4",
                    this.ToString(), "SetParamsSize", aryParamsTieRes.Length);

                throw (new Exception(sMsg));
            }

            _TieX = aryParamsTieRes[0]; _TieY = aryParamsTieRes[1];
            _ResX = aryParamsTieRes[2]; _ResY = aryParamsTieRes[3];

            if (aryParamsSize.Length != 3)
            {
                string sMsg = string.Format(
                    "{0}/{1}: Number of params Size ({2})is different for 3",
                    this.ToString(), "SetParamsSize", aryParamsSize.Length);

                throw (new Exception(sMsg));
            }

            _SizeTile = aryParamsSize[0];
            _SizeTileValidX = aryParamsSize[1];
            _SizeTileValidY = aryParamsSize[2];
        }

        public void SetLevelIdGrid(int Level, int idGridX, int idGridY)
        {
            _Level = Level; _IdGridX = idGridX; _IdGridY = idGridY;
        }

        public void WriteGroundOverlayKML(ref XmlTextWriter xtr, int drawOrder)
        {
            // GroundOverlay
            xtr.WriteStartElement("GroundOverlay");
            _WriteNameKML(ref xtr);
            xtr.WriteStartElement("drawOrder"); xtr.WriteString(drawOrder.ToString()); xtr.WriteEndElement();
            xtr.WriteStartElement("Icon");
            xtr.WriteStartElement("href"); xtr.WriteString(FileName); xtr.WriteEndElement();
            xtr.WriteEndElement(); // Icon
            xtr.WriteStartElement("LatLonBox"); _WriteLatLonImgBoxKML(ref xtr); xtr.WriteEndElement();
            xtr.WriteEndElement(); // GroundOverlay
        }

        public void WriteRegionKML(ref XmlTextWriter xtr)
        {
            string minLodPixels = "128"; // ((double)(m_SizeTile / 2)).ToString().Replace(',', '.');

            xtr.WriteStartElement("Region");
            xtr.WriteStartElement("LatLonAltBox"); _WriteLatLonRegionBoxKML(ref xtr); xtr.WriteEndElement();
            xtr.WriteStartElement("Lod");
            xtr.WriteStartElement("minLodPixels"); xtr.WriteString(minLodPixels); xtr.WriteEndElement();
            xtr.WriteStartElement("maxLodPixels"); xtr.WriteString("-1"); xtr.WriteEndElement();
            xtr.WriteEndElement(); // Lod
            xtr.WriteEndElement(); // Region
        }

        public void WriteLinkThis(ref XmlTextWriter xtr)
        {
            // NetworkLink
            xtr.WriteStartElement("NetworkLink");
            _WriteNameKML(ref xtr);
            WriteRegionKML(ref xtr);
            xtr.WriteStartElement("Link");
            xtr.WriteStartElement("href"); xtr.WriteString(FileNameKML); xtr.WriteEndElement();
            xtr.WriteStartElement("viewRefreshMode"); xtr.WriteString("onRegion"); xtr.WriteEndElement();
            //  <viewFormat/> ??
            xtr.WriteEndElement(); // Link
            xtr.WriteEndElement(); // NetworkLink
        }

        #endregion Public Methods

        #region Private Methods

        private void _WriteLatLonImgBoxKML(ref XmlTextWriter xtr)
        {
            double north, south, east, west;

            north = _TieY;
            south = _TieY - (_ResY * _SizeTile);
            west = _TieX;
            east = _TieX + (_ResX * _SizeTile);

            xtr.WriteStartElement("north");
            xtr.WriteString(north.ToString().Replace(',', '.'));
            xtr.WriteEndElement();

            xtr.WriteStartElement("south");
            xtr.WriteString(south.ToString().Replace(',', '.'));
            xtr.WriteEndElement();

            xtr.WriteStartElement("east");
            xtr.WriteString(east.ToString().Replace(',', '.'));
            xtr.WriteEndElement();

            xtr.WriteStartElement("west");
            xtr.WriteString(west.ToString().Replace(',', '.'));
            xtr.WriteEndElement();
        }

        private void _WriteLatLonRegionBoxKML(ref XmlTextWriter xtr)
        {
            double north, south, east, west;

            north = _TieY;
            south = _TieY - (_ResY * _SizeTileValidY);
            west = _TieX;
            east = _TieX + (_ResX * _SizeTileValidX);

            xtr.WriteStartElement("north");
            xtr.WriteString(north.ToString().Replace(',', '.'));
            xtr.WriteEndElement();

            xtr.WriteStartElement("south");
            xtr.WriteString(south.ToString().Replace(',', '.'));
            xtr.WriteEndElement();

            xtr.WriteStartElement("east");
            xtr.WriteString(east.ToString().Replace(',', '.'));
            xtr.WriteEndElement();

            xtr.WriteStartElement("west");
            xtr.WriteString(west.ToString().Replace(',', '.'));
            xtr.WriteEndElement();
        }

        private void _WriteNameKML(ref XmlTextWriter xtr)
        {
            string sName = string.Format("{0}_{1}_{2}", _Level, _IdGridY, _IdGridX);
            xtr.WriteStartElement("name"); xtr.WriteString(sName); xtr.WriteEndElement();
        }

        #endregion Private Methods

        #region IComparable Members

        public int CompareTo(object obj)
        {
            if ((obj is KMLTile) == false) throw new ArgumentException("Object is not a ImageTileKML");

            KMLTile pC = (KMLTile)obj;

            // Crescente
            if (_Level > pC._Level) return 1;
            else if (_Level < pC.Level) return -1;
            else if (_IdGridX > pC._IdGridX) return 1;
            else if (_IdGridX < pC._IdGridX) return -1;
            else if (_IdGridY > pC._IdGridY) return 1;
            else if (_IdGridY < pC._IdGridY) return -1;

            return 0;
        }

        #endregion IComparable Members
    }

    internal class KMLWriteTilesMng
    {
        #region Private Members

        private string _path;
        private List<KMLTile> _ref_lstKMLTile;

        #endregion Private Members

        #region Public Methods

        public KMLWriteTilesMng(string path, List<KMLTile> lstKMLTile)
        {
            _path = path;
            _ref_lstKMLTile = lstKMLTile;
        }

        public void WriteMain(string nameFile, double[] boxImg)
        {
            string sNameFileKML = _path + "\\" + nameFile + ".kml";

            if (System.IO.File.Exists(sNameFileKML)) System.IO.File.Delete(sNameFileKML);

            XmlTextWriter xtr = _Create(sNameFileKML, "Tiles from " + nameFile);

            // Region
            xtr.WriteStartElement("Region");
            xtr.WriteStartElement("LatLonAltBox");
            _WriteLatLonBox(ref xtr, boxImg);
            _WriteAltitudeZero(ref xtr);
            xtr.WriteEndElement();
            xtr.WriteEndElement(); // Region

            _WriteNetworkLinkMain(ref xtr, 0, boxImg);

            _Close(ref xtr);
        }

        public void WriteTiles(int Level)
        {
            for (int i = 0; i < _ref_lstKMLTile.Count; i++) WriteTileEach(i, Level);
        }

        #endregion Public Methods

        #region Private Methods

        private XmlTextWriter _Create(string sNameFileKML, string sNameDocXML)
        {
            XmlTextWriter xtr = new XmlTextWriter(sNameFileKML, UTF8Encoding.UTF8);

            xtr.Formatting = Formatting.Indented;

            xtr.WriteStartDocument();

            // Tags: KML & Document
            xtr.WriteStartElement("kml");
            xtr.WriteAttributeString("xmlns", "http://earth.google.com/kml/2.2");
            xtr.WriteStartElement("Document");

            // Tags for Document
            xtr.WriteStartElement("Name"); xtr.WriteString(sNameDocXML); xtr.WriteEndElement();
            //xtr.WriteStartElement("open"); xtr.WriteString("1"); xtr.WriteEndElement(); ********* VERIFICAR

            return xtr;
        }

        private void _WriteLatLonBox(ref XmlTextWriter xtr, double[] boxImg)
        {
            double north, south, east, west;

            north = boxImg[0];
            south = boxImg[1];
            west = boxImg[2];
            east = boxImg[3];

            xtr.WriteStartElement("north");
            xtr.WriteString(north.ToString().Replace(',', '.'));
            xtr.WriteEndElement();

            xtr.WriteStartElement("south");
            xtr.WriteString(south.ToString().Replace(',', '.'));
            xtr.WriteEndElement();

            xtr.WriteStartElement("east");
            xtr.WriteString(east.ToString().Replace(',', '.'));
            xtr.WriteEndElement();

            xtr.WriteStartElement("west");
            xtr.WriteString(west.ToString().Replace(',', '.'));
            xtr.WriteEndElement();
        }

        private void _WriteAltitudeZero(ref XmlTextWriter xtr)
        {
            xtr.WriteStartElement("minAltitude"); xtr.WriteString("0"); xtr.WriteEndElement();
            xtr.WriteStartElement("maxAltitude"); xtr.WriteString("0"); xtr.WriteEndElement();
        }

        private void _WriteNetworkLinkMain(ref XmlTextWriter xtr, int idImgTile, double[] boxImg)
        {
            string sLinkKML = _path + "\\" + _ref_lstKMLTile[idImgTile].FileNameKML;

            // NetworkLink
            xtr.WriteStartElement("NetworkLink");

            // Open
            xtr.WriteStartElement("open"); xtr.WriteString("1"); xtr.WriteEndElement();
            // Region
            xtr.WriteStartElement("Region");
            xtr.WriteStartElement("LatLonAltBox");
            _WriteLatLonBox(ref xtr, boxImg);
            _WriteAltitudeZero(ref xtr); xtr.WriteEndElement();
            _WriteLodDefault(ref xtr);
            xtr.WriteEndElement(); // Region

            // Link
            xtr.WriteStartElement("Link");
            xtr.WriteStartElement("href"); xtr.WriteString(sLinkKML); xtr.WriteEndElement();
            xtr.WriteStartElement("viewRefreshMode"); xtr.WriteString("onRegion"); xtr.WriteEndElement();

            //  <viewFormat/> ??

            xtr.WriteEndElement(); // Link

            xtr.WriteEndElement(); // NetworkLink
        }

        private void _Close(ref XmlTextWriter xtr)
        {
            xtr.WriteEndElement(); // Document
            xtr.WriteEndElement(); // KML
            xtr.WriteEndDocument();
            xtr.Flush();
            xtr.Close();
        }

        private void _WriteLodDefault(ref XmlTextWriter xtr)
        {
            string minLodPixels = "128";//((double)(m_SizeTile / 2)).ToString().Replace(',', '.');

            xtr.WriteStartElement("Lod");
            xtr.WriteStartElement("minLodPixels"); xtr.WriteString(minLodPixels); xtr.WriteEndElement();
            xtr.WriteStartElement("maxLodPixels"); xtr.WriteString("-1"); xtr.WriteEndElement();
            xtr.WriteStartElement("minFadeExtent"); xtr.WriteString("0"); xtr.WriteEndElement();
            xtr.WriteStartElement("maxFadeExtent"); xtr.WriteString("0"); xtr.WriteEndElement();
            xtr.WriteEndElement();
        }

        private void WriteTileEach(int idImgTile, int nLevel)
        {
            string sNameFileKML = _path + "\\" + _ref_lstKMLTile[idImgTile].FileNameKML;

            if (System.IO.File.Exists(sNameFileKML)) System.IO.File.Delete(sNameFileKML);

            XmlTextWriter xtr = _Create(sNameFileKML, _ref_lstKMLTile[idImgTile].FileNameKML);

            _ref_lstKMLTile[idImgTile].WriteRegionKML(ref xtr);
            _ref_lstKMLTile[idImgTile].WriteGroundOverlayKML(ref xtr, _ref_lstKMLTile[idImgTile].Level);

            // Link

            int iniLevel = _ref_lstKMLTile[idImgTile].Level;

            if (iniLevel == nLevel) // Don´t have link
            {
                _Close(ref xtr);
                return;
            }

            // Go to next Level
            int idNextLevel = idImgTile + 1;
            for (; idNextLevel < _ref_lstKMLTile.Count; idNextLevel++)
                if (iniLevel != _ref_lstKMLTile[idNextLevel].Level) break;

            if (_ref_lstKMLTile[idNextLevel].Level != (iniLevel + 1))
            {
                string sMsg = string.Format(
                    "{0}/{1}: Not found next Level number {2}",
                    this.ToString(), "_WriteTileKML", iniLevel.ToString());

                throw (new Exception(sMsg));
            }

            // Write Link
            int countLink = 0;
            for (int i = idNextLevel; i < _ref_lstKMLTile.Count; i++)
            {
                if (countLink == 4) break;
                if (_ref_lstKMLTile[i].Level != (iniLevel + 1)) break;

                if (_ref_lstKMLTile[i].IsTile(_ref_lstKMLTile[idImgTile]))
                {
                    _ref_lstKMLTile[i].WriteLinkThis(ref xtr);
                    countLink++;
                }
            }

            _Close(ref xtr);
        }

        #endregion Private Methods
    }

    public class KMLWriteTilesGdal
    {
        #region Private Members

        private Dataset _ds;
        private string _path;
        private string _nameFile;
        private int _sizeTile;

        private List<KMLTile> _lstKMLTile = new List<KMLTile>();

        #endregion Private Members

        #region Public Methods

        public KMLWriteTilesGdal(Dataset ds, int sizeTile, string path, string nameFile)
        {
            _ds = ds;
            _sizeTile = sizeTile;
            _path = path;
            _nameFile = nameFile;
        }

        public void Write()
        {
            int Level = ImageCalculus.GetTotalLevelForPyramid(_sizeTile, _ds.RasterXSize, _ds.RasterYSize);
            _SetLstKMLTile(Level);

            KMLWriteTilesMng kmlMng = new KMLWriteTilesMng(_path, _lstKMLTile);

            double[] boxImg = new double[4];
            _WriteBox(ref boxImg);

            kmlMng.WriteMain(_nameFile, boxImg);
            kmlMng.WriteTiles(Level);
        }

        #endregion Public Methods

        #region Private Methods

        private void _WriteBox(ref double[] box)
        {
            // Array -> north[0], south[1], west[2], east[3];

            if (box.Length != 4)
            {
                string sMsg = string.Format(
                    "{0}/{1}: Array of box({2}) not equal size 4",
                    this.ToString(), "WriteBox", box.Length);

                throw (new Exception(sMsg));
            }

            double[] argTransform = new double[6];
            _ds.GetGeoTransform(argTransform);

            //argTransform[0] top left x
            //argTransform[1] w-e pixel resolution
            //argTransform[2] rotation, 0 if image is "north up"
            //argTransform[3] top left y
            //argTransform[4] rotation, 0 if image is "north up"
            //argTransform[5] n-s pixel resolution (is negative)

            box[0] = argTransform[3]; // north
            box[1] = argTransform[3] + (argTransform[5] * _ds.RasterYSize); // south
            box[2] = argTransform[0]; // west
            box[3] = argTransform[0] + (argTransform[1] * _ds.RasterXSize); // east
        }

        private void _SetLstKMLTile(int Level)
        {
            // Original
            _AddLstKMLTile(Level, -1);

            // Next Levels
            for (int i = 0; i < Level; i++)
                _AddLstKMLTile(Level - (i + 1), i);

            _lstKMLTile.Sort();
        }

        private void _AddLstKMLTile(int Level, int idOverview)
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

            int[] aryParamsSize = new int[3]; // SizeTile, SizeTileValidX, SizeTileValidY
            aryParamsSize[0] = _sizeTile;

            // Pixels with full values in TileSize
            for (int idGridX = 0; idGridX < nTileX; idGridX++)
                for (int idGridY = 0; idGridY < nTileY; idGridY++)
                    _AddKMLTileEachFull(Level, idOverview, idGridX, idGridY);

            // Pixels without full values in TileSize
            if (nPixelRemTileX > 0)
            {
                aryParamsSize[1] = nPixelRemTileX;
                aryParamsSize[2] = _sizeTile;
                for (int idGridY = 0; idGridY < nTileY; idGridY++)
                    _AddKMLTileEachNotFull(Level, idOverview, nTileX, idGridY, ref aryParamsSize);
            }
            if (nPixelRemTileY > 0)
            {
                aryParamsSize[1] = _sizeTile;
                aryParamsSize[2] = nPixelRemTileY;
                for (int idGridX = 0; idGridX < nTileX; idGridX++)
                    _AddKMLTileEachNotFull(Level, idOverview, idGridX, nTileY, ref aryParamsSize);
            }

            if (nPixelRemTileX > 0 && nPixelRemTileY > 0)
            {
                aryParamsSize[1] = nPixelRemTileX;
                aryParamsSize[2] = nPixelRemTileY;
                _AddKMLTileEachNotFull(Level, idOverview, nTileX, nTileY, ref aryParamsSize);
            }
        }

        private void _AddKMLTileEachFull(int Level, int idOverview, int idGridX, int idGridY)
        {
            int xoff = idGridX * _sizeTile;
            int yoff = idGridY * _sizeTile;

            double[] aryParamsTieRes = new double[4]; // ULTieX, ULTieY, ResX, ResY
            _CalcParamsImg(idOverview, xoff, yoff, ref aryParamsTieRes);

            KMLTile item = new KMLTile();
            item.SetParamsSize(ref aryParamsTieRes, _sizeTile);
            item.SetLevelIdGrid(Level, idGridX, idGridY);

            _lstKMLTile.Add(item);
        }

        private void _AddKMLTileEachNotFull(int Level, int idOverview, int idGridX, int idGridY, ref int[] aryParamsSize)
        {
            int xoff = idGridX * _sizeTile;
            int yoff = idGridY * _sizeTile;

            double[] aryParamsTieRes = new double[4]; // ULTieX, ULTieY, ResX, ResY
            _CalcParamsImg(idOverview, xoff, yoff, ref aryParamsTieRes);

            KMLTile item = new KMLTile();
            item.SetParamsSize(ref aryParamsTieRes, ref aryParamsSize);
            item.SetLevelIdGrid(Level, idGridX, idGridY);

            _lstKMLTile.Add(item);
        }

        private void _CalcParamsImg(int idOverview, int xoff, int yoff, ref double[] ParamsImg)
        {
            // ParamsImg = ULTieX, ULTieY, ResX, ResY

            double[] argTransform = new double[6];
            _ds.GetGeoTransform(argTransform);

            //argTransform[0] /* top left x */
            //argTransform[1] /* w-e pixel resolution */
            //argTransform[2] /* rotation, 0 if image is "north up" */
            //argTransform[3] /* top left y */
            //argTransform[4] /* rotation, 0 if image is "north up" */
            //argTransform[5] /* n-s pixel resolution */

            if (idOverview != -1)
            {
                argTransform[1] *= Math.Pow(2, (double)(idOverview + 1));
                argTransform[5] *= Math.Pow(2, (double)(idOverview + 1));
            }

            argTransform[0] += xoff * argTransform[1]; // X origin
            argTransform[3] += yoff * argTransform[5]; // Y origin

            ParamsImg[0] = argTransform[0]; // ULTieX
            ParamsImg[1] = argTransform[3]; // ULTieY
            ParamsImg[2] = argTransform[1]; // ResX
            ParamsImg[3] = -1 * argTransform[5]; // ResY
        }

        #endregion Private Methods
    }

    public static class EnvironmentalGdal
    {
        /// <summary>
        /// Status for Set enviroment (Register drive, set path,...)
        /// </summary>
        private static bool _MakeEnvironment = false;

        /// <summary>
        /// Set environment (Register drive, set path,...)
        /// </summary>
        public static void MakeEnvironment(string sPathProgram)
        {
            if (_MakeEnvironment) return;

            _SetEnvironment(sPathProgram);

            Gdal.AllRegister();

            _MakeEnvironment = true;
        }

        /// <summary>
        /// Make and set variables system for GDAL DLL´s
        /// Create variables GDAL (IF not exist):
        /// - GDAL_DATA        -> %Program Path% \data
        /// - GEOTIFF_CSV      -> %Program Path% \data
        /// - GDAL_DRIVER_PATH -> %Program Path% \gdalplugins
        ///
        ///  Add PATH
        ///  - PATH            -> PATH + %Program Path% + %Program Path%\dll
        ///
        /// Struct folder for this program and files (Folder : files)
        /// -- %Program Path% : %name program%, %this DLL%, %*_csharp.dll% (Ex.: GdalToTilesWin.exe, MngImg.dll, gdal_csharp.dll, osr_csharp.dll)
        ///          |_ dll: FWTools Dll´s for this program
        ///          |_ data: files find in FWTools\data
        ///          |_ gdalplugins: files find in FWTools\gdalplugins
        /// </summary>
        private static void _SetEnvironment(string sPathProgram)
        {
            // Check exist variables, else, set variable
            _SetValueNewVariable("GDAL_DATA", sPathProgram + "\\data");
            _SetValueNewVariable("GEOTIFF_CSV", sPathProgram + "\\data");
            _SetValueNewVariable("GDAL_DRIVER_PATH", sPathProgram + "\\gdalplugins");

            // Add variable Path new folders
            string sVarPath = System.Environment.GetEnvironmentVariable("Path");
            sVarPath += sPathProgram + ";" + sPathProgram + "\\dll";
            System.Environment.SetEnvironmentVariable("Path", sVarPath);
        }

        private static void _SetValueNewVariable(string sVar, string sValue)
        {
            if (System.Environment.GetEnvironmentVariable(sVar) == null)
                System.Environment.SetEnvironmentVariable(sVar, sValue);
        }
    }
}