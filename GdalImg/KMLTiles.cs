using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using OSGeo.GDAL;

namespace MngImg
{
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

            if (iniLevel == nLevel) // Don磘 have link
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
}