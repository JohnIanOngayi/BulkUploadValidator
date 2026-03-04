using BulkUploadValidator.Dtos;
using BulkUploadValidator.Models;
using BulkUploadValidator.Repository;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace BulkUploadValidator.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LinkController : ControllerBase
    {
        private readonly ILinkRepository _linkRepository;
        public LinkController(ILinkRepository linkRepository)
        {
            _linkRepository = linkRepository;
        }
        [HttpGet("validsubcounties")]
        public async Task<ActionResult<List<SubCounty>>> GetAllSubCounties()
        {
            await _linkRepository.ReadyCache();
            var validSubCounties = await _linkRepository.GetAllValidSubCounties(false);
            return Ok(validSubCounties);
        }

        [HttpGet("valdlinktypes")]
        public async Task<ActionResult<List<LinkType>>> GetAllLinkTypes()
        {
            var result = await _linkRepository.GetAllValidLinkTypes(false);
            return result != null ? Ok(result) : StatusCode(500);
        }

        [HttpGet("downloadsubcounties")]
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

        [HttpPost("validatesubcounty")]
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
    }
}
