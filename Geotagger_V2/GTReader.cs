using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            recordQueue = new ConcurrentQueue<Record>();
            Task consumer = Task.Factory.StartNew(() =>
            {
                foreach (var item in photoQueue.GetConsumingEnumerable())
                {
                    Bitmap bmp = new Bitmap(item);
                    string photo = Path.GetFileName(item);
                    Record record = new Record(photo);

                    PropertyItem[] propItems = bmp.PropertyItems;
                    PropertyItem propItemLatRef = bmp.GetPropertyItem(0x0001); //PropertyTagGpsLatitudeRef
                    PropertyItem propItemLat = bmp.GetPropertyItem(0x0002); //PropertyTagGpsLatitude
                    PropertyItem propItemLonRef = bmp.GetPropertyItem(0x0003); //PropertyTagGpsLongitudeRef
                    PropertyItem propItemLon = bmp.GetPropertyItem(0x0004); //PropertyTagGpsLongitude
                    PropertyItem propItemAltRef = bmp.GetPropertyItem(0x0005);
                    PropertyItem propItemAlt = bmp.GetPropertyItem(0x0006);
                    PropertyItem propItemSat = bmp.GetPropertyItem(0x0008); //type 2
                    PropertyItem propItemDir = bmp.GetPropertyItem(0x0011); //type 5
                    PropertyItem propItemVel = bmp.GetPropertyItem(0x000D); //type 5
                    PropertyItem propItemPDop = bmp.GetPropertyItem(0x000B); //type 5
                    PropertyItem propItemDateTime = bmp.GetPropertyItem(0x0132);
                    bmp.Dispose();
                    string latitudeRef = ASCIIEncoding.UTF8.GetString(propItemLatRef.Value);
                    string longitudeRef = ASCIIEncoding.UTF8.GetString(propItemLonRef.Value);
                    string altRef = ASCIIEncoding.UTF8.GetString(propItemAltRef.Value);
                    int satellites = Int32.Parse(ASCIIEncoding.UTF8.GetString(propItemSat.Value));
                    double latitude = Utilities.byteToDegrees(propItemLat.Value);
                    double longitude = Utilities.byteToDegrees(propItemLon.Value);
                    double altitude = Utilities.byteToDecimal(propItemAlt.Value);
                    DateTime dateTime = Utilities.byteToDate(propItemDateTime.Value);
                    double direction = Utilities.byteToDecimal(propItemDir.Value);
                    double velocity = Utilities.byteToDecimal(propItemVel.Value);
                    double PDop = Utilities.byteToDecimal(propItemPDop.Value);
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
                    record.Satellites = satellites;
                    record.Bearing = direction;
                    record.Velocity = velocity;
                    record.PDop = PDop;
                    int id = recordQueue.Count + 1;
                    record.Id = id.ToString();
                    recordQueue.Enqueue(record);
                    _recordQueueCount = recordQueue.Count;
                    double newvalue = ((double)recordQueue.Count / (double)_photoCount) * 100;
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
                return Interlocked.CompareExchange(ref _recordQueueCount, 0, 0);
            }
        }
    }
}
