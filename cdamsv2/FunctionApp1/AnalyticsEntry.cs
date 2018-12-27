using System;

namespace cdamsv2
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
