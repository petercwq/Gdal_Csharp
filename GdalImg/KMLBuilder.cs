using System;
using System.IO;
using OSGeo.GDAL;

namespace MngImg
{
    public class KMLBuildOptions
    {
        private Action<string> _showStatus;

        // int Min, int Max, int Step, int id
        private Action<int, int, int, int> _progressed;

        public int TileSize { get; private set; }

        public string OutPath { get; private set; }

        public int[] BandsOrder { get; private set; }
        public bool HaveBandsOrder { get { return BandsOrder != null && BandsOrder.Length > 0; } }

        public int StretchStardDesvNum { get; private set; }
        public bool HaveStretchStardDesv { get { return StretchStardDesvNum > 0; } }

        public int NullDataValue { get; private set; }
        public bool HaveNullData { get { return NullDataValue >= 0; } }

        public byte AlphaBandValue { get; private set; }
        public bool HaveAlphaBand { get { return AlphaBandValue >= 0; } }

        public KMLBuildOptions(string outPath, int tileSize = 256, int[] bandsOrder = null, int stretchStardDesv = int.MinValue, int nullDataValue = int.MinValue, byte alphaBandValue = byte.MinValue, Action<string> funcST = null, Action<int, int, int, int> funcSPB = null)
        {
            if (!Directory.Exists(outPath))
                Directory.CreateDirectory(outPath);
            OutPath = outPath;
            _showStatus = funcST;
            _progressed = funcSPB;
            TileSize = tileSize;
            BandsOrder = bandsOrder;
            StretchStardDesvNum = stretchStardDesv;
            NullDataValue = nullDataValue;
            AlphaBandValue = alphaBandValue;
        }

        public bool IsOriginal()
        {
            return !HaveBandsOrder && !HaveStretchStardDesv && !HaveNullData && !HaveAlphaBand;
        }

        public void ShowStatus(string message)
        {
            if (_showStatus != null)
                _showStatus(message);
        }

        public void Progressed(int min, int max, int step, int id)
        {
            if (_progressed != null)
                _progressed(min, max, step, id);
        }
    }

    public class KMLBuilder
    {
        private Dataset _ds;

        #region Public Methods

        public KMLBuilder(string filename)
        {
            _ds = Gdal.Open(filename, Access.GA_Update);
        }

        public void Write(KMLBuildOptions options)
        {
            int nLevel = GlobalMercator.GetTotalLevel(options.TileSize, _ds.RasterXSize, _ds.RasterYSize);
            _SetOverviewSameLevel(nLevel);

            // Original: idOverview = -1 and Level is Max
            _WriteTiles(nLevel, -1, options);
            // int Level, int idOverview, int[] Order, int SizeTile, string sNameDirTile

            // Next Levels
            for (int i = 0; i < nLevel; i++)
                _WriteTiles(nLevel - (i + 1), i, options);
        }

        #endregion Public Methods

        #region Private Methods

