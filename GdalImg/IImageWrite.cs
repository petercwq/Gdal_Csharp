namespace MngImg
{
    public interface IImageWrite
    {
        void SetOptionOrder(int[] Order);

        void SetOptionSubset(int xoff, int yoff, int xsize, int ysize);

        void SetOptionStretchStardDesv(int nSD);

        void SetOptionNullData(int Value);

        void SetOptionOverview(int NumOverview);

        void SetOptionMakeAlphaBand(byte ValueAlphaBand);

        void WriteFile(string sDrive, string sPathFileName);
    }
}