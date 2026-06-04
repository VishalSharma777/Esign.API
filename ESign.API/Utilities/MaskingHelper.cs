using System.Text.RegularExpressions;

namespace ESign.API.Utilities;


public static class MaskingHelper
{

	private static readonly Regex MobileRegex = new(@"\b(\d{4})\d{6}\b", RegexOptions.Compiled);


	public static string MaskSensitiveData(string? input)
	{
		if (string.IsNullOrEmpty(input)) return string.Empty;
		return MobileRegex.Replace(input, "$1XXXXXX");
	}
}