        private void _SetOverviewSameLevel(int nLevel)
        {
            if (_ds.GetRasterBand(1).GetOverviewCount() < 1)
            {
                int[] aryOverview = new int[nLevel];
                for (int i = 0; i < nLevel; i++)
                    aryOverview[i] = (int)Math.Pow(2.0, (double)(i + 1));

                // Example:
                // Level = 3
                // aryOverview = { 2, 4, 8 }
                // Common resolutions are 2 4 8 16, meaning an overview at 1/2, 1/4, 1/8, and 1/16 resolution are created.
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

        private void _WriteTiles(int Level, int idOverview, KMLBuildOptions options)
        {
            int nTileX = 0, nTileY = 0, nPixelRemTileX, nPixelRemTileY;

            // nTile          -> number of tile with value
            // nPixelRemTileX -> Remainder pixel before nTile

            if (idOverview == -1) // Original
            {
                nTileX = Math.DivRem(_ds.RasterXSize, options.TileSize, out nPixelRemTileX);
                nTileY = Math.DivRem(_ds.RasterYSize, options.TileSize, out nPixelRemTileY);
            }
            else
            {
                nTileX = Math.DivRem(_ds.GetRasterBand(1).GetOverview(idOverview).XSize, options.TileSize, out nPixelRemTileX);
                nTileY = Math.DivRem(_ds.GetRasterBand(1).GetOverview(idOverview).YSize, options.TileSize, out nPixelRemTileY);
            }

            options.ShowStatus(string.Format("Level {0} TileX 0->{1} TileY 0->{2} ...", Level, nTileX, nTileY));
            options.Progressed(0, nTileX, 1, 0);

            // Pixels with full values in TileSize
            for (int idGridX = 0; idGridX < nTileX; idGridX++)
            {
                options.Progressed(0, 0, 0, idGridX);
                for (int idGridY = 0; idGridY < nTileY; idGridY++)
                    WriteTileEach(Level, idOverview, idGridX, idGridY, options.TileSize, options.TileSize, options);
            }

            // Pixels without full values in TileSize
            if (nPixelRemTileX > 0)
            {
                options.ShowStatus(string.Format("Level {0} Remainder TileX 0->{1} ...", Level, nTileY));
                options.Progressed(0, nTileY, 1, 0);
                for (int idGridY = 0; idGridY < nTileY; idGridY++)
                {
                    options.Progressed(0, 0, 0, idGridY);
                    WriteTileEach(Level, idOverview, nTileX, idGridY, nPixelRemTileX, options.TileSize, options);
                }
            }

            if (nPixelRemTileY > 0)
            {
                options.ShowStatus(string.Format("Level {0} Remainder TileY 0->{1} ...", Level, nTileX));
                options.Progressed(0, nTileX, 1, 0);
                for (int idGridX = 0; idGridX < nTileX; idGridX++)
                {
                    options.Progressed(0, 0, 0, idGridX);
                    WriteTileEach(Level, idOverview, idGridX, nTileY, options.TileSize, nPixelRemTileY, options);
                }
            }

            if (nPixelRemTileX > 0 && nPixelRemTileY > 0)
            {
                options.ShowStatus("Remainder TileX & TileY ...");
                WriteTileEach(Level, idOverview, nTileX, nTileY, nPixelRemTileX, nPixelRemTileY, options);
            }
        }

        private void WriteTileEach(int Level, int idOverview, int idGridX, int idGridY, int nPixelX, int nPixelY, KMLBuildOptions options)
        {
            int xoff = idGridX * options.TileSize;
            int yoff = idGridY * options.TileSize;

            Dataset dsOut = CreatePngDataset(idOverview, xoff, yoff, options.TileSize, options.HaveAlphaBand);

            WriteTileEachDS(idOverview, ref dsOut, xoff, yoff, nPixelX, nPixelY, options);

            if (options.HaveNullData)
                _SetNullDataImage(dsOut, options.NullDataValue);

            // Tiles: sPath\$[level]_$[y]_$[x].png
            string sPathNameTile = string.Format("{0}\\{1}_{2}_{3}.png", options.OutPath, Level, idGridX, idGridY);
            _DatasetPng2File(dsOut, sPathNameTile);

            dsOut.FlushCache();
            dsOut.Dispose();
        }

        private Dataset CreatePngDataset(int idOverview, int xoff, int yoff, int tileSize, bool hasAlpha)
        {
            string[] aryOption = { "" };
            OSGeo.GDAL.Driver drv = Gdal.GetDriverByName("MEM");

            int nBand = hasAlpha ? _ds.RasterCount + 1 : _ds.RasterCount;
            Dataset dsReturn = drv.Create("filememory", tileSize, tileSize, nBand, _ds.GetRasterBand(1).DataType, aryOption);

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

        private void WriteTileEachDS(int idOverview, ref Dataset dsOut, int xoff, int yoff, int xsize, int ysize, KMLBuildOptions options)
        {
            byte[] pixelsTile = new byte[options.TileSize * options.TileSize];

            byte[] pixelsAlpha = new byte[0];
            if (options.HaveAlphaBand)
            {
                pixelsAlpha = null;
                pixelsAlpha = new byte[options.TileSize * options.TileSize];
            }

            int idOrder = 0;

            for (int id = 0; id < _ds.RasterCount; id++)
            {
                Band bdOut = dsOut.GetRasterBand(id + 1);

                idOrder = options.HaveBandsOrder ? options.BandsOrder[id] : id + 1;

                Band bdIn = (idOverview != -1) ? _ds.GetRasterBand(idOrder).GetOverview(idOverview) : _ds.GetRasterBand(idOrder);

                if (xsize == options.TileSize)
                {
                    bdIn.ReadRaster(xoff, yoff, xsize, ysize, pixelsTile, xsize, ysize, 0, 0);
                    if (options.HaveStretchStardDesv)
                        ImageProcessing.SetStretchStardDevi(bdIn, ref pixelsTile, options.StretchStardDesvNum);
                }
                else
                {
                    byte[] pixels = new byte[xsize * ysize];
                    bdIn.ReadRaster(xoff, yoff, xsize, ysize, pixels, xsize, ysize, 0, 0);
                    if (options.HaveStretchStardDesv)
                        ImageProcessing.SetStretchStardDevi(bdIn, ref pixels, options.StretchStardDesvNum);

                    for (int iy = 0; iy < ysize; iy++)
                        System.Buffer.BlockCopy(pixels, iy * xsize, pixelsTile, iy * options.TileSize, xsize);
                }
                bdOut.WriteRaster(0, 0, options.TileSize, options.TileSize, pixelsTile, options.TileSize, options.TileSize, 0, 0);

                if (options.HaveAlphaBand)
                    ImageProcessing.PopulateAlphaPixels(ref pixelsTile, ref pixelsAlpha);

                bdOut.FlushCache(); bdIn.FlushCache();
                bdIn.Dispose(); bdOut.Dispose();
            }

            if (options.HaveAlphaBand)
            {
                Band bdAlpha = dsOut.GetRasterBand(_ds.RasterCount + 1);
                bdAlpha.WriteRaster(0, 0, options.TileSize, options.TileSize, pixelsAlpha, options.TileSize, options.TileSize, 0, 0);
                bdAlpha.FlushCache();
                bdAlpha.Dispose();
            }
        }

        private void _SetNullDataImage(Dataset dsOut, int nullValue)
        {
            for (int nBand = 1; nBand < dsOut.RasterCount; nBand++)
                dsOut.GetRasterBand(nBand).SetNoDataValue(nullValue);
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
