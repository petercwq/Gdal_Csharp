/******************************************************************************
 * $Id: IImage.cs 00002 2008-04-17  luiz $
 *
 * Name:     IImage.cs
 * Project:  Manager Image from GDAL
 * Purpose:  Interface for class mananger image.
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
 */

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