using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Geotagger_V2
{
    public abstract class GeotagManger
    {

        protected string _progressMessage;
        protected double _progressValue;
        protected int _photoCount;
        protected int _progressPhotoQueueCount;
        protected Boolean mZip;
        protected BlockingCollection<string> photoQueue;

        /// <summary>
        /// Adds all image files(.jpg) found in directory to a concurrent dictionary- key: filename, value: filepath
        /// </summary>
        /// <param name="path">parent folder path</param>
        /// <param name="zip">searches and reads zip directory for .jpg files</param>
        public virtual void photoReader(string path, bool zip)
        {
            photoQueue = new BlockingCollection<string>(); ;
            string initialMessage = "Searching directories...";
            Interlocked.Exchange(ref _progressMessage, initialMessage);
            Task search = Task.Factory.StartNew(() =>
            {
                if (zip)
                {
                    //photoDict = new ConcurrentDictionary<string, string>(); //
                    string[] files = Directory.GetFiles(path, "*.zip", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        string f = file;
                        using (FileStream zipToOpen = new FileStream(file, FileMode.Open))
                        {
                            using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
                            {
                                foreach (ZipArchiveEntry entry in archive.Entries)
                                {
                                    string s = entry.FullName;
                                    string[] tokens = s.Split('/');
                                    s = tokens[tokens.Length - 1];
                                    if (s.Substring(s.Length - 3) == "jpg")

                                    {
                                        string key = s.Substring(0, s.Length - 4);

                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    photoQueue = new BlockingCollection<string>();
                    string[] files = Directory.GetFiles(path, "*.jpg", SearchOption.AllDirectories);
                    int fileCount = files.Length;
                    Interlocked.Exchange(ref _photoCount, fileCount);
                    int i = 0;
                    string message = "Building photo queue...";
                    Interlocked.Exchange(ref _progressMessage, message);
                    foreach (var file in files)
                    {
                        bool added = photoQueue.TryAdd(file);
                        if (added)
                        {
                            Interlocked.Increment(ref i);
                            double newvalue = ((double)i / (double)fileCount) * 100;
                            Interlocked.Exchange(ref _photoCount, i);
                            Interlocked.Exchange(ref _progressValue, newvalue);
                            Interlocked.Exchange(ref _progressPhotoQueueCount, photoQueue.Count);
                        }
                        else
                        {
                            Console.WriteLine("failed to add to photo dictionary");
                        }
                    }
                    photoQueue.CompleteAdding();
                }
            });
            Task.WaitAll(search);
            Interlocked.Exchange(ref _progressMessage, "Finished");
        }
    }
}
