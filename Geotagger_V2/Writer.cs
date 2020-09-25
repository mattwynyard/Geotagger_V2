using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geotagger_V2
{
    class Writer
    {
        private List<Record> _data;
        public Writer()
        {

        }
        public Writer(List<Record> list)
        {
            _data = list;
        }

        public string Path
        {
            get
            {
                return Path;
            }
            set
            {
                Path = value;
            }
        }

        public List<Record> Data
        {
            get
            {
                return _data;
            }
            set
            {
                _data = value;
            }
        }

        public void WriteCSV(string path)
        {

            if (Data != null)
            {
                path += "\\data.csv";
                using (var w = new StreamWriter(path))
                {
                    string header = Record.getHeader();
                    w.WriteLine(header);
                    w.Flush();
                    foreach (var record in Data)
                    {
                        string line = record.ToFullString();
                        w.WriteLine(line);
                        w.Flush();
                    }
                }
            }
        }
    }
}
