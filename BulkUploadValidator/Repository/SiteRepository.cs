using BulkUploadValidator.Models;
using Dapper;
using MySql.Data.MySqlClient;
using System.Data;

namespace BulkUploadValidator.Repository
{
    public class SiteRepository : ISiteRepository
    {
        private readonly IConfiguration _config;
        private readonly string _connectionString;
        private readonly MySqlConnection _connection;

        private HashSet<string> _counties = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _subCounties = new(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _constituencies = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Ward> _wardsCache = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, SiteType> _siteTypesCache = new(StringComparer.OrdinalIgnoreCase);

        public SiteRepository(IConfiguration configuration)
        {
            _config = configuration;
            _connectionString = _config.GetConnectionString("DefaultConnection");
            _connection = new MySqlConnection(connectionString: _connectionString);
        }

        public async Task<List<SiteType>?> GetAllValidSiteTypes(bool cache)
        {
            try
            {
                const string querySql = @"
                    SELECT
                        SiteTypeID as SiteTypeId, SiteTypeName, IsDelete as IsDeleted, IsActive
                    FROM SiteTypeMaster
                    WHERE IsDelete = 0;";

                var result = (await _connection.QueryAsync<SiteType>(querySql, commandType: CommandType.Text)).ToList();
                if (cache == true)
                {
                    foreach (var item in result)
                        _siteTypesCache.Add(item.SiteTypeName, item);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occured in {nameof(GetAllValidSiteTypes)}: {ex}");
                return null;
            }
        }

        public async Task<List<Ward>?> GetAllValidWards(bool cache)
        {
            try
            {
                const string querySql = @"
                    SELECT
                        w.WardID as WardId, w.WardName, 
                        c.CountyId, c.CountyName,
                        sc.SubCountyID as SubCountyId, sc.SubCountyName,
                        cs.ConstituencyId, cs.ConstituencyName
                    FROM WardMaster w
                    JOIN ConstituencyMaster cs 
                        ON cs.ConstituencyID = w.ConstituencyId
                    JOIN SubCountyMaster sc
                        ON sc.SubCountyID = cs.SubCountyID
                    JOIN County_Master c
                        ON sc.CountyID = c.CountyId
                    ORDER BY WardName ASC;";

                var result = (await _connection.QueryAsync<Ward>(querySql, commandType: CommandType.Text)).ToList();

                if (cache == true)
                {
                    foreach (var item in result)
                    {
                        if (!_constituencies.Contains(item.ConstituencyName))
                            _constituencies.Add(item.ConstituencyName);
                        if (!_subCounties.Contains(item.SubCountyName))
                            _subCounties.Add(item.SubCountyName);
                        if (!_counties.Contains(item.CountyName))
                            _counties.Add(item.CountyName);
                        _wardsCache.Add(item.WardName, item);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An exception occurred in {nameof(GetAllValidWards)}: {ex}");
                return null;
            }
        }

        public async Task ReadyCache()
        {
            try
            {
                if (_wardsCache.Keys.Count == 0)
                    _ = await GetAllValidWards(true);
                if (_siteTypesCache.Keys.Count == 0)
                    _ = await GetAllValidSiteTypes(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in {nameof(ReadyCache)}: {ex.Message}");
            }
        }

        public void ValidateWard(string wardName, string constituencyName, string subCountyName, string countyName)
        {
            if (!_wardsCache.TryGetValue(wardName, out var path))
                throw new Exception($"Ward '{wardName}' not found.");

            if (!_constituencies.Contains(constituencyName))
                throw new Exception($"Constituency '{constituencyName}' does not exist.");

            if (!_subCounties.Contains(subCountyName))
                throw new Exception($"SubCounty '{subCountyName}' does not exist.");

            if (!_counties.Contains(countyName))
                throw new Exception($"County '{countyName}' does not exist.");


            if (!string.Equals(path.ConstituencyName, constituencyName, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Ward '{wardName}' does not exist in Constituency '{constituencyName}'.");

            if (!string.Equals(path.SubCountyName, subCountyName, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Ward '{wardName}' does not exist in SubCounty '{subCountyName}'.");

            if (!string.Equals(path.CountyName, countyName, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Ward '{wardName}' is not in County '{countyName}'.");
        }
    }
}
