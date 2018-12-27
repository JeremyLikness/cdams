using Microsoft.WindowsAzure.Storage.Table;

namespace cdamsv2
{
    public class UrlEntity : TableEntity
    {
        public string Url { get; set; }
    }
}
