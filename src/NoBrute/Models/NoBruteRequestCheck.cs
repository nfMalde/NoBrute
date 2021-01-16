using System;
using System.Collections.Generic;
using System.Text;

namespace NoBrute.Models
{
    public class NoBruteRequestCheck
    {
        public bool IsGreenRequest { get; set; }

        public int AppendRequestTime { get; set; }

        public string RemoteAddr { get; set; }

        public int RequestNum { get; set; }

        public DateTime ResetTime { get; set; }
    }
}
