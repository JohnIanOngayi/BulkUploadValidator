namespace BulkUploadValidator.Models
{
    public class Ward
    {
        public int WardId { get; set; }
        public string? WardName { get; set; }
        public int CountyId { get; set; }
        public string? CountyName { get; set; }
        public int SubCountyId { get; set; }
        public string? SubCountyName { get; set; }
        public int ConstituencyId { get; set; }
        public string? ConstituencyName { get; set; }
    }
}
