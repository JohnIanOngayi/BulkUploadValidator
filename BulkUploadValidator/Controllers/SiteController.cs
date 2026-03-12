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
    public class SiteController : ControllerBase
    {
        private readonly ISiteRepository _siteRepository;
        private readonly TemplateGenerator<SiteTemplateConfig> _siteTemplateGen;
        public SiteController(ISiteRepository repository, TemplateGenerator<SiteTemplateConfig> siteTemplateGen)
        {
            _siteRepository = repository;
            _siteTemplateGen = siteTemplateGen;
        }

        [HttpGet("ValidWards")]
        public async Task<ActionResult<List<Ward>>> GetValidWards()
        {
            var wards = await _siteRepository.GetAllValidWards(false);
            return wards != null ? Ok(wards) : StatusCode(500);
        }

        [HttpGet("ValidSiteTypes")]
        public async Task<ActionResult<List<LinkType>>> GetAllLinkTypes()
        {
            var result = await _siteRepository.GetAllValidSiteTypes(false);
            return result != null ? Ok(result) : StatusCode(500);
        }

        [HttpGet("DownloadWards")]
        public async Task<ActionResult> ExportWards()
        {
            using (var workbook = new XLWorkbook())
            {
                var validWards = await _siteRepository.GetAllValidWards(false);
                try
                {
                    var worksheet = workbook.Worksheets.Add("Valid Wards");

                    var currentRow = 1;
                    worksheet.Cell(currentRow, 1).Value = "Ward";
                    worksheet.Cell(currentRow, 2).Value = "Constituency";
                    worksheet.Cell(currentRow, 3).Value = "SubCounty";
                    worksheet.Cell(currentRow, 4).Value = "County";

                    foreach (var ward in validWards)
                    {
                        currentRow++;
                        worksheet.Cell(currentRow, 1).Value = ward.WardName;
                        worksheet.Cell(currentRow, 2).Value = ward.ConstituencyName;
                        worksheet.Cell(currentRow, 3).Value = ward.SubCountyName;
                        worksheet.Cell(currentRow, 4).Value = ward.CountyName;
                    }

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();

                        return File(content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            "wards.xlsx");
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("An error occurred;");
                    return StatusCode(500);
                }
            }
        }

        [HttpGet("DownloadTemplate")]
        public async Task<IActionResult> DownloadTemplate()
        {
            var bytes = await _siteTemplateGen.GenerateTemplateAsync();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "SiteBulkUploadTemplate.xlsx");
        }

        [HttpPost("ValidateWard")]
        public async Task<IActionResult> ValidateWard(WardDto ward)
        {
            try
            {
                await _siteRepository.ReadyCache();
                _siteRepository.ValidateWard(ward.WardName, ward.ConstituencyName, ward.SubCountyName, ward.CountyName);
                return Ok("Ward is valid!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occured in {nameof(ValidateWard)}: {ex.Message}");
                return BadRequest(ex.Message.ToString());
            }
        }

        [HttpPost("ValidateSiteCreateDto")]
        public async Task<IActionResult> ValidateSiteCreateDto(SiteCreateDto site)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _siteRepository.ReadyCache();

            var result = _siteRepository.ValidateSiteDto(site);
            if (result.Error != null)
                return BadRequest(result.Error);

            return Ok($"Site is valid.");
        }

        [HttpPost("UploadSitesExcel")]
        public async Task<IActionResult> UploadSitesExcel(IFormFile file)
        {
            // empty file or no file
            if (file == null || file.Length == 0)
                return BadRequest("Invalid file.");
            if (!file.FileName.EndsWith(".xlsx") && !file.FileName.EndsWith(".xls"))
                return BadRequest("Only Excel files (.xlsx, .xls) are allowed.");

            var helper = new ExcelHelper(file);
            var result = await helper.ParseSiteCreateDtos();

            // zero rows
            if (result.ValidItems.Count == 0)
                return BadRequest(new { Message = "Excel has no rows.", Errors = new List<string> { "Excel has no rows." } });

            // duplicates, blank cells, wrong data types in cells
            if (result.HasErrors)
                return BadRequest(new { Message = "Excel contains validation errors.", Errors = result.Errors });

            await _siteRepository.ReadyCache();

            // clashes with db, wrong electoral relationships, existent records etc
            var repoErrors = new List<string>();
            foreach (var parsedSite in result.ValidItems)
            {
                var validation = _siteRepository.ValidateSiteDto(parsedSite);
                if (!validation.IsSuccess)
                    repoErrors.Add(validation.Error!);
            }
            if (repoErrors.Count > 0)
                return BadRequest(new { Message = "Validation failed.", Errors = repoErrors });

            // insert / any error is now an infra error (500)
            var success = await _siteRepository.BulkInsertSites(result.ValidItems);
            if (!success)
                return StatusCode(500, "An error occurred while inserting sites.");

            return Ok($"{result.ValidItems.Count} sites inserted successfully.");
        }
    }
}
