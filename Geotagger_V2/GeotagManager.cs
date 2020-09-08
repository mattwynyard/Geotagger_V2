using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Emgu.CV;
using Emgu.CV.Structure;

namespace Geotagger_V2
{
    public class GeotagManager
    {
        private string progressMessage = "";
        private double progressValue = 0;
        private int progressRecordCount = 0;
        private int progressRecordDictCount = 0;
        private int progressPhotoQueueCount = 0;
        private int progressBitmapQueueCount = 0;
        private int progressRecordDictErrors = 0;
        private int photoCount = 0;
        private int geotagCount;
        private int sizeRecordQueue;
        private int sizeBitmapQueue;
        private Boolean mZip;
        private Stopwatch stopwatch;
        private ConcurrentDictionary<string, Record> recordDict;
        private BlockingCollection<string> photoQueue;
        //private BlockingCollection<Record> recordQueue;
        private BlockingCollection<ThreadInfo> geotagQueue;
        //private ConcurrentDictionary<string, string> photoDict;
        private ConcurrentDictionary<string, Record> noPhotoDict;
        private BlockingCollection<object[]> bitmapQueue;
        private ConcurrentDictionary<string, Exception> errorDict;
        private string connectionString;
        private ProgressObject progress;
        private Boolean geotagging = false;

        private static ManualResetEvent mre = new ManualResetEvent(false);
        private static readonly object obj = new object();

        public GeotagManager()
        {
            intialise();
        }
        public GeotagManager(int sizeRecordQueue, int sizeBitmapQueue)
        {
            intialise(sizeRecordQueue, sizeBitmapQueue);
        }

        private void intialise(int sizeRecordQueue = 50000, int sizeBitmapQueue = 50)
        {
            geotagCount = 0;
            this.sizeRecordQueue = sizeRecordQueue;
            this.sizeBitmapQueue = sizeBitmapQueue;
            recordDict = new ConcurrentDictionary<string, Record>();
            
            //recordQueue = new BlockingCollection<Record>(sizeRecordQueue);
            geotagQueue = new BlockingCollection<ThreadInfo>();
            noPhotoDict = new ConcurrentDictionary<string, Record>();
            bitmapQueue = new BlockingCollection<object[]>(sizeBitmapQueue);
            errorDict = new ConcurrentDictionary<string, Exception>();
            stopwatch = Stopwatch.StartNew();
            progress = new ProgressObject();
        }

        //public BlockingCollection<string> buildQueue(string path)
        //{

        //    BlockingCollection<string> fileQueue = new BlockingCollection<string>();
        //    string[] files = Directory.GetFiles(path, "*.jpg", SearchOption.AllDirectories);
        //    Task producer = Task.Factory.StartNew(() =>
        //    {
        //        foreach (string file in files)
        //        {
        //            fileQueue.Add(file);
        //        }
        //        fileQueue.CompleteAdding();
        //    });
        //    Task.WaitAll(producer);
        //    return fileQueue;
        //}

