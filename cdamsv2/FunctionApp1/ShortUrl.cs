using Microsoft.WindowsAzure.Storage.Table;

namespace cdamsv2
{
    public class ShortUrl : TableEntity 
    {
        public string Url { get; set; }
    }
}
