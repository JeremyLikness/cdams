using Microsoft.WindowsAzure.Storage.Table;

namespace cdams
{
    public class UrlKey : TableEntity
    {
        public long Id { get; set; }
    }
}
