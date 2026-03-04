namespace BulkUploadValidator.Models
{
    public class County
    {
        public int CountyId { get; set; }
        public string? CountyName { get; set; }
        public int CountryId { get; set; }
        public string? CountryName { get; set; }
        public int IsActive { get; set; }
        public int IsDeleted { get; set; }
    }
}
