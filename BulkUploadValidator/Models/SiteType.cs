namespace BulkUploadValidator.Models
{
    public class SiteType
    {
        public int SiteTypeId { get; set; }
        public string? SiteTypeName { get; set; }
        public int IsDelete { get; set; }
        public int IsActive { get; set; }
    }
}
