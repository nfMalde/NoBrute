using System;
using System.Collections.Generic;
using System.Text;

namespace NoBrute.Models
{
    [Serializable]
    public class NoBruteEntry
    {
        public string IP { get; set; }
        
        public List<NoBruteRequestItem> Requests { get; set; }

    }
}
