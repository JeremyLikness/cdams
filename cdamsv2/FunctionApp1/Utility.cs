using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace cdamsv2
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
        public const string BASEURL = "cda.ms";
        public const string URL_TRACKING = "cdams";
        public const string URL_STATS = "cdams-stats";
        public const string UTM_MEDIUM = "utm_medium";
        public const string UTM_SOURCE = "utm_source";
        public const string UTM_CAMPAIGN = "utm_campaign";
        public const string WTMCID = "WT.mc_id";
        public const string DEFAULT_ALIAS = "cdams";

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

        public static string AsPage(this Uri uri)
        {
            var pageUrl = new UriBuilder(uri)
            {
                Port = -1
            };
            var parameters = QueryHelpers.ParseNullableQuery(pageUrl.Query);
            foreach (var check in new[] {
                UTM_CAMPAIGN,
                UTM_MEDIUM,
                UTM_SOURCE,
                WTMCID })
            {
                if (parameters.ContainsKey(check) && parameters[check].Any())
                {
                    parameters.Remove(check);
                }
            }
            pageUrl.Query = CreateQueryString(parameters);
            return $"{pageUrl.Host}{pageUrl.Path}{pageUrl.Query}{pageUrl.Fragment}";
        }

        public static string CreateQueryString(Dictionary<string, StringValues> values)
        {
            var baseUrl = "http://test/";
            var baseValues = new Dictionary<string, string>();
            foreach(var key in values.Keys)
            {
                baseValues.Add(key, values[key].First());
            }
            return QueryHelpers.AddQueryString(baseUrl, baseValues).Substring(baseUrl.Length);
        }

        public static AnalyticsEntry ParseQueuePayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new ArgumentNullException("payload");
            }
            var parts = payload.Split('|');
            if (parts.Length != 5)
            {
                throw new ArgumentException($"Bad payload: {payload}");
            }
            var entry = new AnalyticsEntry
            {
                ShortUrl = parts[0].Trim(),
                LongUrl = new Uri(parts[1]),
                TimeStamp = DateTime.Parse(parts[2]),
                Referrer = string.IsNullOrWhiteSpace(parts[3]) ? null : new Uri(parts[3]),
                Agent = parts[4]
            };
            return entry;
        }

        public static Tuple<string, string, string> ExtractCampaignMediumAndAlias(this Uri uri)
        {
            var alias = string.Empty;
            var campaign = string.Empty;
            var medium = string.Empty;
            if (!string.IsNullOrWhiteSpace(uri.Query))
            {
                var queries = QueryHelpers.ParseNullableQuery(uri.Query);
                if (queries.ContainsKey(WTMCID) && queries[WTMCID].Any())
                {
                    var parts = queries[WTMCID].First().Split('-');
                    if (parts.Length == 3)
                    {
                        campaign = parts[0];
                        medium = parts[1];
                        alias = parts[2];
                    }
                }
                else if (queries.ContainsKey(UTM_MEDIUM) && queries[UTM_MEDIUM].Any())
                {
                    medium = queries[UTM_MEDIUM].First();
                    if (queries.ContainsKey(UTM_CAMPAIGN) && queries[UTM_CAMPAIGN].Any())
                    {
                        campaign = queries[UTM_CAMPAIGN].First();
                    }
                    if (queries.ContainsKey(UTM_SOURCE) && queries[UTM_SOURCE].Any())
                    {
                        alias = queries[UTM_SOURCE].First();
                    }
                }
            }
            return Tuple.Create(campaign, medium, alias);
        }
    }
}
