//namespace ESign.API.Utilities;

//public static class LogPathHelper
//{
//	public static string GetPath(string logType)
//	{

//		var baseDir = AppContext.BaseDirectory;

//		// Build full path: <baseDir>/logs/<LogType>.log
//		return Path.Combine(baseDir, "logs", $"{logType}.log");
//	}
//}




namespace ESign.API.Utilities;

public static class LogPathHelper
{
	// Root log storage path in C drive
	private static readonly string BasePath =
		Path.Combine(@"C:\ESign_logs\");

	public static string GetPath(string logType)
	{
		var now = DateTime.Now;

		// Year folder
		var year = now.Year.ToString();

		// Month folder
		var month = now.Month.ToString("D2");

		// Week folder
		var week = $"week_{GetWeekOfMonth(now)}";

		// Day folder
		var day = now.ToString("yyyy-MM-dd");

		// Full folder path
		// Example:
		// C:\ESign_logs\2026\05\week_4\2026-05-28\Application
		var folder = Path.Combine(
			BasePath,
			year,
			month,
			week,
			day,
			logType
		);

		// Create directory if not exists
		Directory.CreateDirectory(folder);

		// Final log file
		return Path.Combine(
			folder,
			$"esign-{logType.ToLower()}-{now:yyyy-MM-dd}.log"
		);
	}

	private static int GetWeekOfMonth(DateTime date)
	{
		var firstDay = new DateTime(date.Year, date.Month, 1);

		return ((date.Day + (int)firstDay.DayOfWeek - 1) / 7) + 1;
	}
}