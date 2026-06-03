
// for storing in C - drive 
namespace ESign.API.Utilities;

public static class FileStorageHelper
{
	// Root storage location in C drive
	// Final structure:
	// C:\ESign-Storage\signed-documents\2026\05\week_4\2026-05-28\docket_xxxx\
	private static readonly string BasePath = Path.Combine(@"C:\ESign-Storage\signed-documents\");

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

	// getting full path 1/06
	public static string GetFullPath(string relativePath)
	{
		return Path.Combine(BasePath, relativePath);
	}



	private static int GetWeekOfMonth(DateTime date)
	{
		var firstDay = new DateTime(date.Year, date.Month, 1);

		return ((date.Day + (int)firstDay.DayOfWeek - 1) / 7) + 1;
	}
}