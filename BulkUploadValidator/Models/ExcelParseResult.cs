namespace BulkUploadValidator.Models
{
    public class ExcelParseResult<T>
    {
        public List<T> ValidItems { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public bool HasErrors => Errors.Any();
    }
}
