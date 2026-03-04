using BulkUploadValidator.Models;

namespace BulkUploadValidator.Repository
{
    public interface ILinkRepository
    {
        Task<List<SubCounty>?> GetAllValidSubCounties(bool cache);
        Task ReadyCache();
        void ValidateSubCounty(string subCountyName, string countyName);
    }
}