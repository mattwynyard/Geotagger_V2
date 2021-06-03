using System;
using System.Drawing.Imaging;
using System.Text;


namespace Geotagger_V2
{
    class RecordUtil// : Record
    {
        Record record;

        public RecordUtil()
        {
        }

        public RecordUtil(Record record)
        {
            this.record = record;
        }

        public PropertyItem getEXIFNumber(PropertyItem item, String type, int precision)
        {
            int value = 0;
            int multiplier = precision;
            if (type.Equals("altitude"))
            {
                value = (int)Math.Round(Math.Abs(record.Altitude) * multiplier);
            }
            else if (type.Equals("bearing"))
            {
                value = (int)Math.Round(Math.Abs(record.Bearing) * multiplier);
            }
            else if (type.Equals("velocity"))
            {
                value = (int)Math.Round(Math.Abs(record.Velocity) * multiplier);
            }
            else if (type.Equals("pdop"))
            {
                value = (int)Math.Round(Math.Abs(record.PDop) * multiplier);
            }
            int[] values = { value, multiplier };

            byte[] byteArray = new byte[8];
            int offset = 0;
            foreach (var x in values)
            {
                BitConverter.GetBytes(x).CopyTo(byteArray, offset);
                offset += 4;
            }
            if (item != null)
            {
                item.Value = byteArray;
            }
            
            return item;
        }

        public PropertyItem getEXIFInt(PropertyItem item, int number)
        {
            int value = number;
            if (item != null)
            {
                item.Value = ASCIIEncoding.ASCII.GetBytes(value.ToString() + "\0");
                item.Type = 2;
            }
            return item;
        }

        public PropertyItem getEXIFAltitudeRef(PropertyItem item)
        {
            int value;
            if (record.Altitude < 0)
            {
                value = 0;
            }
            else
            {
                value = 1;
            }
            int[] values = { value };
            byte[] byteArray = new byte[4];
            BitConverter.GetBytes(values[0]).CopyTo(byteArray, 0);
            if (item != null)
            {
                item.Value = byteArray;
            }
            return item;
        }

        public PropertyItem getEXIFDateTime(PropertyItem item)
        {
            DateTime date = Convert.ToDateTime(record.TimeStamp.ToString());
            string dateTime = date.ToString("yyyy:MM:dd HH:mm:ss") + "\0";
            byte[] bytes = Encoding.ASCII.GetBytes(dateTime);
            if (item != null)
            {
                item.Value = bytes;
            }
            return item;
        }

        public PropertyItem getEXIFCoordinate(String coordinate, PropertyItem item)
        {
            double coord = 0;
            int multiplier = 10000;
            if (coordinate.Equals("latitude"))
            {
                coord = Math.Abs(record.Latitude);
            }
            else
            {
                coord = Math.Abs(record.Longitude);
            }

            int d = (int)coord;
            coord -= d;
            coord *= 60;
            int m = (int)coord;
            coord -= m;
            coord *= 60;
            int s = (int)Math.Round(coord * multiplier);

            int[] values = { d, 1, m, 1, s, multiplier };

            byte[] byteArray = new byte[24];
            int offset = 0;
            foreach (var value in values)
            {
                BitConverter.GetBytes(value).CopyTo(byteArray, offset);
                offset += 4;
            }
            if (item != null)
            {
                item.Type = 5;
                item.Value = byteArray; //write bytes
            }
            return item;
        }

        public PropertyItem getEXIFCoordinateRef(String coordinate, PropertyItem item)
        {
            if (item != null) {
                if (coordinate.Equals("latitude"))
                {
                    if (record.Latitude < 0)
                    {
                        item.Value = ASCIIEncoding.ASCII.GetBytes("S\0");
                    }
                    else
                    {
                        item.Value = ASCIIEncoding.ASCII.GetBytes("N\0");
                    }
                }
                else
                {
                    if (record.Longitude < 0)
                    {
                        item.Value = ASCIIEncoding.ASCII.GetBytes("W\0");
                    }
                    else
                    {
                        item.Value = ASCIIEncoding.ASCII.GetBytes("E\0");
                    }
                }
                return item;
            } else
            {
                return null;
            } 
        }
    }
}

