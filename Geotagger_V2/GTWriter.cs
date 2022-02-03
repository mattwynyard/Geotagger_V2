using System;
using System.Collections.Concurrent;
using System.Data.OleDb;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Structure;


namespace Geotagger_V2
{
    public class GTWriter : GeotagManger
    {
        private int _progressRecordCount;
        private int _progressRecordDictCount;
        private int _progressBitmapQueueCount;
        private int _progressRecordDictErrors;
        private int _photosNoRecordCount;
        private int _geotagCount;
        private int _photoNameError;
        private int _bitmapError = 0;
        private static int _bitmapQueueSize;
        private BlockingCollection<object[]> bitmapQueue;
        private static ManualResetEvent mre = new ManualResetEvent(false);
        private static GTWriter _instance;

        protected GTWriter(int sizeBitmapQueue = 50) 
        {
            _bitmapQueueSize = sizeBitmapQueue;
        }

        public static GTWriter Instance(int sizeBitmapQueue = 50)
        {
            if (_instance == null)
            {
                _instance = new GTWriter();
                _bitmapQueueSize = sizeBitmapQueue;

            }

            return _instance;
        }

        public ConcurrentDictionary<string, Record> RecordDict
        {
            get;
            private set;
        }

        public int getGeotagCount()
        {
            return _geotagCount;
        }

        public void Initialise()
        {
            RecordDict = new ConcurrentDictionary<string, Record>(); ;
            photoQueue = new BlockingCollection<string>();
            bitmapQueue = new BlockingCollection<object[]>(_bitmapQueueSize);
            _geotagCount = 0;
            _progressMessage = "";
            _progressValue = 0;
            _progressRecordCount = 0;
            _progressRecordDictCount = 0;
            _progressPhotoQueueCount = 0;
            _progressBitmapQueueCount = 0;
            _progressRecordDictErrors = 0;
            _photoCount = 0;
            _photosNoRecordCount = 0;
            _photoNameError = 0;

        }

