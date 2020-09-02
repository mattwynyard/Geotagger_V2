using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geotagger_V2
{
    public class ProgressObject
    {
        public ProgressObject()
        {

        }

        public double Value { get; set; }

        public string Message { get; set; }

        public string PhotoCount { get; set; }
    }
}
