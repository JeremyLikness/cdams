using Microsoft.WindowsAzure.Storage.Table;

namespace cdams
{
    public class UrlEntity : TableEntity
    {
        public string Url { get; set; }
    }
}
