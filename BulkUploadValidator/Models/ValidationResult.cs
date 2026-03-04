namespace BulkUploadValidator.Models
{
    public class ValidationResult
    {
        public bool IsSuccess { get; set; }
        public string? Error { get; set; }

        public static ValidationResult Success() => new() { IsSuccess = true };
        public static ValidationResult Failure(string error) => new() { IsSuccess = false, Error = error };
    }
}
