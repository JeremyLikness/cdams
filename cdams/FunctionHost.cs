
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

namespace cdams
{
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
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            ShortRequest input = await req.Content.ReadAsAsync<ShortRequest>();

            if (input == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            if (string.IsNullOrWhiteSpace(input.Url))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            if (!Uri.TryCreate(input.Url, UriKind.Absolute, out Uri uri))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var result = new ShortResponse
            {
                Url = input.Url,
                Short_code = string.Empty,
                Error = string.Empty
            };

            try
            {
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
                    Url = input.Url
                };

                keyTable.Id++;
                var operation = TableOperation.Replace(keyTable);
                await tableOut.ExecuteAsync(operation);
                operation = TableOperation.Insert(url);
                await tableOut.ExecuteAsync(operation);

                result.Short_code = code;

                var response = new HttpResponseMessage
                {
                    Content = new StringContent(JsonConvert.SerializeObject(result))
                };

                return response;
            }
            catch(Exception ex)
            {
                log.Error("Error processing link", ex);
                result.Error = ex.Message;
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
                var robotResponse = req.CreateResponse(HttpStatusCode.OK, Utility.ROBOT_RESPONSE, "text/plain");
                return robotResponse;
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
    }
}
