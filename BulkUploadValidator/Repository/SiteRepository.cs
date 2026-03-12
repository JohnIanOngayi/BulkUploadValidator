using BulkUploadValidator.Dtos;
using BulkUploadValidator.Models;
using Dapper;
using MySqlConnector;
using System.Data;

namespace BulkUploadValidator.Repository
{
    public class SiteRepository : ISiteRepository
    {
        private readonly string _connectionString;

        private Dictionary<string, int> _counties = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> _subCounties = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> _constituencies = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Ward> _wardsCache = new(StringComparer.OrdinalIgnoreCase);

        private HashSet<string> _existentSites = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, SiteType> _siteTypesCache = new(StringComparer.OrdinalIgnoreCase);

        public SiteRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        private IDbConnection CreateConnection() => new MySqlConnection(_connectionString);

        public async Task<List<SiteType>?> GetAllValidSiteTypes(bool cache)
        {
            try
            {
                const string querySql = @"
                    SELECT
                        SiteTypeID as SiteTypeId, SiteTypeName, IsDelete as IsDeleted, IsActive
                    FROM SiteTypeMaster
                    WHERE IsDelete = 0;";

                using var connection = CreateConnection();
                var result = (await connection.QueryAsync<SiteType>(querySql, commandType: CommandType.Text)).ToList();
                if (cache == true)
                {
                    foreach (var item in result)
                        _siteTypesCache.Add(item.SiteTypeName.Trim().ToUpperInvariant(), item);
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

                using var connection = CreateConnection();
                var result = (await connection.QueryAsync<Ward>(querySql, commandType: CommandType.Text)).ToList();

                if (cache == true)
                {
                    foreach (var item in result)
                    {
                        item.WardName = item.WardName.Trim().ToUpperInvariant();
                        item.ConstituencyName = item.ConstituencyName.Trim().ToUpperInvariant();
                        item.SubCountyName = item.SubCountyName.Trim().ToUpperInvariant();
                        item.CountyName = item.CountyName.Trim().ToUpperInvariant();

                        if (!_constituencies.TryGetValue(item.ConstituencyName, out _))
                            _constituencies.Add(item.ConstituencyName, item.ConstituencyId);
                        if (!_subCounties.TryGetValue(item.SubCountyName, out _))
                            _subCounties.Add(item.SubCountyName, item.SubCountyId);
                        if (!_counties.TryGetValue(item.CountyName, out _))
                            _counties.Add(item.CountyName, item.CountyId);
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

        public async Task<List<string>> GetExistentSites(bool cache)
        {
            try
            {
                const string query = @"SELECT SiteName FROM SiteMaster;";

                using var connection = CreateConnection();
                var result = (await connection.QueryAsync<string>(query, commandType: CommandType.Text)).ToList();
                if (cache)
                    _existentSites.UnionWith(result.Select(x => x.Trim().ToUpperInvariant()));

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in {nameof(GetExistentSites)}: {ex}");
                return null;
            }
        }

        public async Task ReadyCache()
        {
            try
            {
                await Task.WhenAll(
                    GetExistentSites(true),
                    GetAllValidWards(true),
                    GetAllValidSiteTypes(true)
                    );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in {nameof(ReadyCache)}: {ex.Message}");
            }
        }

        public ValidationResult ValidateSiteDto(SiteCreateDto siteCreateDto)
        {
            //Func runs after no dupes in excel. Now check that the rows do not already exist in db
            siteCreateDto.SiteName = siteCreateDto.SiteName.Trim().ToUpperInvariant();
            siteCreateDto.WardName = siteCreateDto.WardName.Trim().ToUpperInvariant();
            siteCreateDto.ConstituencyName = siteCreateDto.ConstituencyName.Trim().ToUpperInvariant();
            siteCreateDto.SubCountyName = siteCreateDto.SubCountyName.Trim().ToUpperInvariant();
            siteCreateDto.CountyName = siteCreateDto.CountyName.Trim().ToUpperInvariant();

            // check dupes in names
            if (_existentSites.TryGetValue(siteCreateDto.SiteName, out _))
                return ValidationResult.Failure($"Site with name '{siteCreateDto.SiteName}' already exists.");

            // Validate SiteType exists and is active
            if (!_siteTypesCache.TryGetValue(siteCreateDto.SiteType, out _))
                return ValidationResult.Failure($"SiteType '{siteCreateDto.SiteType}' not found.");
            if (Convert.ToInt32(_siteTypesCache[siteCreateDto.SiteType].IsActive) != 1)
                return ValidationResult.Failure($"SiteType '{siteCreateDto.SiteType}' is not active.");

            // Validate Electorals exist
            if (!_wardsCache.TryGetValue(siteCreateDto.WardName, out var path))
                return ValidationResult.Failure($"Ward '{siteCreateDto.WardName}' not found.");

            if (!_constituencies.TryGetValue(siteCreateDto.ConstituencyName, out _))
                return ValidationResult.Failure($"Constituency '{siteCreateDto.ConstituencyName}' not found.");

            if (!_subCounties.TryGetValue(siteCreateDto.SubCountyName, out _))
                return ValidationResult.Failure($"SubCounty '{siteCreateDto.SubCountyName}' not found.");

            if (!_counties.TryGetValue(siteCreateDto.CountyName, out _))
                return ValidationResult.Failure($"County '{siteCreateDto.CountyName}' not found.");

            // Validate Electorals nested correctly
            if (!string.Equals(path.ConstituencyName, siteCreateDto.ConstituencyName, StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Failure($"Ward '{siteCreateDto.WardName}' does not exist in Constituency '{siteCreateDto.ConstituencyName}'.");

            if (!string.Equals(path.SubCountyName, siteCreateDto.SubCountyName, StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Failure($"Ward '{siteCreateDto.WardName}' does not exist in SubCounty '{siteCreateDto.SubCountyName}'.");

            if (!string.Equals(path.CountyName, siteCreateDto.CountyName, StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Failure($"Ward '{siteCreateDto.WardName}' does not exist in County '{siteCreateDto.CountyName}'.");

            return ValidationResult.Success();
        }

        public void ValidateWard(string wardName, string constituencyName, string subCountyName, string countyName)
        {
            if (!_wardsCache.TryGetValue(wardName, out var path))
                throw new Exception($"Ward '{wardName}' not found.");

            if (!_constituencies.TryGetValue(constituencyName, out _))
                throw new Exception($"Constituency '{constituencyName}' does not exist.");

            if (!_subCounties.TryGetValue(subCountyName, out _))
                throw new Exception($"SubCounty '{subCountyName}' does not exist.");

            if (!_counties.TryGetValue(countyName, out _))
                throw new Exception($"County '{countyName}' does not exist.");

            if (!string.Equals(path.ConstituencyName, constituencyName, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Ward '{wardName}' does not exist in Constituency '{constituencyName}'.");

            if (!string.Equals(path.SubCountyName, subCountyName, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Ward '{wardName}' does not exist in SubCounty '{subCountyName}'.");

            if (!string.Equals(path.CountyName, countyName, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Ward '{wardName}' is not in County '{countyName}'.");
        }

        public async Task<bool> BulkInsertSites(List<SiteCreateDto> sites)
        {
            var table = new DataTable();

            table.Columns.Add("SiteName", typeof(string));
            table.Columns.Add("SiteTypeId", typeof(int));
            table.Columns.Add("Location", typeof(string));
            table.Columns.Add("GPSLatitude", typeof(double));
            table.Columns.Add("GPSLongitude", typeof(double));
            table.Columns.Add("CountyId", typeof(int));
            table.Columns.Add("SubCountyId", typeof(int));
            table.Columns.Add("ConstituencyId", typeof(int));
            table.Columns.Add("WardId", typeof(int));
            table.Columns.Add("NoOfInternetUsers", typeof(int));

            foreach (var site in sites)
            {
                table.Rows.Add(
                    site.SiteName,
                    _siteTypesCache[site.SiteType].SiteTypeId,
                    site.LocationName,
                    site.GPSLatitude,
                    site.GPSLongitude,
                    _counties[site.CountyName],
                    _subCounties[site.SubCountyName],
                    _constituencies[site.ConstituencyName],
                    _wardsCache[site.WardName].WardId,
                    site.NoOfInternetUsers
                );
            }

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var bulkCopy = new MySqlBulkCopy(connection, transaction);
                bulkCopy.DestinationTableName = "SiteMaster";
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(0, "SiteName"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(1, "SiteTypeName"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(2, "LocationName"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(3, "GPSLatitude"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(4, "GPSLongitude"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(6, "CountyId"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(7, "SubCountyId"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(8, "ConstituencyId"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(9, "WardId"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(5, "NoOfInternetUsers"));

                await bulkCopy.WriteToServerAsync(table);
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"An error occurred in {nameof(BulkInsertSites)}: {ex}");
                return false;
            }
        }
    }
}