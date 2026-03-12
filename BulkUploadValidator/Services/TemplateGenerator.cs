using BulkUploadValidator.Models;
using ClosedXML.Excel;
using Dapper;
using MySqlConnector;
using System.Text.RegularExpressions;

namespace BulkUploadValidator.Services
{
    /// <summary>
    /// Produces a styled Excel bulk-upload template with cascading dropdowns
    /// for any entity whose column layout is described by <typeparamref name="TConfig"/>.
    /// </summary>
    public class TemplateGenerator<TConfig> where TConfig : ITemplateConfig, new()
    {
        private readonly string _connectionString;
        private readonly TConfig _config = new();
        private const int DataRows = 100;

        // Cascade depth constants (indices into each ElectoralCascade list)
        private const int CascadeCounty = 0;
        private const int CascadeSubCounty = 1;
        private const int CascadeConstituency = 2;  // optional
        private const int CascadeWard = 3;  // optional

        public TemplateGenerator(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<byte[]> GenerateTemplateAsync()
        {
            // ── 1. Fetch reference data ──────────────────────────────────────
            List<TypeRow> types;
            List<County> counties;
            List<SubCounty> subCounties;
            List<Constituency> constituencies = new();
            List<Ward> wards = new();

            bool needsFullCascade = _config.ElectoralCascades
                .Any(c => c.Count >= 4);

            using (var connection = new MySqlConnection(_connectionString))
            {
                types = (await connection.QueryAsync<TypeRow>(_config.TypeQuerySql)).ToList();

                counties = (await connection.QueryAsync<County>(@"
                    SELECT CountyId, CountyName
                    FROM County_Master")).ToList();

                subCounties = (await connection.QueryAsync<SubCounty>(@"
                    SELECT SubCountyID as SubCountyId, SubCountyName, CountyID as CountyId
                    FROM SubCountyMaster")).ToList();

                if (needsFullCascade)
                {
                    constituencies = (await connection.QueryAsync<Constituency>(@"
                        SELECT ConstituencyId, ConstituencyName, SubCountyID as SubCountyId
                        FROM ConstituencyMaster")).ToList();

                    wards = (await connection.QueryAsync<Ward>(@"
                        SELECT WardID as WardId, WardName, ConstituencyId
                        FROM WardMaster")).ToList();
                }
            }

            // ── 2. Build workbook ────────────────────────────────────────────
            using var workbook = new XLWorkbook();
            var sheet = workbook.AddWorksheet("BulkUpload");
            var lists = workbook.AddWorksheet("Lists");
            lists.Visibility = XLWorksheetVisibility.VeryHidden;

            WriteHeaders(sheet);
            PopulateListsSheet(lists, types, counties, subCounties, constituencies, wards);
            ApplyValidations(sheet);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // ── Headers ──────────────────────────────────────────────────────────

        private void WriteHeaders(IXLWorksheet sheet)
        {
            for (int i = 0; i < _config.Headers.Count; i++)
            {
                var cell = sheet.Cell(1, i + 1);
                cell.Value = _config.Headers[i];
                cell.Style.Font.SetFontName("Roboto Condensed");
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#342E37");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                sheet.Column(i + 1).Width = 22;
            }

            sheet.SheetView.FreezeRows(1);
            sheet.Row(1).Height = 20;
        }

        // ── Lists sheet ───────────────────────────────────────────────────────

        private void PopulateListsSheet(
            IXLWorksheet lists,
            List<TypeRow> types,
            List<County> counties,
            List<SubCounty> subCounties,
            List<Constituency> constituencies,
            List<Ward> wards)
        {
            int col = 1;

            // Column(s) for the entity type (SiteType / LinkType)
            col = WriteList(lists, col, types.Select(t => t.Name), _config.TypeListRangeName);

            // County list — shared across all cascade chains
            col = WriteList(lists, col, counties.Select(c => c.CountyName), "CountyList");

            // For every cascade chain that has been configured …
            foreach (var cascade in _config.ElectoralCascades)
            {
                bool fullCascade = cascade.Count >= 4;

                // SubCounty lists keyed by County
                foreach (var county in counties)
                {
                    var subs = subCounties
                        .Where(s => s.CountyId == county.CountyId)
                        .Select(s => s.SubCountyName)
                        .ToList();

                    col = WriteList(lists, col, subs, SafeName(county.CountyName));
                }

                if (!fullCascade) continue;

                // Constituency lists keyed by SubCounty
                foreach (var sub in subCounties)
                {
                    var cons = constituencies
                        .Where(c => c.SubCountyId == sub.SubCountyId)
                        .Select(c => c.ConstituencyName)
                        .ToList();

                    col = WriteList(lists, col, cons, SafeName(sub.SubCountyName));
                }

                // Ward lists keyed by Constituency
                foreach (var con in constituencies)
                {
                    var ws = wards
                        .Where(w => w.ConstituencyId == con.ConstituencyId)
                        .Select(w => w.WardName)
                        .ToList();

                    col = WriteList(lists, col, ws, SafeName(con.ConstituencyName));
                }
            }
        }

        /// <summary>Writes a vertical list and registers it as a named range. Returns next free column.</summary>
        private static int WriteList(IXLWorksheet lists, int col, IEnumerable<string> values, string rangeName)
        {
            var items = values.ToList();
            if (items.Count == 0) return col + 1;

            for (int row = 0; row < items.Count; row++)
                lists.Cell(row + 1, col).Value = items[row];

            lists.Range(1, col, items.Count, col).AddToNamed(rangeName);
            return col + 1;
        }

        // ── Data validation ───────────────────────────────────────────────────

        private void ApplyValidations(IXLWorksheet sheet)
        {
            int lastRow = DataRows + 1;

            // Type dropdown(s) — same list for every configured column
            foreach (int typeCol in _config.TypeDropdownColumns)
            {
                sheet.Range(2, typeCol, lastRow, typeCol)
                    .CreateDataValidation().List(_config.TypeListRangeName, true);
            }

            // County always uses the shared CountyList
            foreach (var cascade in _config.ElectoralCascades)
            {
                int countyCol = cascade[CascadeCounty];
                int subCountyCol = cascade[CascadeSubCounty];
                bool fullCascade = cascade.Count >= 4;
                int constituencyCol = fullCascade ? cascade[CascadeConstituency] : -1;
                int wardCol = fullCascade ? cascade[CascadeWard] : -1;

                // County column → shared CountyList
                sheet.Range(2, countyCol, lastRow, countyCol)
                    .CreateDataValidation().List("CountyList", true);

                // SubCounty: INDIRECT on the County cell in the same row
                for (int r = 2; r <= lastRow; r++)
                {
                    var countyRef = CellRef(r, countyCol);
                    sheet.Cell(r, subCountyCol)
                        .CreateDataValidation()
                        .List($"INDIRECT(SUBSTITUTE({countyRef},\" \",\"_\"))", true);
                }

                if (!fullCascade) continue;

                // Constituency: INDIRECT on the SubCounty cell
                for (int r = 2; r <= lastRow; r++)
                {
                    var subRef = CellRef(r, subCountyCol);
                    sheet.Cell(r, constituencyCol)
                        .CreateDataValidation()
                        .List($"INDIRECT(SUBSTITUTE({subRef},\" \",\"_\"))", true);
                }

                // Ward: INDIRECT on the Constituency cell
                for (int r = 2; r <= lastRow; r++)
                {
                    var conRef = CellRef(r, constituencyCol);
                    sheet.Cell(r, wardCol)
                        .CreateDataValidation()
                        .List($"INDIRECT(SUBSTITUTE({conRef},\" \",\"_\"))", true);
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Converts a (row, col) pair to an Excel cell reference string, e.g. (2, 7) → "G2".</summary>
        private static string CellRef(int row, int col)
        {
            string colLetter = string.Empty;
            while (col > 0)
            {
                col--;
                colLetter = (char)('A' + col % 26) + colLetter;
                col /= 26;
            }
            return $"{colLetter}{row}";
        }

        /// <summary>Converts a location name to a valid Excel named-range identifier.</summary>
        private static string SafeName(string name)
            => Regex.Replace(name.Replace(" ", "_").Replace("-", "_"), @"[^\w]", "");
    }

    // ── Lightweight DTO used only for the type query ──────────────────────────
    internal class TypeRow()
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public object IsActive { get; set; }
    }

}