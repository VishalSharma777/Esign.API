namespace ESign.API.Utilities
{

	public static class LogPathHelper
	{
		// Root log storage path in C drive
		private static readonly string BasePath = Path.Combine(@"C:\ESign_logs\");

		public static string GetPath(string logType)
		{
			var now = DateTime.Now;
			var year = now.Year.ToString();
			var month = now.Month.ToString("D2");
			var week = $"week_{GetWeekOfMonth(now)}";
			var day = now.ToString("yyyy-MM-dd");
			var folder = Path.Combine(
				BasePath,
				year,
				month,
				week,
				day,
				logType
			);

			Directory.CreateDirectory(folder);

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
}