using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Utilities;

namespace ESign.API.Application.Services.Implementations
{

	public class PdfStorageService : IPdfStorageService

	{   
		private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D };

		// Max allowed PDF size for upload: 10 MB --- to prevent abuse
		//private const long MaxFileSizeBytes = 10 * 1024 * 1024;   // 10 MB

		// SaveSignedPdfAsync — converts Base64 → bytes → saves to disk
		public async Task<string> SaveSignedPdfAsync(
			string base64Content,
			string docketId,
			DateTime completedAt)
		{
			SafeLogger.App($"[PDF STORAGE] SaveSignedPdfAsync START | DocketId: {docketId}");

			if (string.IsNullOrWhiteSpace(base64Content))
				throw new AppException("PDF_CONTENT_EMPTY", "Signed PDF content is empty", 500);

			// Decode Base64 string → raw PDF bytes
			byte[] pdfBytes;
			try
			{
				pdfBytes = Convert.FromBase64String(base64Content);
			}
			catch (FormatException)
			{
				throw new AppException("PDF_DECODE_FAILED",
					"Signed PDF content is not valid Base64", 500);
			}

			var fullPath = FileStorageHelper.GetSignedDocumentPath(docketId, completedAt);

			// Write bytes to disk — overwrites if file already exists (idempotent)
			await File.WriteAllBytesAsync(fullPath, pdfBytes);

			var relativePath = FileStorageHelper.GetRelativePath(fullPath);

			SafeLogger.App($"[PDF STORAGE] Signed PDF saved | Path: {relativePath} | Size: {pdfBytes.Length} bytes");

			return relativePath;
		}

		public async Task<string> ValidateAndConvertToBase64Async(IFormFile file)
		{
			SafeLogger.App($"[PDF STORAGE] ValidateAndConvert START | FileName: {file.FileName} | Size: {file.Length}");

			// ── Validation 1: File must not be empty ──────────────────────────────
			if (file == null || file.Length == 0)
				throw new AppException("PDF_EMPTY", "Uploaded file is empty.", 400);

			// ── File size must be under 10 MB ───────────────────────
			//if (file.Length > MaxFileSizeBytes)
			//	throw new AppException("PDF_TOO_LARGE",
			//		$"File size {file.Length / 1024 / 1024} MB exceeds the 10 MB limit.", 400);

			// ──  File extension must be .pdf ─────────────────────────
			var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
			if (extension != ".pdf")
				throw new AppException("INVALID_FILE_TYPE",
					$"Only PDF files are accepted. Got: {extension}", 400);

			// ── Read file into memory ──────────────────────────────────────────────
			using var memoryStream = new MemoryStream();
			await file.CopyToAsync(memoryStream);
			var fileBytes = memoryStream.ToArray();

			// ── Validation 4: Check PDF magic bytes (%PDF-) ───────────────────────
			// Even if someone renames a .jpg to .pdf, this catches it
			// A real PDF always starts with %PDF- regardless of content
			if (fileBytes.Length < PdfMagicBytes.Length ||
				!fileBytes.Take(PdfMagicBytes.Length).SequenceEqual(PdfMagicBytes))
			{
				throw new AppException("NOT_A_PDF",
					"The uploaded file does not appear to be a valid PDF (missing PDF header).", 400);
			}

			// ── Convert to Base64 ──────────────────────────────────────────────────
			var base64 = Convert.ToBase64String(fileBytes);

			SafeLogger.App($"[PDF STORAGE] PDF validated and converted | OriginalName: {file.FileName} | Base64Length: {base64.Length}");

			return base64;
		}
	}
}