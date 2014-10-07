/******************************************************************************
 * $Id: GdalToTilesWin.cs 00002 2008-04-17  luiz $
 *
 * Name:     GdalToTilesWin.cs
 * Project:  Manager Image from GDAL
 * Purpose:  GUI for make tiles.
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
 * References (GDAL and Manager):
 * - gdal_csharp
 * - MngImg
 */

using System;
using System.Windows.Forms;

using MngImg;

namespace GdalToTilesWin
{
    public partial class GdalToTilesWin : Form
    {
        private ImageGdal _Img;

        public GdalToTilesWin()
        {
            InitializeComponent();
            _Img = null;

            EnvironmentalGdal.MakeEnvironment(Application.StartupPath);
            // Application.StartupPath;

            SetItemCmbSD();
            SetItemCmbSizeTile();
        }

        private void ShowDescriptImg()
        {
            treeViewDescript.Nodes.Clear();

            //Array -> north[0], south[1], west[2], east[3];
            double[] box = new double[4];

            int idNode = 0;
            treeViewDescript.Nodes.Add("Source");
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("Path {0}", _Img.Path));
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("Name {0}", _Img.FileName));
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("Format {0}", _Img.Format));

            idNode++;
            treeViewDescript.Nodes.Add("Raster");
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("Size X/Y {0}/{1}", _Img.XSize, _Img.YSize));
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("ResolutionX/Y {0:0.0000}/{1:0.0000}", _Img.XResolution, _Img.YResolution));
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("Number Bands {0}", _Img.NumberBand));
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("Type {0}", _Img.Type));

            idNode++;
            _Img.WriteBox(ref box);
            treeViewDescript.Nodes.Add("Extent");
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("North {0:0.0000}", box[0]));
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("West {0:0.0000}", box[2]));
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("South {0:0.0000}", box[1]));
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("East {0:0.0000}", box[3]));

            idNode++;
            treeViewDescript.Nodes.Add("Spatial Reference");
            treeViewDescript.Nodes[idNode].Nodes.Add(string.Format("{0} Geodesic WGS84(EPSG:4326)", _Img.IsSameCS("EPSG:4326") ? "It is" : "It is not"));
            treeViewDescript.Nodes[idNode].Nodes.Add(_Img.SpatialReference);
        }

        private void SetItemCmbOrder()
        {
            // Clear
            cmbBxBand1.Items.Clear();
            cmbBxBand2.Items.Clear();
            cmbBxBand3.Items.Clear();

            int nBand = _Img.NumberBand;
            for (int i = 0; i < nBand; i++)
            {
                cmbBxBand1.Items.Add(i + 1);
                cmbBxBand2.Items.Add(i + 1);
                cmbBxBand3.Items.Add(i + 1);
            }
            cmbBxBand1.SelectedIndex = cmbBxBand2.SelectedIndex = cmbBxBand3.SelectedIndex = 0;

            if (nBand > 1) cmbBxBand2.SelectedIndex = cmbBxBand3.SelectedIndex = 1;
            if (nBand > 2) cmbBxBand3.SelectedIndex = 2;
        }

        private void SetItemCmbSD()
        {
            for (int i = 1; i < 5; i++)
                cmBxSD.Items.Add(i);

            cmBxSD.SelectedIndex = 1; // SD = 2
        }

        private void SetItemCmbSizeTile()
        {
            // 2^8 = 256
            for (int i = 8; i < 11; i++)
                cmBxSize.Items.Add(System.Convert.ToInt32(Math.Pow(2, i)));

            cmBxSize.SelectedIndex = 1; // 512
        }

        private string MakePathTiles(string sSelectedPath)
        {
            string sPathTiles = sSelectedPath + "\\" + _Img.FileName + "_tiles";

            if (System.IO.Directory.Exists(sPathTiles))
                foreach (string file in System.IO.Directory.GetFiles(sPathTiles))
                    System.IO.File.Delete(file);
            else System.IO.Directory.CreateDirectory(sPathTiles);

            return sPathTiles;
        }

        private void RunWriteTiles(string sSelectedPath)
        {
            txtBxStatus.Text = "";

            if (!_Img.IsSameCS("EPSG:4326")) _Img.Warp("EPSG:4326");

            int sizeTile = (int)cmBxSize.SelectedItem;

            string sPathTiles = MakePathTiles(sSelectedPath);
            ImageWriteTilesGdal imgWrite = new ImageWriteTilesGdal
                (_Img.Dataset, sizeTile, sPathTiles, new StatusText(FuncStatusText), new StatusProgressBar(FuncStatusProgressBar));

            imgWrite.SetOptionNullData(0);
            imgWrite.SetOptionMakeAlphaBand((byte)0);

            if (ckOrder.Checked)
            {
                int[] Order = new int[3];
                Order[0] = (int)cmbBxBand1.SelectedItem;
                Order[1] = (int)cmbBxBand2.SelectedItem;
                Order[2] = (int)cmbBxBand3.SelectedItem;
                imgWrite.SetOptionOrder(Order);
            }

            if (ckStrech.Checked)
                imgWrite.SetOptionStretchStardDesv((int)cmBxSD.SelectedItem);

            FuncStatusText("Saving tiles");
            imgWrite.Write();

            KMLWriteTilesGdal kmlWrite = new KMLWriteTilesGdal(_Img.Dataset, sizeTile, sPathTiles, _Img.FileName);
            kmlWrite.Write();

            FuncStatusText(string.Format(
               "\r\nSuccess writed tiles!\r\nPath tiles: {0}\r\nSource KML: {1}.kml", sPathTiles, _Img.FileName));

            progressBar1.Value = 0;
        }

        public void FuncStatusText(string msgStatus)
        {
            txtBxStatus.AppendText("\r\n" + msgStatus);
            txtBxStatus.Refresh();
        }

        public void FuncStatusProgressBar(int Min, int Max, int Step, int id)
        {
            if (Max > 0)
            {
                progressBar1.Maximum = Max;
                progressBar1.Minimum = Min;
                progressBar1.Value = 0;

                txtBxStatus.Refresh();
                progressBar1.Refresh();

                Application.DoEvents();
            }
            if (Step > 0) progressBar1.Step = Step;
            if ((id + 1) > 0)
            {
                if ((id + 1) > progressBar1.Maximum) id = 0;
                progressBar1.Value = id + 1;
            }
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Geo Image Files(*.IMG;*.TIF)|*.IMG;*.TIF";
            dialog.Title = "Select a image files";

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (ImageGdal.IsValidImage(dialog.FileName))
                {
                    _Img = new ImageGdal(dialog.FileName);

                    grpBxOptions.Enabled = true;
                    btnSelectPath.Enabled = true;

                    FuncStatusText(string.Format("Getting description\r\n{0}...", dialog.FileName));

                    pctBoxImg.Image = _Img.GetBitmap(pctBoxImg.Size, null, 0);

                    ShowDescriptImg();
                    SetItemCmbOrder();

                    txtBxStatus.Text = "";
                }
                else
                {
                    txtBxStatus.Text = string.Format("Invalid Format image\r\n{0}", dialog.FileName);
                }
            }
        }

        private void btnSelectPath_Click(object sender, EventArgs e)
        {
            // RunWriteTiles(@"C:\_trabalhos\BD_test\tileskml");

            FolderBrowserDialog fdlBrw = new FolderBrowserDialog();
            fdlBrw.Description = "Select Path for Tiles(KML & images)";
            fdlBrw.ShowNewFolderButton = true;

            if (fdlBrw.ShowDialog() == DialogResult.OK)
                RunWriteTiles(fdlBrw.SelectedPath);
        }

        private void ckStrech_CheckedChanged(object sender, EventArgs e)
        {
            cmBxSD.Enabled = ckStrech.Checked;
        }

        private void ckOrder_CheckedChanged(object sender, EventArgs e)
        {
            cmbBxBand1.Enabled = cmbBxBand2.Enabled = cmbBxBand3.Enabled = ckOrder.Checked;
        }

        private void btPreview_Click(object sender, EventArgs e)
        {
            int[] Order = null;
            int SD = 0;

            if (ckOrder.Checked)
            {
                Order = new int[3];
                Order[0] = (int)cmbBxBand1.SelectedItem;
                Order[1] = (int)cmbBxBand2.SelectedItem;
                Order[2] = (int)cmbBxBand3.SelectedItem;
            }

            if (ckStrech.Checked)
                SD = (int)cmBxSD.SelectedItem;

            pctBoxImg.Image = _Img.GetBitmap(pctBoxImg.Size, Order, SD);
        }

        private void treeViewDescript_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            try
            {
                Clipboard.SetDataObject(treeViewDescript.SelectedNode.Text, true);
            }
            catch (Exception)
            {
            }
        }

        private void btAbout_Click(object sender, EventArgs e)
        {
            string sMsg =
                "Purpose: Make tiles for KML (superoverlay format) from one image\r\n" +
                "This project use GDAL(www.gdal.org) for manager image data\r\n" +
                "It´s program with GNU license and can be download from http://www.codeplex.com/gdal2tilescsharp\r\n\r\n" +
                "Luiz Motta\r\n" +
                "2008-04-15\r\n" +
                "\r\nVersion 0.9";
            MessageBox.Show(sMsg, "GdalToTiles", MessageBoxButtons.OK);
        }
    }
}