namespace BulkUploadValidator.Dtos
{
    public class SiteCreateDto
    {
        public string? SiteName { get; set; }
        public string? SiteType { get; set; }
        public string? LocationName { get; set; }

        public double GPSLatitude { get; set; }
        public double GPSLongitude { get; set; }

        public int NoOfInternetUsers { get; set; }

        public string? CountyName { get; set; }
        public string? SubCountyName { get; set; }
        public string? ConstituencyName { get; set; }
        public string? WardName { get; set; }
    }
}
