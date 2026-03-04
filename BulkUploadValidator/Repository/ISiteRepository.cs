using BulkUploadValidator.Dtos;
using BulkUploadValidator.Models;

namespace BulkUploadValidator.Repository
{
    public interface ISiteRepository
    {
        Task<List<string>> GetExistentSites(bool cache);
        Task<List<Ward>?> GetAllValidWards(bool cache);
        Task<List<SiteType>?> GetAllValidSiteTypes(bool cache);
        Task ReadyCache();
        void ValidateWard(string wardName, string constituencyName, string subCountyName, string countyName);

        ValidationResult ValidateSiteDto(SiteCreateDto siteCreateDto);
    }
}