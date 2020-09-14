using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geotagger_V2
{
    
    class GTReader : GeotagManger
    {
        private static GTReader _instance;

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
    }
}
