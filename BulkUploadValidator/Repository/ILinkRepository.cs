using BulkUploadValidator.Dtos;
using BulkUploadValidator.Models;

namespace BulkUploadValidator.Repository
{
    public interface ILinkRepository
    {
        Task<List<SubCounty>?> GetAllValidSubCounties(bool cache);
        Task<List<LinkType>?> GetAllValidLinkTypes(bool cache);
        Task ReadyCache();
        void ValidateSubCounty(string subCountyName, string countyName);
        ValidationResult ValidateLinkDto(LinkCreateDto linkCreateDto);
    }
}