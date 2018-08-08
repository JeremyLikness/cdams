using Microsoft.WindowsAzure.Storage.Table;

namespace cdams
{
    public class ShortUrl : TableEntity 
    {
        public string Url { get; set; }
    }
}
