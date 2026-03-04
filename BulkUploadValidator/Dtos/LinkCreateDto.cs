namespace BulkUploadValidator.Dtos
{
    public class LinkCreateDto
    {
        public string? LinkName { get; set; }
        public string? LinkType { get; set; }

        public string? StartLocation { get; set; }
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public string? StartSubCountyName { get; set; }
        public string? StartCountyName { get; set; }

        public string? EndLocation { get; set; }
        public double EndLatitude { get; set; }
        public double EndLongitude { get; set; }
        public string? EndSubCountyName { get; set; }
        public string? EndCountyName { get; set; }
    }
}
