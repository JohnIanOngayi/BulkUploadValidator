using BulkUploadValidator.Dtos;
using BulkUploadValidator.Models;
using Dapper;
using MySqlConnector;
using System.Data;

namespace BulkUploadValidator.Repository
{
    public class LinkRepository : ILinkRepository
    {
        private readonly string _connectionString;

        private Dictionary<string, int> _countiesCache = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, SubCounty> _subCountiesCache = new(StringComparer.OrdinalIgnoreCase);

        private HashSet<string> _existentLinks = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, LinkType> _linkTypesCache = new(StringComparer.OrdinalIgnoreCase);

        public LinkRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        private IDbConnection CreateConnection() => new MySqlConnection(_connectionString);

        public async Task<List<LinkType>?> GetAllValidLinkTypes(bool cache)
        {
            try
            {
                const string querySql = @"
                    SELECT
                        LinkTypeID as LinkTypeId, LinkTypeName, IsDelete as IsDeleted, IsActive
                    FROM 
                        LinkTypeMaster
                    WHERE 
                        IsDelete = 0 AND IsActive = 1
                    ORDER BY
                        LinkTypeName ASC;";

                using var connection = CreateConnection();
                var result = (await connection.QueryAsync<LinkType>(querySql, commandType: CommandType.Text)).ToList();
                if (cache == true)
                {
                    foreach (var item in result)
                        _linkTypesCache.Add(item.LinkTypeName.Trim().ToUpperInvariant(), item);
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
                    FROM 
                        SubCountyMaster s
                    JOIN 
                        County_Master c ON s.CountyID = c.CountyID
                    WHERE
                        s.IsDelete = 0 AND s.IsActive = 1
                    ORDER BY
                        s.SubCountyName ASC;
                    ";

                using var connection = CreateConnection();
                var result = (await connection.QueryAsync<SubCounty>(querySql, commandType: CommandType.Text)).ToList();
                if (cache == true)
                {
                    foreach (var item in result)
                    {
                        item.CountyName.Trim().ToUpperInvariant();
                        item.SubCountyName.Trim().ToUpperInvariant();

                        if (!_countiesCache.TryGetValue(item.CountyName, out _))
                            _countiesCache.Add(item.CountyName, item.CountyId);
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

        public async Task<List<string>> GetExistentLinks(bool cache)
        {
            try
            {
                const string query = @"SELECT LinkName FROM LinkMaster;";

                using var connection = CreateConnection();
                var result = (await connection.QueryAsync<string>(query, commandType: CommandType.Text)).ToList();
                if (cache)
                    _existentLinks.UnionWith(result.Select(x => x.Trim().ToUpperInvariant()));

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in {nameof(GetExistentLinks)}: {ex}");
                return null;
            }
        }

        public async Task ReadyCache()
        {
            try
            {
                await Task.WhenAll(
                    GetExistentLinks(true),
                    GetAllValidSubCounties(true),
                    GetAllValidLinkTypes(true)
                    );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred in {nameof(ReadyCache)}: {ex.Message}");
            }
        }

        public ValidationResult ValidateLinkDto(LinkCreateDto linkCreateDto)
        {
            linkCreateDto.LinkName = linkCreateDto.LinkName.Trim().ToUpperInvariant();
            linkCreateDto.LinkType = linkCreateDto.LinkType.Trim().ToUpperInvariant();
            linkCreateDto.StartSubCountyName = linkCreateDto.StartSubCountyName.Trim().ToUpperInvariant();
            linkCreateDto.EndSubCountyName = linkCreateDto.EndSubCountyName.Trim().ToUpperInvariant();
            linkCreateDto.StartCountyName = linkCreateDto.StartCountyName.Trim().ToUpperInvariant();
            linkCreateDto.EndCountyName = linkCreateDto.EndCountyName.Trim().ToUpperInvariant();

            if (_existentLinks.TryGetValue(linkCreateDto.LinkName, out _))
                return ValidationResult.Failure($"Link with name '{linkCreateDto.LinkName}' already exists.");

            // Validate LinkType exists and is active
            if (!_linkTypesCache.TryGetValue(linkCreateDto.LinkType, out _))
                return ValidationResult.Failure($"LinkType '{linkCreateDto.LinkType}' not found.");
            if (Convert.ToInt32(_linkTypesCache[linkCreateDto.LinkType].IsActive) != 1)
                return ValidationResult.Failure($"LinkType '{linkCreateDto.LinkType}' is not active.");

            // validate start electorals exist
            if (!_subCountiesCache.TryGetValue(linkCreateDto.StartSubCountyName, out var startElectorals))
                return ValidationResult.Failure($"SubCounty '{linkCreateDto.StartSubCountyName}' not found.");
            if (!_countiesCache.TryGetValue(linkCreateDto.StartCountyName, out _))
                return ValidationResult.Failure($"County '{linkCreateDto.StartCountyName}' not found.");

            // validate end electorals exist
            if (!_subCountiesCache.TryGetValue(linkCreateDto.EndSubCountyName, out var endElectorals))
                return ValidationResult.Failure($"SubCounty '{linkCreateDto.EndSubCountyName}' not found.");
            if (!_countiesCache.TryGetValue(linkCreateDto.EndCountyName, out _))
                return ValidationResult.Failure($"County '{linkCreateDto.EndCountyName}' not found.");

            // validate start electorals hierarchy
            if (!string.Equals(startElectorals.CountyName, linkCreateDto.StartCountyName, StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Failure($"SubCounty '{linkCreateDto.StartSubCountyName}' does not exist in County '{linkCreateDto.StartCountyName}'.");

            //validate end elesctorals hierarchy
            if (!string.Equals(endElectorals.CountyName, linkCreateDto.EndCountyName, StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Failure($"SubCounty '{linkCreateDto.EndSubCountyName}' does not exist in County '{linkCreateDto.EndCountyName}'.");

            return new ValidationResult { IsSuccess = true };
        }

        public void ValidateSubCounty(string subCountyName, string countyName)
        {
            if (!_subCountiesCache.TryGetValue(subCountyName, out var path))
                throw new Exception($"SubCounty '{subCountyName}' not found.");

            if (!_countiesCache.TryGetValue(countyName, out _))
                throw new Exception($"County '{countyName}' does not exist.");


            if (!string.Equals(path.CountyName, countyName, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"SubCounty '{subCountyName}' does not exist in County '{countyName}'.");
        }

        public async Task<bool> BulkInsertLinks(List<LinkCreateDto> links)
        {
            var table = new DataTable();

            table.Columns.Add("LinkName", typeof(string));
            table.Columns.Add("LinkType", typeof(int));

            table.Columns.Add("StartLocation", typeof(string));
            table.Columns.Add("StartPointLatitude", typeof(double));
            table.Columns.Add("StartPointLongitude", typeof(double));
            table.Columns.Add("StartCountyId", typeof(int));
            table.Columns.Add("StartSubCountyId", typeof(int));

            table.Columns.Add("EndLocation", typeof(string));
            table.Columns.Add("EndPointLatitude", typeof(double));
            table.Columns.Add("EndPointLongitude", typeof(double));
            table.Columns.Add("EndCountyId", typeof(int));
            table.Columns.Add("EndSubCountyId", typeof(int));

            foreach (var link in links)
            {
                table.Rows.Add(
                    link.LinkName,
                    _linkTypesCache[link.LinkType].LinkTypeId,

                    link.StartLocation,
                    link.StartLatitude,
                    link.StartLongitude,
                    _countiesCache[link.StartCountyName],
                    _subCountiesCache[link.StartSubCountyName].SubCountyId,

                    link.EndLocation,
                    link.EndLatitude,
                    link.StartLongitude,
                    _countiesCache[link.EndCountyName],
                    _subCountiesCache[link.EndSubCountyName].SubCountyId
                );
            }

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                var bulkCopy = new MySqlBulkCopy(connection, transaction);
                bulkCopy.DestinationTableName = "LinkMaster";

                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(0, "LinkName"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(1, "LinkType"));

                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(2, "StartLocation"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(3, "StartPointLatitude"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(4, "StartPointLongitude"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(5, "StartCountyId"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(6, "StartSubCountyId"));

                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(7, "EndLocation"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(8, "EndPointLatitude"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(9, "EndPointLongitude"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(10, "EndCountyId"));
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(11, "EndSubCountyId"));

                await bulkCopy.WriteToServerAsync(table);
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"An error occurred in {nameof(BulkInsertLinks)}: {ex}");
                return false;
            }
        }
    }
}
