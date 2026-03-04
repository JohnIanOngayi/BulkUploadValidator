namespace BulkUploadValidator.Models
{
    public class Constituency
    {
        public int ConstituencyId { get; set; }
        public string? ConstituencyName { get; set; }
        public int SubCountyId { get; set; }
        public string? SubCountyName { get; set; }
        public int IsActive { get; set; }
        public int IsDeleted { get; set; }
    }
}
