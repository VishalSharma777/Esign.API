//using ESign.API.Infrastructure.Entities;

//namespace ESign.API.Infrastructure.Repositories.Interfaces;

//// IESignRepository — all DB operations on esign_transactions table
//public interface IESignRepository
//{
//	// InsertTransaction — inserts a new row into esign_transactions
//	// Returns the auto-generated primary key (id)
//	Task<long> InsertTransaction(ESignTransaction transaction);

//	// GetByDocketId — finds a transaction by the provider's docket_id
//	// Used in webhook handler to find which transaction the webhook is about
//	Task<ESignTransaction?> GetByDocketId(string docketId);

//	// UpdateTransactionStatus — updates status + completed_at + updated_at
//	// Called from webhook handler when all signers have signed
//	Task UpdateTransactionStatus(long transactionId, string status, DateTime? completedAt, DateTime updatedAt);
//}

//// IESignSignerRepository — all DB operations on esign_signers table
//public interface IESignSignerRepository
//{
//	// InsertSigner — inserts one signer row into esign_signers
//	// Called twice (once per signer) after transaction is created
//	Task InsertSigner(ESignSigner signer);

//	// GetSignersByTransactionId — returns both signers for a transaction
//	// Used in webhook handler to check if all signers have signed
//	Task<List<ESignSigner>> GetSignersByTransactionId(long transactionId);

//	// UpdateSignerStatus — marks one signer as SIGNED and sets signed_at timestamp
//	// Called from webhook handler for each completed signer
//	Task UpdateSignerStatus(string signerRefId, string status, DateTime signedAt, DateTime updatedAt);
//}







using ESign.API.Infrastructure.Entities;

namespace ESign.API.Infrastructure.Repositories.Interfaces{

// IESignRepository — all DB operations on esign_transactions table
public interface IESignRepository
{
	Task<long> InsertTransaction(ESignTransaction transaction);
	Task<ESignTransaction?> GetByDocketId(string docketId);
	Task UpdateTransactionStatus(long transactionId, string status, DateTime? completedAt, DateTime updatedAt);

	// UpdateSignedPdfPath — saves the relative file path of the signed PDF to DB
	// Called from WebhookService after PdfStorageService saves the file to disk
	Task UpdateSignedPdfPath(long transactionId, string signedPdfPath, DateTime updatedAt);
}

// IESignSignerRepository — all DB operations on esign_signers table
public interface IESignSignerRepository
{
	Task InsertSigner(ESignSigner signer);
	Task<List<ESignSigner>> GetSignersByTransactionId(long transactionId);
	Task UpdateSignerStatus(string signerRefId, string status, DateTime signedAt, DateTime updatedAt);
}
}