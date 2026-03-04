/* JetBrains Mono */
using BulkUploadValidator.Dtos;
using BulkUploadValidator.Models;
using ClosedXML.Excel;

namespace BulkUploadValidator.Services
{
    public class ExcelHelper
    {
        private readonly IFormFile _excelFile;
        public ExcelHelper(IFormFile formFile) => _excelFile = formFile;

        // Public Entry Points
        public async Task<ExcelParseResult<SiteCreateDto>> ParseSiteCreateDtos()
            => await ParseExcel<SiteCreateDto>(GetSitePropertyMap, "SiteName");

        public async Task<ExcelParseResult<LinkCreateDto>> ParseLinkCreateDtos()
            => await ParseExcel<LinkCreateDto>(GetLinkPropertyMap, "LinkName");

        // Unified Parsing Logic
        private async Task<ExcelParseResult<T>> ParseExcel<T>(Func<IXLRangeRow, Dictionary<string, int>> mapFunc, string uniqueFieldName) where T : new()
        {
            var result = new ExcelParseResult<T>();
            var uniqueKeyTracker = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            using var stream = new MemoryStream();
            await _excelFile.CopyToAsync(stream);
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RangeUsed().RowsUsed();

            var headerRow = rows.First();
            var propertyMap = mapFunc(headerRow);

            foreach (var row in rows.Skip(1))
            {
                if (row.IsEmpty()) continue;

                var dto = new T();
                bool rowHasError = false;

                foreach (var mapping in propertyMap)
                {
                    var cell = row.Cell(mapping.Value);
                    var prop = typeof(T).GetProperty(mapping.Key);
                    if (prop == null) continue;

                    var cellValue = cell.GetValue<object>();
                    bool isMissing = cellValue == null || string.IsNullOrWhiteSpace(cellValue.ToString());

                    if (isMissing)
                    {
                        result.Errors.Add($"Row {row.RowNumber()}: '{mapping.Key}' is required.");
                        rowHasError = true;
                        continue;
                    }

                    try
                    {
                        var convertedValue = Convert.ChangeType(cellValue, prop.PropertyType);
                        prop.SetValue(dto, convertedValue);
                    }
                    catch
                    {
                        result.Errors.Add($"Row {row.RowNumber()}: Invalid data '{cell.Value}' for column '{mapping.Key}'.");
                        rowHasError = true;
                    }
                }

                if (rowHasError) continue;

                // Validate Unique Constraint
                var uniqueVal = typeof(T).GetProperty(uniqueFieldName)?.GetValue(dto)?.ToString() ?? "Unknown";
                if (!uniqueKeyTracker.ContainsKey(uniqueVal)) uniqueKeyTracker[uniqueVal] = new List<int>();
                uniqueKeyTracker[uniqueVal].Add(row.RowNumber());

                result.ValidItems.Add(dto);
            }

            // Post-process duplicates
            foreach (var entry in uniqueKeyTracker.Where(x => x.Value.Count > 1))
            {
                result.Errors.Add($"Duplicate '{entry.Key}' found on rows: {string.Join(", ", entry.Value)}");
            }

            return result;
        }

        // Mappings
        private Dictionary<string, int> GetSitePropertyMap(IXLRangeRow headerRow) => GetMap(headerRow, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "SiteName", nameof(SiteCreateDto.SiteName) },
            { "SiteType", nameof(SiteCreateDto.SiteType) },
            { "Location", nameof(SiteCreateDto.LocationName) },
            { "GPSLatitude", nameof(SiteCreateDto.GPSLatitude) },
            { "GPSLongitude", nameof(SiteCreateDto.GPSLongitude) },
            { "NoOfInternetUsers", nameof(SiteCreateDto.NoOfInternetUsers) },
            { "County", nameof(SiteCreateDto.CountyName) },
            { "SubCounty", nameof(SiteCreateDto.SubCountyName) },
            { "Constituency", nameof(SiteCreateDto.ConstituencyName) },
            { "Ward", nameof(SiteCreateDto.WardName) }
        });

        private Dictionary<string, int> GetLinkPropertyMap(IXLRangeRow headerRow) => GetMap(headerRow, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "LinkName", nameof(LinkCreateDto.LinkName) },
            { "LinkType", nameof(LinkCreateDto.LinkType) },
            { "Start Location", nameof(LinkCreateDto.StartLocation) },
            { "Start Latitude", nameof(LinkCreateDto.StartLatitude) },
            { "Start Longitude", nameof(LinkCreateDto.StartLongitude) },
            { "Start County", nameof(LinkCreateDto.StartCountyName) },
            { "Start SubCounty", nameof(LinkCreateDto.StartSubCountyName) },
            { "End Location", nameof(LinkCreateDto.EndLocation) },
            { "End Latitude", nameof(LinkCreateDto.EndLatitude) },
            { "End Longitude", nameof(LinkCreateDto.EndLongitude) },
            { "End County", nameof(LinkCreateDto.EndCountyName) },
            { "End SubCounty", nameof(LinkCreateDto.EndSubCountyName) }
        });

        private Dictionary<string, int> GetMap(IXLRangeRow headerRow, Dictionary<string, string> definitions)
        {
            var map = new Dictionary<string, int>();
            foreach (var cell in headerRow.Cells())
            {
                string header = cell.GetString().Trim();
                if (definitions.TryGetValue(header, out var propName)) map.Add(propName, cell.Address.ColumnNumber);
            }
            return map;
        }
    }
}