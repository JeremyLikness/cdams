using System;
using System.Collections.Generic;
using System.Text;

namespace cdams
{
    public class AnalyticsEntry
    {
        public string ShortUrl { get; set; }
        public Uri LongUrl { get; set; }
        public DateTime TimeStamp { get; set; }
        public Uri Referrer { get; set; }
        public string Agent { get; set; }
    }
}