        /// <summary>
        /// Adds all image files(.jpg) found in directory to a concurrent dictionary- key: filename, value: filepath
        /// </summary>
        /// <param name="path">parent folder path</param>
        /// <param name="zip">searches and reads zip directory for .jpg files</param>
        public void photoReader(string path, Boolean zip)
        {
            string initialMessage = "Searching directories...";
            Interlocked.Exchange(ref progressMessage, initialMessage);
            mZip = zip;
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
                                        //bool added = photoDict.TryAdd(key, file);
                                        //if (!added)
                                        //{
                                        //    string photo = file;
                                        //    Console.WriteLine(photo);
                                        //}
                                    }
                                }
                            }
                        }
                    }

                }
                else
                {
                    //photoDict = new ConcurrentDictionary<string, string>();
                    photoQueue = new BlockingCollection<string>();
                    string[] files = Directory.GetFiles(path, "*.jpg", SearchOption.AllDirectories);
                    int fileCount = files.Length;
                    Interlocked.Exchange(ref photoCount, fileCount);
                    int i = 0;
                    string message = "Building photo queue...";
                    Interlocked.Exchange(ref progressMessage, message);
                    foreach (var file in files)
                    {
                        //string key = Path.GetFileNameWithoutExtension(file);
                        //bool added = photoDict.TryAdd(key, file);
                        Boolean added = photoQueue.TryAdd(file);
                        
                        if (added)
                        {
                            i++;
                            double newvalue = ((double)i / (double)fileCount) * 100;
                            Interlocked.Exchange(ref photoCount, i);
                            Interlocked.Exchange(ref progressValue, newvalue);
                            Interlocked.Exchange(ref progressPhotoQueueCount, photoQueue.Count);
                        } else
                        {
                            Console.WriteLine("failed to add to photo dictionary");
                        }
                    }
                }
            });
            Task.WaitAll(search);
            Interlocked.Exchange(ref progressMessage, "Finished");
        }

        public async Task<TaskStatus> readDatabase(string dbPath, string inspector)
        {
            Interlocked.Exchange(ref progressMessage, "Reading database...");
            Interlocked.Exchange(ref progressValue, 0);
            string _inspector = Utilities.getInspector(inspector);
            string connectionString = string.Format("Provider={0}; Data Source={1}; Jet OLEDB:Engine Type={2}",
                "Microsoft.Jet.OLEDB.4.0", dbPath, 5);
            OleDbConnection connection = new OleDbConnection(connectionString);
            string strSQL;
            //string strRecord;
            string lengthSQL; //sql count string
            if (_inspector == "")
            {
                strSQL = "SELECT * FROM PhotoList WHERE PhotoList.GeoMark = true;";
                lengthSQL = "SELECT Count(PhotoID) FROM PhotoList WHERE PhotoList.GeoMark = true;";
            }
            else
            {
                strSQL = "SELECT * FROM PhotoList WHERE PhotoList.GeoMark = true AND PhotoList.Inspector = '" + _inspector + "';";
                lengthSQL = "SELECT Count(PhotoID) FROM PhotoList WHERE PhotoList.GeoMark = true  AND PhotoList.Inspector = '" + _inspector + "';";
            }
            OleDbCommand commandLength = new OleDbCommand(lengthSQL, connection);
            connection.Open();
            int recordCount = Convert.ToInt32(commandLength.ExecuteScalar());
            Interlocked.Exchange(ref progressRecordCount, recordCount);
            commandLength.Dispose();
            
            Task taskreader = Task.Factory.StartNew(() =>
            {
                int i = 0;
                int errors = 0;
                Interlocked.Exchange(ref progressMessage, "Building Record Dictionary...");
                OleDbCommand command = new OleDbCommand(strSQL, connection);
                using (OleDbDataReader reader = command.ExecuteReader())
                {
                    object[] row;
                    while (reader.Read())
                    {
                        row = new object[reader.FieldCount];
                        reader.GetValues(row);
                        Record r = buildRecord(row).Result;
                        try
                        {
                            Boolean success = recordDict.TryAdd(r.PhotoName, r);
                            if (success)
                            {
                                Interlocked.Increment(ref i);
                                double newvalue = ((double)i / (double)recordCount) * 100;
                                Interlocked.Exchange(ref progressValue, newvalue);
                                Interlocked.Exchange(ref progressRecordDictCount, recordDict.Count);
                            } else
                            {
                                Console.WriteLine("Failed to add: " + r.PhotoName);
                                Interlocked.Increment(ref errors);
                                Interlocked.Exchange(ref progressRecordDictErrors, errors);
                            }

                        } catch (InvalidOperationException ex)
                        {
                            Console.WriteLine(ex.StackTrace);
                        }
                        
                       
                    }
                    reader.Close();
                    command.Dispose();
                }
                double finalvalue = ((double)i / (double)recordCount) * 100;
                Interlocked.Exchange(ref progressValue, finalvalue);
                Interlocked.Exchange(ref progressRecordDictCount, recordDict.Count);
            });
            await Task.WhenAll(taskreader);
            try
            {
                connection.Close();
            } catch
            {

            }
            return taskreader.Status;
            
        }

        public async void writeGeotag(string outPath)
        {
            Interlocked.Exchange(ref progressValue, 0);
            
            Interlocked.Exchange(ref progressMessage, "Writing Geotags...");
            Task producer = Task.Factory.StartNew(() =>
            {
                int i = 0;
                foreach (var item in photoQueue.GetConsumingEnumerable())
                {
                    try
                    {
                        ThreadInfo threadInfo = new ThreadInfo();
                        Boolean found;
                        string photo = Path.GetFileNameWithoutExtension(item);
                        Record record = null;
                        found = recordDict.TryRemove(photo, out record);
                        //dictCount = photoDict.Count;
                        if (found)
                        {
                            threadInfo.PhotoPath = item;
                            threadInfo.Zip = mZip;
                            threadInfo.OutPath = outPath;
                            threadInfo.Record = record;
                            threadInfo.Photo = photo;
                            Record newRecord = null;
                            newRecord = ProcessFile(threadInfo);
                            Interlocked.Exchange(ref progressRecordDictCount, recordDict.Count);
                        }
                        else
                        {
                            Console.WriteLine("item not found " + item);
                        }
                    }
                    catch (Exception ex)
                    {
                        String s = ex.Message;
                    }
                    Interlocked.Increment(ref i);
                    double newvalue = ((double)i / (double)photoCount) * 100;
                    Interlocked.Exchange(ref progressValue, newvalue);
                    Interlocked.Exchange(ref progressPhotoQueueCount, photoQueue.Count);
                }
                bitmapQueue.CompleteAdding();
                mre.Set();
            });


            Task consumer = Task.Factory.StartNew(() =>
            {
                foreach (var item in bitmapQueue.GetConsumingEnumerable())
                {
                    if (!bitmapQueue.IsCompleted)
                    {
                        mre.WaitOne();
                        if (bitmapQueue.IsAddingCompleted)
                        {
                            if (item != null)
                            {
                                processImage(item);
                            }
                            if (bitmapQueue.Count == 0)
                            {
                                break;
                            }

                        }
                        else
                        {
                            processImage(item);
                            if (bitmapQueue.Count == 0)
                            {
                                mre.Reset();
                            }
                        }
                    }
                }
            });

            await Task.WhenAll(producer, consumer);
            Interlocked.Exchange(ref progressBitmapQueueCount, bitmapQueue.Count);
        }

        /// <summary>
        /// Intialises a new Record and adds data extracted from access to each relevant field.
        /// The record is then added to the Record Dictionary.
        /// </summary>
        /// <param name="row: the access record"></param>
        private async Task<Record> buildRecord(Object[] row)
        {
            Record r = new Record((string)row[1]);
            await Task.Run(() =>
            {
                try
                {
                    int id = (int)row[0];
                    r.Id = id.ToString();
                    r.PhotoRename = Convert.ToString(row[2]);
                    r.Latitude = (double)row[3];
                    r.Longitude = (double)row[4];
                    r.Altitude = (double)row[5];
                    r.Bearing = Convert.ToDouble(row[6]);
                    r.Velocity = Convert.ToDouble(row[7]);
                    r.Satellites = Convert.ToInt32(row[8]);
                    r.PDop = Convert.ToDouble(row[9]);
                    r.Inspector = Convert.ToString(row[10]);
                    r.TimeStamp = Convert.ToDateTime(row[12]);
                    r.GeoMark = Convert.ToBoolean(row[13]);
                    r.Side = Convert.ToString(row[19]);
                    r.Road = Convert.ToInt32(row[20]);
                    r.Carriageway = Convert.ToInt32(row[21]);
                    r.ERP = Convert.ToInt32(row[22]);
                    r.FaultID = Convert.ToInt32(row[23]);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.StackTrace);
                }
            });
            return r;
        }

        private Record ProcessFile(object a)
        {
            ThreadInfo threadInfo = a as ThreadInfo;
            Record r = threadInfo.Record;
            string outPath = threadInfo.OutPath;
            int length = threadInfo.Length;
            string path;
            Bitmap bmp = null;
            PropertyItem[] propItems = null;
            r.GeoTag = true;
            string photoName = r.PhotoName;
            string photoRename = r.PhotoRename;
            r.PhotoName = photoRename; //new photo name          
            path = outPath + "\\" + photoRename + ".jpg";
            string uncPath = GetUNCPath(path);
            r.Path = uncPath;
            threadInfo.OutPath = uncPath;

            try
            {
                if (!threadInfo.Zip)
                {
                    bmp = new Bitmap(threadInfo.PhotoPath);
                    propItems = bmp.PropertyItems;
                    threadInfo.propItemLatRef = bmp.GetPropertyItem(0x0001);
                    threadInfo.propItemLat = bmp.GetPropertyItem(0x0002);
                    threadInfo.propItemLonRef = bmp.GetPropertyItem(0x0003);
                    threadInfo.propItemLon = bmp.GetPropertyItem(0x0004);
                    threadInfo.propItemAltRef = bmp.GetPropertyItem(0x0005);
                    threadInfo.propItemAlt = bmp.GetPropertyItem(0x0006);
                    threadInfo.propItemSat = bmp.GetPropertyItem(0x0008);
                    threadInfo.propItemDir = bmp.GetPropertyItem(0x0011);
                    threadInfo.propItemVel = bmp.GetPropertyItem(0x000D);
                    threadInfo.propItemPDop = bmp.GetPropertyItem(0x000B);
                    threadInfo.propItemDateTime = bmp.GetPropertyItem(0x0132);
                }
                else
                {
                    using (FileStream zipToOpen = new FileStream(threadInfo.PhotoPath, FileMode.Open))
                    {
                        String[] tokens = zipToOpen.Name.Split('\\');
                        string s = tokens[tokens.Length - 1];
                        string key = s.Substring(0, s.Length - 4);
                        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
                        {
                            string entry = threadInfo.Photo + ".jpg";
                            ZipArchiveEntry zip = archive.GetEntry(entry);
                            Stream stream = zip.Open();
                            Image img = Image.FromStream(stream);
                            propItems = img.PropertyItems;
                            threadInfo.propItemLatRef = img.GetPropertyItem(0x0001);
                            threadInfo.propItemLat = img.GetPropertyItem(0x0002);
                            threadInfo.propItemLonRef = img.GetPropertyItem(0x0003);
                            threadInfo.propItemLon = img.GetPropertyItem(0x0004);
                            threadInfo.propItemAltRef = img.GetPropertyItem(0x0005);
                            threadInfo.propItemAlt = img.GetPropertyItem(0x0006);
                            threadInfo.propItemSat = img.GetPropertyItem(0x0008);
                            threadInfo.propItemDir = img.GetPropertyItem(0x0011);
                            threadInfo.propItemVel = img.GetPropertyItem(0x000D);
                            threadInfo.propItemPDop = img.GetPropertyItem(0x000B);
                            threadInfo.propItemDateTime = img.GetPropertyItem(0x0132);
                            bmp = new Bitmap(img);
                            img.Dispose();
                            stream.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                String s = ex.StackTrace;
            }
            object[] o = { threadInfo, bmp };
            bitmapQueue.Add(o);
            Interlocked.Exchange(ref progressBitmapQueueCount, bitmapQueue.Count);
            mre.Set();
            return r;
        }

        private async void processImage(object[] item)
        {
            try
            {
                ThreadInfo threadInfo = item[0] as ThreadInfo;    
                RecordUtil RecordUtil = new RecordUtil(threadInfo.Record);
                PropertyItem propItemLat = RecordUtil.getEXIFCoordinate("latitude", threadInfo.propItemLat);
                PropertyItem propItemLon = RecordUtil.getEXIFCoordinate("longitude", threadInfo.propItemLon);
                PropertyItem propItemAlt = RecordUtil.getEXIFNumber(threadInfo.propItemAlt, "altitude", 10);
                PropertyItem propItemLatRef = RecordUtil.getEXIFCoordinateRef("latitude", threadInfo.propItemLatRef);
                PropertyItem propItemLonRef = RecordUtil.getEXIFCoordinateRef("longitude", threadInfo.propItemLonRef);
                PropertyItem propItemAltRef = RecordUtil.getEXIFAltitudeRef(threadInfo.propItemAltRef);
                PropertyItem propItemDir = RecordUtil.getEXIFNumber(threadInfo.propItemDir, "bearing", 10);
                PropertyItem propItemVel = RecordUtil.getEXIFNumber(threadInfo.propItemVel, "velocity", 100);
                PropertyItem propItemPDop = RecordUtil.getEXIFNumber(threadInfo.propItemPDop, "pdop", 10);
                PropertyItem propItemSat = RecordUtil.getEXIFInt(threadInfo.propItemSat, threadInfo.Record.Satellites);
                PropertyItem propItemDateTime = RecordUtil.getEXIFDateTime(threadInfo.propItemDateTime);
                RecordUtil = null;
                Image image = null;

                try
                {
                    //do image correction
                    //CLAHE correction

                    Bitmap bmp = item[1] as Bitmap;
                    //Image<Bgr, Byte> img = bmp.ToImage<Bgr, byte>();
                    Image<Bgr, Byte> img = new Image<Bgr, Byte>(bmp);
                    Mat src = img.Mat;
                    Image<Bgr, Byte> emguImage = CorrectionUtil.ClaheCorrection(src, 0.5);
                    //arr.Dispose();
                    emguImage = CorrectionUtil.GammaCorrection(emguImage);
                    image = CorrectionUtil.ImageFromEMGUImage(emguImage);
                    emguImage.Dispose();
                }
                catch (Exception ex)
                {
                    String s = ex.StackTrace;
                }
                image.SetPropertyItem(propItemLat);
                image.SetPropertyItem(propItemLon);
                image.SetPropertyItem(propItemLatRef);
                image.SetPropertyItem(propItemLonRef);
                image.SetPropertyItem(propItemAlt);
                image.SetPropertyItem(propItemAltRef);
                image.SetPropertyItem(propItemDir);
                image.SetPropertyItem(propItemVel);
                image.SetPropertyItem(propItemPDop);
                image.SetPropertyItem(propItemSat);
                image.SetPropertyItem(propItemDateTime);
                await saveFile(image, threadInfo.OutPath);

                Interlocked.Increment(ref geotagCount);

                image.Dispose();
                image = null;
            }
            catch (Exception ex)
            {
                String s = ex.StackTrace;
            }
        }

            private async Task saveFile(Image image, string path)
        {
            await Task.Run(() =>
            {
                try
                {
                    image.Save(path);
                }
                catch (Exception ex)
                {
                    String err = ex.StackTrace;
                    Console.WriteLine(err);
                }
            });
        }

        [DllImport("mpr.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int WNetGetConnection([MarshalAs(UnmanagedType.LPTStr)] string localName,
       [MarshalAs(UnmanagedType.LPTStr)] StringBuilder remoteName, ref int length);

        public static string GetUNCPath(string originalPath)
        {
            StringBuilder sb = new StringBuilder(512);
            int size = sb.Capacity;

            if (originalPath.Length > 2 && originalPath[1] == ':')
            {
                char c = originalPath[0];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    int error = WNetGetConnection(originalPath.Substring(0, 2), sb, ref size);
                    if (error == 0)
                    {
                        DirectoryInfo dir = new DirectoryInfo(originalPath);
                        string path = Path.GetFullPath(originalPath).Substring(Path.GetPathRoot(originalPath).Length);
                        return Path.Combine(sb.ToString().TrimEnd(), path);
                    }
                }
            }
            return originalPath;
        }

        public string updateProgessMessage
        {
            get
            {
                return Interlocked.CompareExchange(ref progressMessage, "", "");
            }
        }

        public int updatePhotoCount
        {
            get
            {
                return Interlocked.CompareExchange(ref photoCount, 0, 0);
            }
        }

        public int updateGeoTagCount
        {
            get
            {
                return Interlocked.CompareExchange(ref geotagCount, 0, 0);
            }
        }

        public int updateRecordCount
        {
            get
            {
                return Interlocked.CompareExchange(ref progressRecordCount, 0, 0);
            }
        }

        public int updateRecordDictCount
        {
            get
            {
                return Interlocked.CompareExchange(ref progressRecordDictCount, 0, 0);
            }
        }

        public int updateBitmapQueueCount
        {
            get
            {
                return Interlocked.CompareExchange(ref progressBitmapQueueCount, 0, 0);
            }
        }

        public int updatePhotoQueueCount
        {
            get
            {
                return Interlocked.CompareExchange(ref progressPhotoQueueCount, 0, 0);
            }
        }

        public double updateProgessValue
        {
            get
            {
                return Interlocked.CompareExchange(ref progressValue, 0, 0);
            }
        }
    }
}
