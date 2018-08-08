using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace cdams
{
    public static class Utility
    {
        public const string ROBOTS = "robots.txt";
        public const string ROBOT_RESPONSE = "user-agent: *\ndisallow: /";
        public const string FALLBACKURL = "https://developer.microsoft.com/advocates";
        public const string TABLE = "cdams";
        public const string QUEUE = "redirects";
        public const string PARTITIONKEY = "0";
        public const string KEY = "KEY";
        public const string BASE = "0123456789bcdfghjkmnpqrstvwxyzBCDFGHJKLMNPQRSTVWXYZ";

        public static string AsPartitionKey(this string code)
        {
            return $"{code.First()}";
        }

        public static string Encode(long id)
        {
            var s = string.Empty;
            while (id > 0)
            {
                var idx = ((float)id) % BASE.Length;
                s = $"{BASE[(int)idx]}{s}";
                id = (long)Math.Floor((float)id / BASE.Length);
            }
            return s;
        }
    }
}
