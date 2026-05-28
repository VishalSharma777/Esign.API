using Microsoft.AspNetCore.Mvc;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Utilities;

namespace ESign.API.Controllers;


[ApiController]
[Route("api/v1/esign")]
public class PdfController : ControllerBase
{
	private readonly IPdfStorageService _pdfService;

	public PdfController(IPdfStorageService pdfService)
	{
		_pdfService = pdfService;
	}


	[HttpPost("upload-pdf")]
	[Consumes("multipart/form-data")]   // tells Swagger this is a file upload, not JSON
	public async Task<IActionResult> UploadPdf(IFormFile file)
	{
		var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

		SafeLogger.App($"[PDF CONTROLLER] POST /upload-pdf | FileName: {file?.FileName} | CorrelationId: {correlationId}");

		var base64 = await _pdfService.ValidateAndConvertToBase64Async(file!);

		SafeLogger.App($"[PDF CONTROLLER] PDF upload SUCCESS | CorrelationId: {correlationId}");

		return Ok(new
		{
			status = "SUCCESS",
			file_name = file!.FileName,                                
			file_size_kb = Math.Round(file.Length / 1024.0, 2),          // size in KB rounded to 2 decimal
			base64 = base64,                                        
			message = "Copy the base64 value and use it in POST /api/v1/esign/create → pdf_base64 field"
		});
	}
}