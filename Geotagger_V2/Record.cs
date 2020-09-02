using System;
using System.Text;

namespace Geotagger_V2
{
    public class Record
    {
        public string id;
        public string photo;
        public string photoRename;
        public double latitude;
        public double longitude;
        public double altitude;
        public double bearing;
        public double velocity;
        public int satellites;
        public double pdop;
        public string inspector;
        public DateTime timestamp;
        public bool geomark;
        public bool geotag;
        public string path;


        public Record()
        {
        }

        public Record(string photo)
        {
            this.photo = photo;
            geotag = false;
        }

        public string PhotoName
        {
            get
            {
                return photo;
            }
            set
            {
                photo = value;
            }
        }

        public string PhotoRename
        {
            get
            {
                return photoRename;
            }
            set
            {
                photoRename = value;
            }
        }

        public string Id
        {
            get
            {
                return id;
            }
            set
            {
                id = value;
            }
        }

        public double Latitude
        {
            get
            {
                return latitude;
            }
            set
            {
                latitude = value;
            }
        }

        public double Longitude
        {
            get
            {
                return longitude;
            }
            set
            {
                longitude = value;
            }
        }

        public double Altitude
        {
            get
            {
                return altitude;
            }
            set
            {
                altitude = value;
            }
        }



        public double Bearing
        {
            get
            {
                return bearing;
            }
            set
            {
                bearing = value;
            }
        }

        public double Velocity
        {
            get
            {
                return velocity;
            }
            set
            {
                velocity = value;
            }
        }

        public int Satellites
        {
            get
            {
                return satellites;
            }
            set
            {
                satellites = value;
            }
        }

        public double PDop
        {
            get
            {
                return pdop;
            }
            set
            {
                pdop = value;
            }
        }

        public String Inspector
        {
            get
            {
                return inspector;
            }
            set
            {
                inspector = value;
            }
        }

        public DateTime TimeStamp
        {
            get
            {
                return timestamp;
            }
            set
            {
                timestamp = value;
            }
        }
        public Boolean GeoMark
        {
            get
            {
                return geomark;
            }
            set
            {
                geomark = value;
            }
        }

        public string ToFullString()
        {
            StringBuilder s = new StringBuilder();
            s.Append(id + ",");
            s.Append(PhotoName + ",");
            s.Append(PhotoRename + ",");
            s.Append(Latitude + ",");
            s.Append(Longitude + ",");
            s.Append(Altitude + ",");
            s.Append(Bearing + ",");
            s.Append(Velocity + ",");
            s.Append(Satellites + ",");
            s.Append(PDop + ",");
            s.Append(Inspector + ",");
            //s.Append(sa + ",");
            s.Append(TimeStamp);


            return s.ToString();
        }

        public override string ToString()
        {

            StringBuilder s = new StringBuilder();
            s.Append(id + ",");
            s.Append(PhotoName + ",");
            s.Append(PhotoRename + ",");
            s.Append(Latitude + ",");
            s.Append(Longitude + ",");
            s.Append(Altitude + ",");
            s.Append(Inspector + ",");
            s.Append(TimeStamp);


            return s.ToString();
        }
    }
}
