
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.WindowsAzure.Storage.Table;
using System.Net;
using System;
using System.Web;
using System.Dynamic;
using System.Collections.Generic;

namespace cdams
{
    [Obsolete("Deprecated. Use cdamsv2 instead.")]
    public static class FunctionHost
    {
        [FunctionName("ShortenUrl")]
        public static async Task<HttpResponseMessage> ShortenUrl(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]HttpRequestMessage req,
            [Table(Utility.TABLE, Utility.PARTITIONKEY, Utility.KEY, Take = 1)]UrlKey keyTable,
            [Table(Utility.TABLE)]CloudTable tableOut,
            TraceWriter log)
        {
            log.Info($"C# triggered function called with req: {req}");

            if (req == null)
            {
                return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
            }

            var result = new ShortResponse
            {
                url = string.Empty,
                short_code = string.Empty,
                error = string.Empty
            };

            try
            {
                
                ShortRequest input = await req.Content.ReadAsAsync<ShortRequest>();

                if (input == null)
                {
                    log.Error("Input payload is null.");
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
                }

                if (string.IsNullOrWhiteSpace(input.url))
                {
                    log.Error("No URL found.");
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest };                   
                }

                if (!Uri.TryCreate(input.url, UriKind.Absolute, out Uri uri))
                {
                    log.Error($"Input URL ({input.url}) is invalid format.");
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                if (keyTable == null)
                {
                    keyTable = new UrlKey
                    {
                        PartitionKey = Utility.PARTITIONKEY,
                        RowKey = Utility.KEY,
                        Id = 1
                    };
                    var addKey = TableOperation.Insert(keyTable);
                    await tableOut.ExecuteAsync(addKey);
                }

                var idx = keyTable.Id;
                var code = Utility.Encode(idx);

                var url = new UrlEntity
                {
                    PartitionKey = $"{code[0]}",
                    RowKey = code,
                    Url = input.url
                };

                log.Info($"ShortCode={code} for URL {url.Url}");
                result.url = $"https://{Utility.BASEURL}/{code}";

                keyTable.Id++;
                var operation = TableOperation.Replace(keyTable);
                await tableOut.ExecuteAsync(operation);
                operation = TableOperation.Insert(url);
                await tableOut.ExecuteAsync(operation);

                result.short_code = code;

                var response = new HttpResponseMessage
                {
                    Content = new StringContent(JsonConvert.SerializeObject(result))
                };

                return response;
            }
            catch (Exception ex)
            {
                log.Error("Error processing link", ex);
                result.error = ex.Message;
                var response = new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Conflict,
                    Content = new StringContent(JsonConvert.SerializeObject(result))
                };
                return response;
            }
        }

        [FunctionName(name: "UrlRedirect")]
        public static async Task<System.Net.Http.HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post",
            Route = "UrlRedirect/{shortUrl}")]HttpRequestMessage req,
            [Table(Utility.TABLE)]CloudTable inputTable,
            string shortUrl,
            [Queue(queueName: Utility.QUEUE)]IAsyncCollector<string> queue,
            TraceWriter log)
        {
            log.Info($"C# HTTP trigger function processed a request for shortUrl {shortUrl}");

            if (shortUrl.ToLower() == Utility.ROBOTS)
            {
                log.Info("Request for robots.txt.");
                return new HttpResponseMessage
                {
                    Content = new StringContent(Utility.ROBOT_RESPONSE),
                    StatusCode = HttpStatusCode.OK
                };
            }

            var redirectUrl = Utility.FALLBACKURL;

            if (!String.IsNullOrWhiteSpace(shortUrl))
            {
                shortUrl = shortUrl.Trim();

                var partitionKey = shortUrl.AsPartitionKey();

                log.Info($"Searching for partition key {partitionKey} and row {shortUrl}.");

                TableResult result = null;

                TableOperation operation = TableOperation.Retrieve<ShortUrl>(partitionKey, shortUrl);
                result = await inputTable.ExecuteAsync(operation);
                if (result.Result is ShortUrl fullUrl)
                {
                    log.Info($"Found it: {fullUrl.Url}");
                    redirectUrl = WebUtility.UrlDecode(fullUrl.Url);
                }
                var referrer = string.Empty;
                if (req.Headers.Referrer != null)
                {
                    log.Info($"Referrer: {req.Headers.Referrer.ToString()}");
                    referrer = req.Headers.Referrer.ToString();
                }
                log.Info($"User agent: {req.Headers.UserAgent.ToString()}");
                await queue.AddAsync($"{shortUrl}|{redirectUrl}|{DateTime.UtcNow}|{referrer}|{req.Headers.UserAgent.ToString().Replace('|', '^')}");
            }
            else
            {
                log.Warning("Bad Link, resorting to fallback.");
            }

            var res = req.CreateResponse(HttpStatusCode.Redirect);
            res.Headers.Add("Location", redirectUrl);
            return res;
        }

        [FunctionName("KeepAlive")]
        public static void KeepAlive([TimerTrigger(scheduleExpression: "0 */4 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info("Keep-Alive invoked.");
        }

        [FunctionName("ProcessQueue")]
        public static void ProcessQueue([QueueTrigger(queueName: Utility.QUEUE)]string request,
           [CosmosDB(Utility.URL_TRACKING, Utility.URL_STATS, CreateIfNotExists = true, ConnectionStringSetting = "CosmosDb")]out dynamic doc,
           TraceWriter log)
        {
            try
            {
                AnalyticsEntry parsed = Utility.ParseQueuePayload(request);
                var page = parsed.LongUrl.AsPage(HttpUtility.ParseQueryString);

                var analytics = parsed.LongUrl.ExtractCampaignMediumAndAlias(HttpUtility.ParseQueryString);
                var campaign = analytics.Item1;
                var medium = analytics.Item2;
                var alias = analytics.Item3;
                if (string.IsNullOrWhiteSpace(alias))
                {
                    alias = Utility.DEFAULT_ALIAS;
                }

                // cosmos DB 
                var normalize = new[] { '/' };
                doc = new ExpandoObject();
                doc.id = Guid.NewGuid().ToString();
                doc.page = page.TrimEnd(normalize);
                doc.alias = alias;
                if (!string.IsNullOrWhiteSpace(parsed.ShortUrl))
                {
                    doc.shortUrl = parsed.ShortUrl;
                }
                if (!string.IsNullOrWhiteSpace(campaign))
                {
                    doc.campaign = campaign;
                }
                if (parsed.Referrer != null)
                {
                    doc.referrerUrl = parsed.Referrer.AsPage(HttpUtility.ParseQueryString);
                    doc.referrerHost = parsed.Referrer.DnsSafeHost;
                }
                if (!string.IsNullOrWhiteSpace(parsed.Agent))
                {
                    doc.agent = parsed.Agent;
                }
                doc.count = 1;
                doc.timestamp = parsed.TimeStamp;
                doc.host = parsed.LongUrl.DnsSafeHost;
                if (!string.IsNullOrWhiteSpace(medium))
                {
                    ((IDictionary<string, object>)doc).Add(medium, 1);
                }
                log.Info($"CosmosDB: {doc.id}|{doc.page}|{parsed.ShortUrl}|{campaign}|{medium}");
            }
            catch (Exception ex)
            {
                log.Error("An unexpected error occurred", ex);
                throw;
            }
        }
    }
}
