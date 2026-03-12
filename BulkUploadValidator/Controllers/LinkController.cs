using BulkUploadValidator.Dtos;
using BulkUploadValidator.Models;
using BulkUploadValidator.Repository;
using BulkUploadValidator.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace BulkUploadValidator.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LinkController : ControllerBase
    {
        private readonly ILinkRepository _linkRepository;
        private readonly TemplateGenerator<LinkTemplateConfig> _linkTemplateGen;
        public LinkController(ILinkRepository linkRepository, TemplateGenerator<LinkTemplateConfig> linkTemplateGen)
        {
            _linkRepository = linkRepository;
            _linkTemplateGen = linkTemplateGen;
        }
        [HttpGet("ValidSubCounties")]
        public async Task<ActionResult<List<SubCounty>>> GetAllSubCounties()
        {
            await _linkRepository.ReadyCache();
            var validSubCounties = await _linkRepository.GetAllValidSubCounties(false);
            return Ok(validSubCounties);
        }

        [HttpGet("ValidLinkTypes")]
        public async Task<ActionResult<List<LinkType>>> GetAllLinkTypes()
        {
            var result = await _linkRepository.GetAllValidLinkTypes(false);
            return result != null ? Ok(result) : StatusCode(500);
        }

        [HttpGet("DownloadSubCounties")]
        public async Task<ActionResult> ExportSubCounties()
        {
            using (var workbook = new XLWorkbook())
            {
                var validSubCounties = await _linkRepository.GetAllValidSubCounties(false);
                try
                {
                    var worksheet = workbook.Worksheets.Add("Valid SubCounties");

                    var currentRow = 1;
                    worksheet.Cell(currentRow, 1).Value = "SubCounty";
                    worksheet.Cell(currentRow, 2).Value = "County";

                    foreach (var subCounty in validSubCounties)
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = subCounty.SubCountyName;
                        worksheet.Cell(currentRow, 2).Value = subCounty.CountyName;
                    }

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();

                        return File(content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            "subcounties.xlsx");
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("An error occurred;");
                    return StatusCode(500);
                }
            }
        }

        [HttpPost("ValidateSubCounty")]
        public async Task<IActionResult> ValidateSubCounty(SubCountyDto dto)
        {
            try
            {
                await _linkRepository.ReadyCache();
                _linkRepository.ValidateSubCounty(dto.SubCountyName, dto.CountyName);
                return Ok("SubCounty is valid!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occured in {nameof(ValidateSubCounty)}: {ex.Message}");
                return BadRequest(ex.Message.ToString());
            }
        }

        [HttpPost("ValidateLinkCreateDto")]
        public async Task<IActionResult> ValidateLinkCreateDto(LinkCreateDto site)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _linkRepository.ReadyCache();

            var result = _linkRepository.ValidateLinkDto(site);
            if (result.Error != null)
                return BadRequest(result.Error);

            return Ok($"Link is valid.");
        }

        [HttpGet("DownloadTemplate")]
        public async Task<IActionResult> DownloadTemplate()
        {
            var bytes = await _linkTemplateGen.GenerateTemplateAsync();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "LinkBulkUploadTemplate.xlsx");
        }

        [HttpGet("DebugTemplate")]
        public async Task<IActionResult> DebugLinkTemplate()
        {
            var bytes = await _linkTemplateGen.GenerateTemplateAsync();

            using var stream = new MemoryStream(bytes);
            using var workbook = new XLWorkbook(stream);
            var lists = workbook.Worksheet("Lists");

            var namedRanges = workbook.DefinedNames
                .Select(nr => new { nr.Name, Address = nr.Ranges.ToString() })
                .ToList();

            // Also check first row of each column to see what's there
            var columns = lists.ColumnsUsed()
                .Select(c => new
                {
                    Col = c.ColumnNumber(),
                    Header = c.Cell(1).Value.ToString(),
                    Count = c.CellsUsed().Count()
                }).ToList();

            return Ok(new { namedRanges, columns });
        }

        [HttpPost("UploadLinksExcel")]
        public async Task<IActionResult> UploadLinksExcel(IFormFile file)
        {
            // empty file or no file
            if (file == null || file.Length == 0)
                return BadRequest("Invalid file.");
            if (!file.FileName.EndsWith(".xlsx") && !file.FileName.EndsWith(".xls"))
                return BadRequest("Only Excel files (.xlsx, .xls) are allowed.");

            var helper = new ExcelHelper(file);
            var result = await helper.ParseLinkCreateDtos();

            // zero rows
            if (result.ValidItems.Count == 0)
                return BadRequest(new { Message = "Excel has no rows.", Errors = new List<string> { "Excel has no rows." } });

            // duplicates, blank cells, wrong data types in cells
            if (result.HasErrors)
                return BadRequest(new { Message = "Excel contains validation errors.", Errors = result.Errors });

            await _linkRepository.ReadyCache();

            // clashes with db, wrong electoral relationships, existent records etc
            var repoErrors = new List<string>();
            foreach (var parsedLink in result.ValidItems)
            {
                var validation = _linkRepository.ValidateLinkDto(parsedLink);
                if (!validation.IsSuccess)
                    repoErrors.Add(validation.Error!);
            }
            if (repoErrors.Count > 0)
                return BadRequest(new { Message = "Validation failed.", Errors = repoErrors });

            // insert / any error is now an infra error (500)
            var success = await _linkRepository.BulkInsertLinks(result.ValidItems);
            if (!success)
                return StatusCode(500, "An error occurred while inserting sites.");

            return Ok($"{result.ValidItems.Count} links inserted successfully.");
        }
    }
}
