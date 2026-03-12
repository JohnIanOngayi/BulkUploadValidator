namespace BulkUploadValidator.Services
{
    /// <summary>
    /// Describes the static structure of a bulk-upload Excel template:
    /// headers, which columns get which dropdowns, and how deep the
    /// electoral cascade goes.
    /// </summary>
    public interface ITemplateConfig
    {
        /// <summary>Ordered list of header labels (index 0 = column A).</summary>
        IReadOnlyList<string> Headers { get; }

        /// <summary>1-based column index that receives the "type" dropdown (SiteType / LinkType).</summary>
        IReadOnlyList<int> TypeDropdownColumns { get; }

        /// <summary>
        /// Each entry defines one County→SubCounty→(Constituency→Ward) cascade chain.
        /// The integers are 1-based column indices: [County, SubCounty] or
        /// [County, SubCounty, Constituency, Ward].
        /// </summary>
        IReadOnlyList<IReadOnlyList<int>> ElectoralCascades { get; }

        /// <summary>Named range to use for the type dropdown(s) in this template.</summary>
        string TypeListRangeName { get; }

        /// <summary>SQL to fetch type rows. Must return columns: Id, Name, IsActive.</summary>
        string TypeQuerySql { get; }
    }
}
