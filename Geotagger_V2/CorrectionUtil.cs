using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using Emgu.CV.Structure;
using System.Drawing.Imaging;

namespace Geotagger_V2
{
    public static class CorrectionUtil
    {

        public static Image<Bgr, Byte> GammaCorrection(Image<Bgr, Byte> image)
        {
            //Mat src = CvInvoke.Imread("C:\\Onsite\\opencvTest\\gamma_test.jpg", ImreadModes.AnyColor);
            Mat src = image.Mat;
            Mat hsv = new Mat();
            CvInvoke.CvtColor(src, hsv, ColorConversion.Bgr2Hsv);


            MCvScalar mean = CvInvoke.Mean(hsv); //get average brightness V channel
            double meanV = mean.V2 / 256; //normalise
            hsv.Dispose();
            //soure: Automatic gamma correction based on average of brightness - Babakhani & Zarei
            //Advances in Computer Science Volume 4 issue 6 No.18 2015
            double gamma = -0.3 / (Math.Log10(meanV));

            Image<Bgr, Byte> img = src.ToImage<Bgr, Byte>();
            img._GammaCorrect(gamma);
            src.Dispose();
            return img;
            //Image<Bgr, Byte> bgr = img.Convert<Bgr, Byte>();
            //img.Save("C:\\Onsite\\opencvTest\\gamma.jpg");

        }

        public static void ClaheCorrection(String inpath, double clipLimit)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Mat src = CvInvoke.Imread(inpath, ImreadModes.AnyColor);
            Mat lab = new Mat();
            CvInvoke.CvtColor(src, lab, ColorConversion.Bgr2Lab);
            src.Dispose();

            VectorOfMat channels = new VectorOfMat();
            CvInvoke.Split(lab, channels);
            Mat dst = new Mat();
            Size size = new Size(8, 8);
            CvInvoke.CLAHE(channels[0], clipLimit, size, dst);

            dst.CopyTo(channels[0]);
            dst.Dispose();
            CvInvoke.Merge(channels, lab);
            Mat clahe = new Mat();
            CvInvoke.CvtColor(lab, clahe, ColorConversion.Lab2Bgr);
            lab.Dispose();
            String dir = Path.GetDirectoryName(inpath);
            String outpath = dir + "\\" + Path.GetFileNameWithoutExtension(inpath) + "_clahe.jpg";

            sw.Stop();
            clahe.Save(outpath);

            clahe.Dispose();
            //clahe.Save("C:\\EXIFGeotagger\\opencv\\contrast_equalise_clahe.jpg");


        }

        public static Image ImageFromEMGUImage(Image<Bgr, Byte> image)
        {
            MemoryStream ms = new MemoryStream();
            Image bitmap = image.ToBitmap();
            image.Dispose();
            bitmap.Save(ms, ImageFormat.Jpeg);
            bitmap.Dispose();
            ms.Seek(0, SeekOrigin.Begin);

            return Image.FromStream(ms);
        }

        public static Image<Bgr, Byte> ClaheCorrection(Mat src, double clipLimit)
        {
            //Stopwatch sw = new Stopwatch();
            //sw.Start();
            //Mat src = CvInvoke.Imread(inpath, ImreadModes.AnyColor);
            Mat lab = new Mat();
            CvInvoke.CvtColor(src, lab, ColorConversion.Bgr2Lab);
            src.Dispose();

            VectorOfMat channels = new VectorOfMat();
            CvInvoke.Split(lab, channels);
            Mat dst = new Mat();
            Size size = new Size(8, 8);
            CvInvoke.CLAHE(channels[0], 0.5, size, dst);

            dst.CopyTo(channels[0]);
            dst.Dispose();
            CvInvoke.Merge(channels, lab);
            Mat clahe = new Mat();
            CvInvoke.CvtColor(lab, clahe, ColorConversion.Lab2Bgr);
            lab.Dispose();

            Image<Bgr, Byte> image = clahe.ToImage<Bgr, Byte>();
            clahe.Dispose();

            return image;
        }

        public static Mat GetMatFromImage(Image image)
        {
            int stride = 0;
            Bitmap bmp = new Bitmap(image);
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, bmp.PixelFormat);
            PixelFormat pf = bmp.PixelFormat;
            if (pf == PixelFormat.Format32bppArgb)
            {
                stride = bmp.Width * 4;
            }
            else
            {
                stride = bmp.Width * 3;
            }
            Image<Bgr, byte> cvImage = new Image<Bgr, byte>(bmp.Width, bmp.Height, stride, (IntPtr)bmpData.Scan0);
            bmp.UnlockBits(bmpData);
            //bmp.Dispose();
            return cvImage.Mat;
        }
    }
}

