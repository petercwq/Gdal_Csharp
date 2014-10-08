using System;
using OSGeo.GDAL;

namespace MngImg
{
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
                HaveOrderBands = HaveSubset = HaveStretchStardDesv = HaveNullData = HaveOverview = HaveAlphaBand = false;
            }

            public bool IsOriginal()
            {
                return !HaveOrderBands && !HaveSubset && !HaveStretchStardDesv && !HaveNullData && !HaveOverview && !HaveAlphaBand;
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
                dsOut = _opt.HaveSubset ? _CreateDSSubsetWrite(drv, sPathFileName) : _CreateDSWrite(drv, sPathFileName);
                _SetDSWriteFile(ref dsOut);
            }

            dsOut.FlushCache();
            dsOut.Dispose();
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
}
