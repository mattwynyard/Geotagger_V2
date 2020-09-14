using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geotagger_V2
{
    class LogWriter
    {
        private GTWriter _manager;
        private List<string> lines;
        public LogWriter()
        {

        }

        public LogWriter(GTWriter manager)
        {
            _manager = manager;
            lines = new List<string>();
        }

        public void Write(string path, string time)
        {
            //noPhoto = report.NoPhotoDictionary.ToDictionary(noPhoto => noPhoto.Key, noPhoto => noPhoto.Value as object);
            Dictionary<string, object>  Records = _manager.RecordDict.ToDictionary(noRecord => noRecord.Key, noRecord => noRecord.Value as object);

            lines.Add(_manager.getGeotagCount() + " photos geotagged.\n");
            lines.Add("Time taken: " + time + "\n\n");
            lines.Add("Records with no photo");
            WriteDictionary(Records, "string");
            string _path = Path.GetDirectoryName(path) + "\\log.txt";
            _Save(_path, lines.ToArray());
        }

        private void WriteDictionary(Dictionary<string, object> dict, String type)
        {

            foreach (var item in dict)
            {
                if (type == "record")
                {
                    Record r = item.Value as Record;
                    lines.Add(r.ToString());
                }
                else if (type == "exception")
                {
                    Exception r = item.Value as Exception;
                    lines.Add(r.ToString());
                }
                else
                {
                    lines.Add(item.Value as string);
                }
            }
        }

        private void _Save(string path, string[] lines)
        {
            File.WriteAllLines(path, lines);
        }
    }
}
