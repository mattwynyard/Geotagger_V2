﻿using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Geotagger_V2
{
    public static class ImageExtensions
    {
        public static byte[] ToByteArray(this System.Drawing.Image image, ImageFormat format)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, format);
                return ms.ToArray();
            }
        }
    }

    public static class ModifyProgressBarColor
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr w, IntPtr l);
        public static void SetState(this ProgressBar pBar, int state)
        {
            SendMessage(pBar.Handle, 1040, (IntPtr)state, IntPtr.Zero);
        }
    }

    public static class Utilities
    {
        public static string getInspector(string inspector)
        {
            switch (inspector)
            {
                case "Ian Nobel":
                    return "IN";
                case "Karen Croft":
                    return "KC";
                case "Ross Baker":
                    return "RB";
                case "Scott Fraser":
                    return "SF";
                case "Paul Newman":
                    return "PN";
                case "Andrew Bright":
                    return "AB";
                default:
                    return "";
            }
        }

        public static bool directoryHasFiles(string path)
        {
            if (path != null)
            {
                return Directory.EnumerateFileSystemEntries(path).Any();
            }
            else
            {
                return false;
            }
        }

        public static bool isFileValid(string path)
        {
            System.IO.FileInfo fi = null;
            try
            {
                fi = new System.IO.FileInfo(path);
            }
            catch (ArgumentException) { }
            catch (System.IO.PathTooLongException) { }
            catch (NotSupportedException) { }
            if (ReferenceEquals(fi, null))
            {
                // file name is not valid
                return false;
            }
            else
            {
                return true;
                // file name is valid... May check for existence by calling fi.Exists.
            }
        }

        public static DateTime byteToDate(byte[] b)
        {
            try
            {
                string dateTime = Encoding.UTF8.GetString(b);
                int year = byteToDateInt(b, 0, 4);
                int month = byteToDateInt(b, 5, 2);
                int day = byteToDateInt(b, 8, 2);
                int hour = byteToDateInt(b, 11, 2);
                int min = byteToDateInt(b, 14, 2);
                int sec = byteToDateInt(b, 17, 2);
                return new DateTime(year, month, day, hour, min, sec);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Console.WriteLine(ex.StackTrace);
                return new DateTime();
            }
        }
        public static int byteToDateInt(byte[] b, int offset, int len)
        {
            byte[] a = new byte[len];
            Array.Copy(b, offset, a, 0, len);
            string s = ASCIIEncoding.UTF8.GetString(a);
            try
            {
                int i = Int32.Parse(s);
                return i;
            }
            catch (FormatException ex)
            {
                Console.WriteLine(ex.StackTrace);
                return -1;
            }
        }

        public static DateTime byteToDate(byte[] b, string date)
        {
            try
            {
                int len = b.Length;
                int year = Int32.Parse(date.Substring(0, 4));
                int month = Int32.Parse(date.Substring(5, 2));
                int day = Int32.Parse(date.Substring(8, 2));
                int hour = BitConverter.ToInt32(b, 0);

                int minute = BitConverter.ToInt32(b, 8);
                int second = BitConverter.ToInt32(b, 16);
                return new DateTime(year, month, day, hour, minute, second);
            } catch (FormatException ex)
            {
                Console.WriteLine(ex.StackTrace);
                return new DateTime();
            }   
        }

        public static double byteToDecimal(byte[] b) //type 5
        {
            double numerator = BitConverter.ToInt32(b, 0);
            double denominator = BitConverter.ToInt32(b, 4);

            return Math.Round(numerator / denominator, 2);
        }
        public static double byteToDegrees(byte[] source)
        {
            double coordinate = 0;
            int dms = 1; //degrees minute second divisor
            for (int offset = 0; offset < source.Length; offset += 8)
            {
                byte[] b = new byte[4];
                Array.Copy(source, offset, b, 0, 4);
                int temp = BitConverter.ToInt32(b, 0);
                Array.Copy(source, offset + 4, b, 0, 4);
                int multiplier = BitConverter.ToInt32(b, 0) * dms;
                dms *= 60;
                coordinate += Convert.ToDouble(temp) / Convert.ToDouble(multiplier);
            }
            return Math.Round(coordinate, 6);
        }
    }
}
