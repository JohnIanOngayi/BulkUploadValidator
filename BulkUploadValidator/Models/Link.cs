namespace BulkUploadValidator.Models
{
    public class Link
    {
        public int LinkId { get; set; }
        public string? LinkName { get; set; }
        public int LinkTypeId { get; set; }
        public string? LinkTypeName { get; set; }
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }
        public double StartPointLatitude { get; set; }
        public double StartPointLongitude { get; set; }
        public double EndPointLatitude { get; set; }
        public double EndPointLongitude { get; set; }
        public int StartCountyId { get; set; }
        public string? StartCountyName { get; set; }
        public int StartSubCountyId { get; set; }
        public string? StartSubCountyName { get; set; }
        public int EndCountyId { get; set; }
        public string? EndCountyName { get; set; }
        public int EndSubCountyId { get; set; }
        public string? EndSubCountyName { get; set; }
        public int IsDeleted { get; set; }
        public int IsActive { get; set; }
    }
}
