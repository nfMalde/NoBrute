using System;
using System.Collections.Generic;

namespace NoBrute.Models
{
    [Serializable]
    public class NoBruteEntry
    {
        public string IP { get; set; }

        public List<NoBruteRequestItem> Requests { get; set; }
    }
}