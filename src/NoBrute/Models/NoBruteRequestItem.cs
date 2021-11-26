using System;

namespace NoBrute.Models
{
    [Serializable]
    public class NoBruteRequestItem
    {
        public string RequestName { get; set; }

        public string RequestPath { get; set; }

        public string RequestMethod { get; set; }

        public string RequestQuery { get; set; }

        public int Hitcount { get; set; }

        public DateTime LastHit { get; set; }

        public bool IsExpired(TimeSpan timeToExpire)
        {
            DateTime now = DateTime.Now;

            TimeSpan diff = now - this.LastHit;

            return (diff >= timeToExpire);
        }
    }
}