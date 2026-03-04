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

        private HashSet<string> _counties = new(StringComparer.OrdinalIgnoreCase);
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
                        if (!_counties.Contains(item.CountyName))
                            _counties.Add(item.CountyName);
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

        public void ValidateSubCounty(string subCountyName, string countyName)
        {
            if (!_subCountiesCache.TryGetValue(subCountyName, out var path))
                throw new Exception($"SubCounty '{subCountyName}' not found.");

            if (!_counties.Contains(countyName))
                throw new Exception($"County '{countyName}' does not exist.");


            if (!string.Equals(path.CountyName, countyName, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"SubCounty '{subCountyName}' is not in County '{countyName}'.");
        }
    }
}
