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

        public static string AsPartitionKey(this string code)
        {
            return $"{code.First()}";
        }
    }
}
