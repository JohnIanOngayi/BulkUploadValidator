namespace BulkUploadValidator.Models
{
    public class SubCounty
    {
        public int SubCountyId { get; set; }
        public string? SubCountyName { get; set; }
        public int CountyId { get; set; }
        public string? CountyName { get; set; }
    }
}
