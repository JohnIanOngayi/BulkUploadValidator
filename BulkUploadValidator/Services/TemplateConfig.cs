namespace BulkUploadValidator.Services
{
    // -----------------------------------------------------------------------
    // Site  (B=SiteType  |  G County → H SubCounty → I Constituency → J Ward)
    // -----------------------------------------------------------------------
    public class SiteTemplateConfig : ITemplateConfig
    {
        public IReadOnlyList<string> Headers { get; } = new[]
        {
            "Site Name",             // A 1
            "Site Type",             // B 2
            "Location Name",         // C 3
            "GPS Latitude",          // D 4
            "GPS Longitude",         // E 5
            "No. of Internet Users", // F 6
            "County",                // G 7
            "SubCounty",             // H 8
            "Constituency",          // I 9
            "Ward"                   // J 10
        };

        public IReadOnlyList<int> TypeDropdownColumns { get; } = new[] { 2 };   // B

        public IReadOnlyList<IReadOnlyList<int>> ElectoralCascades { get; } = new[]
        {
            new[] { 7, 8, 9, 10 }   // G→H→I→J  (County→SubCounty→Constituency→Ward)
        };

        public string TypeListRangeName => "SiteTypeList";

        public string TypeQuerySql => @"sp_Bulk_Dropdown_SiteTypes";
    }

    // -----------------------------------------------------------------------
    // Link  (B=LinkType  |  F County → G SubCounty  |  K County → L SubCounty)
    // -----------------------------------------------------------------------
    public class LinkTemplateConfig : ITemplateConfig
    {
        public IReadOnlyList<string> Headers { get; } = new[]
        {
            "Link Name",            // A 1
            "Link Type",            // B 2
            "Start Location",       // C 3
            "Start Latitude",       // D 4
            "Start Longitude",      // E 5
            "Start County",         // F 6
            "Start SubCounty",      // G 7
            "End Location",         // H 8
            "End Latitude",         // I 9
            "End Longitude",        // J 10
            "End County",           // K 11
            "End SubCounty",        // L 12
            "Distance"
        };

        public IReadOnlyList<int> TypeDropdownColumns { get; } = new[] { 2 };   // B

        public IReadOnlyList<IReadOnlyList<int>> ElectoralCascades { get; } = new[]
        {
            new[] { 6, 7 },     // F→G  (Start County→SubCounty)
            new[] { 11, 12 }    // K→L  (End   County→SubCounty)
        };

        public string TypeListRangeName => "LinkTypeList";

        public string TypeQuerySql => @"sp_Bulk_Dropdown_LinkTypes";
    }
}
