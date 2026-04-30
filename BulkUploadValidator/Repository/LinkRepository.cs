using BulkUploadValidator.Dtos;
using BulkUploadValidator.Models;
using Dapper;
using MySqlConnector;
using System.Data;
using System.Text;

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
            const int batchSize = 200; //600 -> 200 200 200
            const int maxRetries = 3;

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                for (int i = 0; i < links.Count; i += batchSize)
                {
                    var batch = links.Skip(i).Take(batchSize).ToList();

                    await ExecuteLinkBatchWithRetry(connection, transaction, batch, maxRetries);
                }

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Bulk insert failed (rolled back): {ex}");
                return false;
            }
        }

        private async Task ExecuteLinkBatchWithRetry(MySqlConnection conn, MySqlTransaction tx, List<LinkCreateDto> batch, int maxRetries)
        {
            int attempt = 0;

            while (true)
            {
                try
                {
                    await ExecuteLinkBatch(conn, tx, batch);
                    return;
                }
                catch (MySqlException ex) when (IsTransient(ex) && attempt < maxRetries)
                {
                    attempt++;

                    var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
                    await Task.Delay(delay);
                }
                catch
                {
                    throw;
                }
            }
        }

        private async Task ExecuteLinkBatch(MySqlConnection conn, MySqlTransaction tx, List<LinkCreateDto> batch)
        {
            var sb = new StringBuilder();
            var cmd = new MySqlCommand { Connection = conn, Transaction = tx };

            sb.Append(@"
                INSERT INTO LinkMaster
                (LinkName, LinkType,
                StartLocation, StartPointLatitude, StartPointLongitude, StartCountyId, StartSubCountyId,
                EndLocation, EndPointLatitude, EndPointLongitude, EndCountyId, EndSubCountyId, distance)
                VALUES ");

            for (int j = 0; j < batch.Count; j++)
            {
                if (j > 0) sb.Append(",");

                sb.Append($@"
                    (@LinkName{j}, @LinkType{j},
                    @StartLocation{j}, @StartLat{j}, @StartLon{j}, @StartCounty{j}, @StartSubCounty{j},
                    @EndLocation{j}, @EndLat{j}, @EndLon{j}, @EndCounty{j}, @EndSubCounty{j}, @Distance{j})");

                var link = batch[j];

                cmd.Parameters.AddWithValue($"@LinkName{j}", link.LinkName);
                cmd.Parameters.AddWithValue($"@LinkType{j}", _linkTypesCache[link.LinkType].LinkTypeId);

                cmd.Parameters.AddWithValue($"@StartLocation{j}", link.StartLocation);
                cmd.Parameters.AddWithValue($"@StartLat{j}", link.StartLatitude);
                cmd.Parameters.AddWithValue($"@StartLon{j}", link.StartLongitude);
                cmd.Parameters.AddWithValue($"@StartCounty{j}", _countiesCache[link.StartCountyName]);
                cmd.Parameters.AddWithValue($"@StartSubCounty{j}", _subCountiesCache[link.StartSubCountyName].SubCountyId);

                cmd.Parameters.AddWithValue($"@EndLocation{j}", link.EndLocation);
                cmd.Parameters.AddWithValue($"@EndLat{j}", link.EndLatitude);
                cmd.Parameters.AddWithValue($"@EndLon{j}", link.EndLongitude);
                cmd.Parameters.AddWithValue($"@EndCounty{j}", _countiesCache[link.EndCountyName]);
                cmd.Parameters.AddWithValue($"@EndSubCounty{j}", _subCountiesCache[link.EndSubCountyName].SubCountyId);

                cmd.Parameters.AddWithValue($"@Distance{j}", link.Distance);
            }

            cmd.CommandText = sb.ToString();

            await cmd.ExecuteNonQueryAsync();
        }

        private static bool IsTransient(MySqlException ex)
        {
            return ex.Number switch
            {
                1205 => true,
                1213 => true,
                1042 => true,
                2006 => true,
                2013 => true,
                _ => false
            };
        }
    }
}
