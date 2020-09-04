using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO.Compression;

namespace Geotagger_V2
{
    class ThreadInfo
    {
        public ThreadInfo()
        {

        }

        public string Photo { get; set; }

        public string PhotoPath { get; set; }

        public string File { get; set; }
        public string OutPath { get; set; }

        public int Length { get; set; }

        public Record Record { get; set; }

        public ZipArchiveEntry Entry { get; set; }

        public int QueueSize { get; set; }

        public Boolean Zip { get; set; }

        public Stopwatch Timer { get; set; }

        public int StartCount { get; set; }

        public PropertyItem propItemLatRef { get; set; }

        public PropertyItem propItemLat { get; set; }

        public PropertyItem propItemLonRef { get; set; }

        public PropertyItem propItemLon { get; set; }

        public PropertyItem propItemAltRef { get; set; }

        public PropertyItem propItemAlt { get; set; }

        public PropertyItem propItemSat { get; set; }

        public PropertyItem propItemDir { get; set; }

        public PropertyItem propItemVel { get; set; }

        public PropertyItem propItemPDop { get; set; }

        public PropertyItem propItemDateTime { get; set; }

        public IProgress<int> ProgressHandler { get; set; }

    }
}

