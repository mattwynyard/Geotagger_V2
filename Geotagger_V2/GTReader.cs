using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Geotagger_V2
{

    class GTReader : GeotagManger
    {
        private static GTReader _instance;
        private ConcurrentQueue<Record> recordQueue;
        private int _recordQueueCount;
        private int errors;
        private int _photoCountTotal;

        protected GTReader()
        {

        }

        public static GTReader Instance()
        {
            if (_instance == null)
            {
                _instance = new GTReader();
            }
            return _instance;
        }

        public ConcurrentQueue<Record> Queue
        {
            get
            {
                return recordQueue;
            }
        }


        public async Task<TaskStatus> readGeotag()
        {
            Interlocked.Exchange(ref _progressValue, 0);
            Interlocked.Exchange(ref _progressMessage, "Reading Geotags...");
            _recordQueueCount = 0;
            errors = 0;
            _photoCountTotal = 0;
            recordQueue = new ConcurrentQueue<Record>();
            Task consumer = Task.Factory.StartNew(() =>
            {
                
                foreach (var item in photoQueue.GetConsumingEnumerable())
                {
                    Bitmap bmp = null;
                    try
                    {
                        bmp = new Bitmap(item);
                        string photo = Path.GetFileName(item);
                        Record record = new Record(photo);
                        PropertyItem[] propItems = bmp.PropertyItems;
                        PropertyItem propItemLatRef = bmp.GetPropertyItem(0x0001); //type2
                        PropertyItem propItemLat = bmp.GetPropertyItem(0x0002); //PropertyTagGpsLatitude
                        PropertyItem propItemLonRef = bmp.GetPropertyItem(0x0003); //type2
                        PropertyItem propItemLon = bmp.GetPropertyItem(0x0004); //PropertyTagGpsLongitude
                        PropertyItem propItemAltRef = bmp.GetPropertyItem(0x0005);
                        PropertyItem propItemAlt = bmp.GetPropertyItem(0x0006);
                        PropertyItem propItemGPSTime = bmp.GetPropertyItem(0x0007); //type5
                        PropertyItem propItemSat = bmp.GetPropertyItem(0x0008); //type 2
                        PropertyItem propItemDir = bmp.GetPropertyItem(0x0011); //type 5
                        PropertyItem propItemGpsDate = bmp.GetPropertyItem(0x001D); //type 5
                        PropertyItem propItemVel = bmp.GetPropertyItem(0x000D); //type 5
                        PropertyItem propItemPDop = bmp.GetPropertyItem(0x000B); //type 5
                        PropertyItem propItemDateTime = bmp.GetPropertyItem(0x0132); //type2
                        bmp.Dispose();
                        try
                        {
                            string latitudeRef = ASCIIEncoding.UTF8.GetString(propItemLatRef.Value); //type2
                            string longitudeRef = ASCIIEncoding.UTF8.GetString(propItemLonRef.Value); //type2
                            string altRef = ASCIIEncoding.UTF8.GetString(propItemAltRef.Value);
                            int satellites = Int32.Parse(ASCIIEncoding.UTF8.GetString(propItemSat.Value));
                            double latitude = Utilities.byteToDegrees(propItemLat.Value); //type5
                            double longitude = Utilities.byteToDegrees(propItemLon.Value); //type5
                            double altitude = Utilities.byteToDecimal(propItemAlt.Value); //type5
                            double direction = Utilities.byteToDecimal(propItemDir.Value); //type 5
                            double velocity = Utilities.byteToDecimal(propItemVel.Value);  //type 5
                            double PDop = Utilities.byteToDecimal(propItemPDop.Value); //type 5                      
                            DateTime dateTime = Utilities.byteToDate(propItemDateTime.Value);
                            string gpsDate = ASCIIEncoding.UTF8.GetString(propItemGpsDate.Value);
                            DateTime gpsTime = Utilities.byteToDate(propItemGPSTime.Value, gpsDate);
                            if (latitudeRef.Equals("S\0"))
                            {
                                latitude = -latitude;
                            }
                            if (longitudeRef.Equals("W\0"))
                            {
                                longitude = -longitude;
                            }
                            if (!altRef.Equals("\0"))
                            {
                                altitude = -altitude;
                            }
                            record.Latitude = latitude;
                            record.Longitude = longitude;
                            record.Altitude = altitude;
                            record.TimeStamp = dateTime;
                            record.GPSTimeStamp = gpsTime;
                            record.Satellites = satellites;
                            record.Bearing = direction;
                            record.Velocity = velocity;
                            record.PDop = PDop;
                            int id = recordQueue.Count + 1;
                            record.Id = id.ToString();
                            recordQueue.Enqueue(record);
                            _recordQueueCount = recordQueue.Count;
                        }
                        catch (FormatException ex)
                        {
                            Interlocked.Increment(ref errors);
                            Console.WriteLine(ex.StackTrace);
                        }
                    } catch (Exception ex)
                    {
                        Interlocked.Increment(ref errors);
                        Console.WriteLine(ex.StackTrace);
                    }
                    Interlocked.Increment(ref _photoCountTotal);
                    double newvalue = (((double)recordQueue.Count + errors) / (double)_photoCount) * 100;
                    Interlocked.Exchange(ref _progressValue, newvalue);

                }
            });
            await Task.WhenAll(consumer);
            Interlocked.Exchange(ref _progressMessage, "Finished!");
            return consumer.Status;
        }

        public int updateRecordQueueCount
        {
            get
            {
                return Interlocked.CompareExchange(ref _photoCountTotal, 0, 0);
            }
        }

        public int updateErrorCount
        {
            get
            {
                return Interlocked.CompareExchange(ref errors, 0, 0);
            }
        }
    }
}
