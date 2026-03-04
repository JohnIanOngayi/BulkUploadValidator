namespace BulkUploadValidator.Models
{
    public class Site
    {
        public int SiteId { get; set; }
        public int SiteName { get; set; }
        public int SiteTypeId { get; set; }
        public string? SiteTypeName { get; set; }
        public string? Location { get; set; }
        public double GPSLatitude { get; set; }
        public double GPSLongitude { get; set; }
        public int CountyId { get; set; }
        public string? CountyName { get; set; }
        public int SubCountyId { get; set; }
        public string? SubCountyName { get; set; }
        public int ConstituencyId { get; set; }
        public string? ConstituencyName { get; set; }
        public int WardId { get; set; }
        public string? WardName { get; set; }
        public int? NoOfInternetUsers { get; set; }
        public int IsDeleted { get; set; }
        public int IsActive { get; set; }
    }
}
