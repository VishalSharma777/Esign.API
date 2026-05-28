namespace ESign.API.Application.Services.Interfaces;


public interface IPdfStorageService
{

	Task<string> SaveSignedPdfAsync(string base64Content, string docketId, DateTime completedAt);
	Task<string> ValidateAndConvertToBase64Async(IFormFile file);
}