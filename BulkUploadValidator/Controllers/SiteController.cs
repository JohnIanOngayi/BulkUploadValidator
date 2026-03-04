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
        public SiteController(ISiteRepository repository)
        {
            _siteRepository = repository;
        }

        [HttpGet("validwards")]
        public async Task<ActionResult<List<Ward>>> GetValidWards()
        {
            var wards = await _siteRepository.GetAllValidWards(false);
            return wards != null ? Ok(wards) : StatusCode(500);
        }

        [HttpGet("valdsitetypes")]
        public async Task<ActionResult<List<LinkType>>> GetAllLinkTypes()
        {
            var result = await _siteRepository.GetAllValidSiteTypes(false);
            return result != null ? Ok(result) : StatusCode(500);
        }

        [HttpGet("downloadwards")]
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

        [HttpPost("validateward")]
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

        [HttpPost("uploadSitesExcel")]
        public async Task<IActionResult> UploadSitesExcel(IFormFile file)
        {
            var helper = new ExcelHelper(file);
            var result = await helper.ParseSiteCreateDtos();

            // Check validation errors in controller before passing to repo
            if (result.HasErrors)
            {
                return BadRequest(new
                {
                    Message = "Excel contains validation errors.",
                    Errors = result.Errors
                });
            }

            // Repo check
            for
        }
    }
}
