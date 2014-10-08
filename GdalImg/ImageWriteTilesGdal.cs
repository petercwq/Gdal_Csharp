using System;
using OSGeo.GDAL;

namespace MngImg
{
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
