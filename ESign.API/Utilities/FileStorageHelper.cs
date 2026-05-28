//namespace ESign.API.Utilities;

//// FileStorageHelper builds the folder path and file path for storing signed PDFs
//// Folder structure:
////   /esign-storage/
////   └── signed-documents/
////       └── 2026/
////           └── 05/
////               └── week_04/
////                   └── 2026-05-27/
////                       └── docket_6179/
////                           └── signed_document.pdf
////
//// Why this structure?
////   - Year/Month/Week folders → easy to find files, easy to archive old months
////   - Daily folder           → narrow down to exact day quickly
////   - Docket folder          → one folder per transaction, isolated from others
////   - signed_document.pdf    → fixed name inside the docket folder (one signed PDF per docket)
//public static class FileStorageHelper
//{
//	// GetSignedDocumentPath — returns the full absolute file path for a signed PDF
//	// docketId: provider's docket ID e.g. "69e88b993963bec14cd67e7f"
//	// date: the date the signing was completed (used to build year/month/week/day folders)
//	public static string GetSignedDocumentPath(string docketId, DateTime date)
//	{
//		var folder = GetSignedDocumentFolder(docketId, date);
//		return Path.Combine(folder, "signed_document.pdf");
//	}

//	// GetSignedDocumentFolder — returns the directory path (without filename)
//	// Creates it if it doesn't exist — app must have write permission to this location
//	public static string GetSignedDocumentFolder(string docketId, DateTime date)
//	{
//		// Base storage root — next to the app's running directory
//		// In dev:  bin/Debug/net8.0/esign-storage/signed-documents/
//		// In prod: /app/esign-storage/signed-documents/ (or configure via appsettings)
//		var baseDir = Path.Combine(AppContext.BaseDirectory, "esign-storage", "signed-documents");

//		// Year folder: "2026"
//		var year = date.Year.ToString();

//		// Month folder: "05" (zero-padded)
//		var month = date.Month.ToString("D2");

//		// Week folder: "week_04" (ISO week number of the year)
//		var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(date);
//		var week = $"week_{weekNumber:D2}";

//		// Day folder: "2026-05-27"
//		var day = date.ToString("yyyy-MM-dd");

//		// Docket folder: "docket_69e88b99" (first 8 chars of docket ID keep it short)
//		// Using first 8 chars avoids very long folder names while keeping it unique enough
//		var docketFolder = $"docket_{docketId[..Math.Min(8, docketId.Length)]}";

//		// Full path: baseDir/2026/05/week_04/2026-05-27/docket_69e88b99/
//		var fullPath = Path.Combine(baseDir, year, month, week, day, docketFolder);

//		// Create all folders in the path if they don't exist yet
//		// CreateDirectory is safe to call even if the folder already exists
//		Directory.CreateDirectory(fullPath);

//		return fullPath;
//	}

//	// GetRelativePath — strips the base directory prefix to store a short relative path in DB
//	// We store relative path (not absolute) so the app works after moving to a different server
//	// Example: "esign-storage/signed-documents/2026/05/week_04/2026-05-27/docket_69e88b99/signed_document.pdf"
//	public static string GetRelativePath(string fullPath)
//	{
//		var baseDir = AppContext.BaseDirectory;

//		// Remove base directory prefix — if the path starts with it, strip it
//		if (fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
//			return fullPath[baseDir.Length..].TrimStart(Path.DirectorySeparatorChar);

//		return fullPath;
//	}
//}





// for storing in C - drive 


namespace ESign.API.Utilities;

public static class FileStorageHelper
{
	// Root storage location in C drive
	// Final structure:
	// C:\ESign-Storage\signed-documents\2026\05\week_4\2026-05-28\docket_xxxx\
	private static readonly string BasePath =
		Path.Combine(@"C:\ESign-Storage\signed-documents\");

	public static string GetSignedDocumentPath(string docketId, DateTime date)
	{
		var folder = GetSignedDocumentFolder(docketId, date);

		return Path.Combine(folder, "signed_document.pdf");
	}

	public static string GetSignedDocumentFolder(string docketId, DateTime date)
	{
		// Year folder
		var year = date.Year.ToString();

		// Month folder
		var month = date.Month.ToString("D2");

		// Week folder
		var week = $"week_{GetWeekOfMonth(date)}";

		// Day folder
		var day = date.ToString("yyyy-MM-dd");

		// Docket folder
		var docketFolder =
			$"docket_{docketId[..Math.Min(8, docketId.Length)]}";

		// Full path creation
		var fullPath = Path.Combine(
			BasePath,
			year,
			month,
			week,
			day,
			docketFolder
		);

		// Create directories if not exists
		Directory.CreateDirectory(fullPath);

		return fullPath;
	}

	public static string GetRelativePath(string fullPath)
	{
		// Remove C:\ESign-Storage\ prefix
		if (fullPath.StartsWith(BasePath, StringComparison.OrdinalIgnoreCase))
			return fullPath[BasePath.Length..]
				.TrimStart(Path.DirectorySeparatorChar);

		return fullPath;
	}

	private static int GetWeekOfMonth(DateTime date)
	{
		var firstDay = new DateTime(date.Year, date.Month, 1);

		return ((date.Day + (int)firstDay.DayOfWeek - 1) / 7) + 1;
	}
}