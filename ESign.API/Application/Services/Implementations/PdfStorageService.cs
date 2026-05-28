using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;
using ESign.API.Utilities;

namespace ESign.API.Application.Services.Implementations;

// PdfStorageService handles saving signed PDFs to disk and validating uploaded PDFs
// Registered as Scoped in Program.cs
public class PdfStorageService : IPdfStorageService

{	// PDF magic bytes — every valid PDF file starts with "%PDF-" in ASCII
	// This is more reliable than just checking the file extension
	// "%PDF-" in bytes = 0x25 0x50 0x44 0x46 0x2D
	private static readonly byte[] PdfMagicBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D };

	// Max allowed PDF size for upload: 10 MB
	// SignDesk accepts large PDFs but we limit here to prevent abuse
	private const long MaxFileSizeBytes = 10 * 1024 * 1024;   // 10 MB

	// SaveSignedPdfAsync — converts Base64 → bytes → saves to disk
	// Called from WebhookService after all signers complete
	// Returns the relative path stored in esign_transactions.signed_pdf_path
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

		// Build the full file path using the folder structure helper
		// e.g. esign-storage/signed-documents/2026/05/week_04/2026-05-27/docket_69e88b99/signed_document.pdf
		var fullPath = FileStorageHelper.GetSignedDocumentPath(docketId, completedAt);

		// Write bytes to disk — overwrites if file already exists (idempotent)
		await File.WriteAllBytesAsync(fullPath, pdfBytes);

		// Get relative path for DB storage
		var relativePath = FileStorageHelper.GetRelativePath(fullPath);

		SafeLogger.App($"[PDF STORAGE] Signed PDF saved | Path: {relativePath} | Size: {pdfBytes.Length} bytes");

		return relativePath;
	}

	// ValidateAndConvertToBase64Async — validates the uploaded PDF file and returns Base64
	// Called from POST /api/v1/esign/upload-pdf
	// Three validations:
	//   1. File size must be under 10 MB
	//   2. Extension must be .pdf
	//   3. File must start with %PDF- magic bytes (actual PDF content check)
	public async Task<string> ValidateAndConvertToBase64Async(IFormFile file)
	{
		SafeLogger.App($"[PDF STORAGE] ValidateAndConvert START | FileName: {file.FileName} | Size: {file.Length}");

		// ── Validation 1: File must not be empty ──────────────────────────────
		if (file == null || file.Length == 0)
			throw new AppException("PDF_EMPTY", "Uploaded file is empty.", 400);

		// ── Validation 2: File size must be under 10 MB ───────────────────────
		if (file.Length > MaxFileSizeBytes)
			throw new AppException("PDF_TOO_LARGE",
				$"File size {file.Length / 1024 / 1024} MB exceeds the 10 MB limit.", 400);

		// ── Validation 3: File extension must be .pdf ─────────────────────────
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