using System;

namespace ESign.API.Utilities;


public static class ReferenceIdGenerator
{
	
	public static string NewReferenceId() => Generate("REF");
	public static string NewReferenceDocId() => Generate("DOC");
	public static string NewSignerRefId(int signerIndex) => $"{Generate("SGN")}_s{signerIndex}";
	private static string Generate(string prefix)
	{

		var tickPart = (DateTime.UtcNow.Ticks % 100_000_000).ToString("D8");
		var guidPart = Guid.NewGuid().ToString("N")[..8];
	   
		return $"{prefix}{tickPart}{guidPart}";
	}
}