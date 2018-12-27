using Microsoft.WindowsAzure.Storage.Table;

namespace cdamsv2
{
    public class UrlKey : TableEntity
    {
        public long Id { get; set; }
    }
}
