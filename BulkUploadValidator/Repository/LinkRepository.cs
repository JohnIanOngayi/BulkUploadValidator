using BulkUploadValidator.Dtos;
using BulkUploadValidator.Models;
using Dapper;
using MySql.Data.MySqlClient;
using System.Data;

namespace BulkUploadValidator.Repository
{
    public class LinkRepository : ILinkRepository
    {
        private readonly IConfiguration _config;
        private readonly string _connectionString;
        private readonly MySqlConnection _connection;

        private Dictionary<string, int> _counties = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, SubCounty> _subCountiesCache = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, LinkType> _linkTypesCache = new(StringComparer.OrdinalIgnoreCase);
        public LinkRepository(IConfiguration configuration)
        {
            _config = configuration;
            _connectionString = _config.GetConnectionString("DefaultConnection");
            _connection = new MySqlConnection(connectionString: _connectionString);
        }

        public async Task<List<LinkType>?> GetAllValidLinkTypes(bool cache)
        {
            try
            {
                const string querySql = @"
                    SELECT
                        LinkTypeID as LinkTypeId, LinkTypeName, IsDelete as IsDeleted, IsActive
                    FROM LinkTypeMaster
                    WHERE IsDelete = 0;";

                var result = (await _connection.QueryAsync<LinkType>(querySql, commandType: CommandType.Text)).ToList();
                if (cache == true)
                {
                    foreach (var item in result)
                        _linkTypesCache.Add(item.LinkTypeName, item);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occured in {nameof(GetAllValidLinkTypes)}: {ex}");
                return null;
            }
        }

        public async Task<List<SubCounty>?> GetAllValidSubCounties(bool cache)
        {
            try
            {

                const string querySql = @"
                    SELECT 
                        s.SubCountyID as SubCountyId, s.SubCountyName, c.CountyID as CountyId, c.CountyName
                    FROM SubCountyMaster s
                    JOIN County_Master c ON s.CountyID = c.CountyID;
                    ";

                var result = (await _connection.QueryAsync<SubCounty>(querySql, commandType: CommandType.Text)).ToList();
                if (cache == true)
                {
                    foreach (var item in result)
                    {
                        if (!_counties.TryGetValue(item.CountyName, out _))
                            _counties.Add(item.CountyName, item.CountyId);
                        _subCountiesCache.Add(item.SubCountyName, item);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occured in {nameof(GetAllValidSubCounties)}: {ex}");
                return null;
            }
        }

        public async Task ReadyCache()
        {
            try
            {
                if (_subCountiesCache.Keys.Count == 0)
                    _ = await GetAllValidSubCounties(true);
                if (_linkTypesCache.Keys.Count == 0)
                    _ = await GetAllValidLinkTypes(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in {nameof(ReadyCache)}: {ex.Message}");
            }
        }

        public ValidationResult ValidateLinkDto(LinkCreateDto linkCreateDto)
        {
            // Validate LinkType
            if (!_linkTypesCache.TryGetValue(linkCreateDto.LinkType, out _))
                return ValidationResult.Failure($"LinkType '{linkCreateDto.LinkType}' not found.");

            if (!_subCountiesCache.TryGetValue(linkCreateDto.StartCountyName, out var path))
                return new ValidationResult { IsSuccess = false, Error = $"SubCounty '{subCountyName}' not found." };
            return ValidationResult.Failure($"SubCounty '{linkCreateDto.SubCountyName}' not found.");

            if (!_counties.ContainsKey(countyName))
                return new ValidationResult { IsSuccess = false, Error = $"County '{countyName}' does not exist." };

            if (!string.Equals(path.CountyName, countyName, StringComparison.OrdinalIgnoreCase))
                return new ValidationResult { IsSuccess = false, Error = $"SubCounty '{subCountyName}' is not in County '{countyName}'." };

            if (!_linkTypesCache.ContainsKey(linkTypeName))
                return new ValidationResult { IsSuccess = false, Error = $"LinkType '{linkTypeName}' is invalid." };

            return new ValidationResult { IsSuccess = true };
        }

        public ValidationResult ValidateLinkDtos(List<LinkCreateDto> linkCreateDtos)
        {
            throw new NotImplementedException();
        }

        public void ValidateSubCounty(string subCountyName, string countyName)
        {
            if (!_subCountiesCache.TryGetValue(subCountyName, out var path))
                throw new Exception($"SubCounty '{subCountyName}' not found.");

            if (!_counties.TryGetValue(countyName, out _))
                throw new Exception($"County '{countyName}' does not exist.");


            if (!string.Equals(path.CountyName, countyName, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"SubCounty '{subCountyName}' is not in County '{countyName}'.");
        }
    }
}
