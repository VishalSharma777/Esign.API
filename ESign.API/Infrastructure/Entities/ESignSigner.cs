using Newtonsoft.Json;

namespace ESign.API.Infrastructure.Entities;

// ESignSigner maps to one row in the esign_signers table
// One transaction has 2 signer rows (signer 1 and signer 2)
// Signer status flows: NOT_SIGNED → SIGNED
public class ESignSigner
{
	public long Id { get; set; }
	public long TransactionId { get; set; }
	public string? SignerRefId { get; set; }
	public string? SignerId { get; set; }
	public string SignerName { get; set; } = string.Empty;
	public string? SignerEmail { get; set; }
	public string SignerMobile { get; set; } = string.Empty;
	public string SignerStatus { get; set; } = string.Empty;
	public string? InvitationLink { get; set; }
	public DateTime? SignedAt { get; set; }
	public int PageNumber { get; set; }

	// Stores all 4 signature coordinates as a single JSONB column in DB
	// Format in DB: { "x1": 20, "y1": 20, "x2": 120, "y2": 60 }
	// Replaces the old separate position_x and position_y columns
	// Dapper reads the JSONB as a string — we deserialize it into SignaturePositionData
	public string? SignaturePosition { get; set; }

	public DateTime CreatedAt { get; set; }
	public DateTime? UpdatedAt { get; set; }

	// Convenience property — deserializes SignaturePosition JSON string into a typed object
	// Use this anywhere in the app instead of parsing the JSON manually
	// Returns null if SignaturePosition was not set
	[JsonIgnore]
	public SignaturePositionData? Position =>
		string.IsNullOrEmpty(SignaturePosition)
			? null
			: JsonConvert.DeserializeObject<SignaturePositionData>(SignaturePosition);
}

// SignaturePositionData — typed representation of the JSONB stored in DB
// Matches the JSON keys: { "x1": 20, "y1": 20, "x2": 120, "y2": 60 }
public class SignaturePositionData
{
	[JsonProperty("x1")] public int X1 { get; set; }
	[JsonProperty("y1")] public int Y1 { get; set; }
	[JsonProperty("x2")] public int X2 { get; set; }
	[JsonProperty("y2")] public int Y2 { get; set; }
}