        /// <summary>
        /// Establishes connection to access database. Selects all geomarked records using OleDbDataReader.
        /// Tdds records to queue ready for processing.
        /// 
        /// </summary>
        /// <param name="dbPath">path of the access database</param>
        /// <param name="inspector">filter query on inspector intials</param>
        /// <returns></returns>
        public async Task<TaskStatus> readDatabase(string dbPath, string inspector)
        {
            RecordDict = new ConcurrentDictionary<string, Record>();
            Interlocked.Exchange(ref _progressMessage, "Reading database...");
            Interlocked.Exchange(ref _progressValue, 0);
            string _inspector = Utilities.getInspector(inspector);
            string connectionString = string.Format("Provider={0}; Data Source={1}; Jet OLEDB:Engine Type={2}",
                "Microsoft.Jet.OLEDB.4.0", dbPath, 5);
            OleDbConnection connection = new OleDbConnection(connectionString);
            string strSQL;
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
            Interlocked.Exchange(ref _progressRecordCount, recordCount);
            commandLength.Dispose();
            
            Task taskreader = Task.Factory.StartNew(() =>
            {
                int i = 0;
                int errors = 0;
                Interlocked.Exchange(ref _progressMessage, "Building Record Dictionary...");
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
                            bool success = RecordDict.TryAdd(r.PhotoName, r);
                            if (success)
                            {
                                Interlocked.Increment(ref i);
                                double newvalue = ((double)i / (double)recordCount) * 100;
                                Interlocked.Exchange(ref _progressValue, newvalue);
                                Interlocked.Exchange(ref _progressRecordDictCount, RecordDict.Count);
                            } else
                            {
                                Interlocked.Increment(ref errors);
                                Interlocked.Exchange(ref _progressRecordDictErrors, errors);

                            }
                        //TODO add to error dictionary
                        } catch (ArgumentNullException ex)
                        {
                            Console.WriteLine(ex.StackTrace);
                        } catch (OverflowException ex)
                        {
                            Console.WriteLine(ex.StackTrace);
                        }
                    }
                    reader.Close();
                    command.Dispose();
                }
                double finalvalue = ((double)i / (double)recordCount) * 100;
                Interlocked.Exchange(ref _progressValue, finalvalue);
                Interlocked.Exchange(ref _progressRecordDictCount, RecordDict.Count);
            });
            await Task.WhenAll(taskreader);
            try
            {
                connection.Close();
            } catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
            return taskreader.Status;
            
        }

        public async Task<TaskStatus> writeGeotag(string outPath)
        {
            Interlocked.Exchange(ref _progressValue, 0);
            Interlocked.Exchange(ref _progressMessage, "Writing Geotags...");
            Task producer = Task.Factory.StartNew(() =>
            {
                int i = 0;
                foreach (var item in photoQueue.GetConsumingEnumerable())
                {
                    try
                    {
                        ThreadInfo threadInfo = new ThreadInfo();
                        bool found;
                        string photo = Path.GetFileNameWithoutExtension(item);
                        Record record = null;
                        found = RecordDict.TryRemove(photo, out record);
                        if (found)
                        {
                            if (record.PhotoRename == null || record.PhotoRename == "") //empty file rename
                            {
                                Interlocked.Increment(ref _photoNameError);

                            } else
                            {
                                threadInfo.PhotoPath = item;
                                threadInfo.Zip = mZip;
                                threadInfo.OutPath = outPath;
                                threadInfo.Record = record;
                                threadInfo.Photo = photo;
                                Record newRecord = null;
                                newRecord = ProcessFile(threadInfo);
                                Interlocked.Exchange(ref _progressRecordDictCount, RecordDict.Count);
                            }
                            
                        }
                        else
                        {
                            Interlocked.Increment(ref _photosNoRecordCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                    Interlocked.Increment(ref i);
                    double newvalue = ((double)i / (double)_photoCount) * 100;
                    Interlocked.Exchange(ref _progressValue, newvalue);
                    Interlocked.Exchange(ref _progressPhotoQueueCount, photoQueue.Count);
                }
                bitmapQueue.CompleteAdding();
                Interlocked.Exchange(ref _progressMessage, "Cleaning up....");
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
                                Interlocked.Exchange(ref _progressMessage, "Finished!");
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
            Interlocked.Exchange(ref _progressBitmapQueueCount, bitmapQueue.Count);
            Interlocked.Exchange(ref _progressMessage, "Finished!");
            return consumer.Status;
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
                    //r.ERP = Convert.ToInt32(row[22]);
                    //r.FaultID = Convert.ToInt32(row[23]);     
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
            if (o[1] != null)
            {
                bitmapQueue.Add(o);
            } else
            {
                _bitmapError++;
            }
                
            Interlocked.Exchange(ref _progressBitmapQueueCount, bitmapQueue.Count);
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
                
                try
                {
                    //do image correction
                    //CLAHE correction
                    Bitmap bmp = item[1] as Bitmap;
                    //Image<Bgr, Byte> img = bmp.ToImage<Bgr, byte>(); Used for EMGUCV 4.3 error when using this version rolled back to 4.1
                    Image<Bgr, Byte> img = new Image<Bgr, Byte>(bmp);
                    Mat src = img.Mat;
                    Image<Bgr, Byte> emguImage = CorrectionUtil.ClaheCorrection(src, 0.5);
                    emguImage = CorrectionUtil.GammaCorrection(emguImage);
                    Image image = CorrectionUtil.ImageFromEMGUImage(emguImage);
                    emguImage.Dispose();
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
                    Interlocked.Increment(ref _geotagCount);
                    Interlocked.Exchange(ref _progressBitmapQueueCount, bitmapQueue.Count);
                    image.Dispose();
                    image = null;
                }
                catch (Exception ex)
                {
                    string s = ex.StackTrace;
                }
                
            }
            catch (Exception ex)
            {
                string s = ex.StackTrace;
            }
        }



        private async Task saveFile(Image image, string path)
        {
            Console.WriteLine("start");
            await Task.Run(() =>
            {
                try
                {
                    image.Save(path);
                    Console.WriteLine("saved");

                }
                catch (Exception ex)
                {
                    string err = ex.StackTrace;
                    Console.WriteLine(err);
                }
            });
            Console.WriteLine("end");
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

        public int updateGeoTagCount
        {
            get
            {
                return Interlocked.CompareExchange(ref _geotagCount, 0, 0);
            }
        }

        public int updateRecordCount
        {
            get
            {
                return Interlocked.CompareExchange(ref _progressRecordCount, 0, 0);
            }
        }

        public int updateRecordDictCount
        {
            get
            {
                return Interlocked.CompareExchange(ref _progressRecordDictCount, 0, 0);
            }
        }

        public int updateBitmapQueueCount
        {
            get
            {
                return Interlocked.CompareExchange(ref _progressBitmapQueueCount, 0, 0);
            }
        }

        public int updateNoRecordCount
        {
            get
            {
                return Interlocked.CompareExchange(ref _photosNoRecordCount, 0, 0);
            }
        }

        public int updatePhotoNameError
        {
            get
            {
                return Interlocked.CompareExchange(ref _photoNameError, 0, 0);
            }
        }

        public int updateDuplicateCount
        {
            get
            {
                return Interlocked.CompareExchange(ref _progressRecordDictErrors, 0, 0);
            }
        }
    }
}
