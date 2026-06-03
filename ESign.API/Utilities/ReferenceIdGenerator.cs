using System;

namespace ESign.API.Utilities;

/// <summary>
/// Generates short, prefixed, collision-resistant reference IDs.
///
/// Format:  {PREFIX}{numericPart}{hexPart}
/// Example: REF00127440012ab34    (transaction reference_id)
///          SGN00127440012ab34_s1 (signer_ref_id for signer 1)
///
/// Pattern breakdown (matches C000012744000bb9112 style):
///   - 3-char alpha prefix      : "REF", "SGN", "DOC"
///   - 8-digit zero-padded tick : last 8 digits of Ticks for chronological sort
///   - 8-char hex segment       : first 8 chars of a new GUID (random, collision-resistant)
///
/// Total length: 3 + 8 + 8 = 19 characters  (well within VARCHAR(200))
/// </summary>
public static class ReferenceIdGenerator
{
	// ── Transaction reference_id ─────────────────────────────────────────────
	// Prefix "REF" identifies this as a top-level transaction
	// Example: REF000127440012ab34
	public static string NewReferenceId()
		=> Generate("REF");

	// ── Document reference_id ────────────────────────────────────────────────
	// Prefix "DOC" identifies this as a document reference
	// Example: DOC000127440012ab34
	public static string NewReferenceDocId()
		=> Generate("DOC");

	// ── Signer signer_ref_id ─────────────────────────────────────────────────
	// Prefix "SGN" + suffix "_s1" / "_s2" for easy human reading
	// Example: SGN000127440012ab34_s1
	// signerIndex: 1-based (1 = first signer, 2 = second signer)
	public static string NewSignerRefId(int signerIndex)
		=> $"{Generate("SGN")}_s{signerIndex}";

	// ── Core generator ───────────────────────────────────────────────────────
	// Uses DateTime.UtcNow.Ticks (last 8 digits) + first 8 hex chars of a new GUID
	// The Ticks portion makes IDs chronologically sortable (approx.)
	// The GUID portion ensures uniqueness even within the same millisecond
	private static string Generate(string prefix)
	{
		// Last 8 digits of current UTC ticks — gives a time-ordered component
		var tickPart = (DateTime.UtcNow.Ticks % 100_000_000).ToString("D8");

		// First 8 hex chars of a fresh GUID — collision-resistant random part
		// e.g. Guid "550e8400-e29b-41d4-a716-446655440000" → "550e8400"
		var guidPart = Guid.NewGuid().ToString("N")[..8];
	   
		return $"{prefix}{tickPart}{guidPart}";
	}
}