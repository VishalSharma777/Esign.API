using System.Text.RegularExpressions;

namespace ESign.API.Utilities;


public static class ValidationHelper
{
	private static readonly Regex MobileRegex = new(@"^[6-9]\d{9}$", RegexOptions.Compiled);

	private static readonly Regex EmailRegex = new(
		@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	public static bool IsValidMobile(string? mobile)
	{
		if (string.IsNullOrWhiteSpace(mobile)) return false;
		return MobileRegex.IsMatch(mobile.Trim());
	}

	public static bool IsValidEmail(string? email)
	{
		if (string.IsNullOrWhiteSpace(email)) return true;   // Email is optional — null is OK
		return EmailRegex.IsMatch(email.Trim());
	}

	public static bool IsValidReferenceId(string? referenceId)
	{
		return !string.IsNullOrWhiteSpace(referenceId);
	}
}