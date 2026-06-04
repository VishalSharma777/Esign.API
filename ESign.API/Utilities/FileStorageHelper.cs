
// for storing in C - drive 
namespace ESign.API.Utilities;

public static class FileStorageHelper
{

	private static readonly string BasePath = Path.Combine(@"C:\ESign-Storage\signed-documents\");

	public static string GetSignedDocumentPath(string docketId, DateTime date)
	{
		var folder = GetSignedDocumentFolder(docketId, date);

		return Path.Combine(folder, "signed_document.pdf");
	}

	public static string GetSignedDocumentFolder(string docketId, DateTime date)
	{
		
		var year = date.Year.ToString();
		var month = date.Month.ToString("D2");
		var week = $"week_{GetWeekOfMonth(date)}";
		var day = date.ToString("yyyy-MM-dd");
		var docketFolder = $"docket_{docketId[..Math.Min(8, docketId.Length)]}";

		// Full path creation
		var fullPath = Path.Combine(
			BasePath,
			year,
			month,
			week,
			day,
			docketFolder
		);

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
	//public static string GetFullPath(string relativePath)
	//{
	//	return Path.Combine(BasePath, relativePath);
	//}



	private static int GetWeekOfMonth(DateTime date)
	{
		var firstDay = new DateTime(date.Year, date.Month, 1);

		return ((date.Day + (int)firstDay.DayOfWeek - 1) / 7) + 1;
	}
}