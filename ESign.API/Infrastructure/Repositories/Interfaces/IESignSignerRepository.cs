using ESign.API.Infrastructure.Entities;

namespace ESign.API.Infrastructure.Repositories.Interfaces{

public interface IESignRepository
{
	Task<long> InsertTransaction(ESignTransaction transaction);
	Task<ESignTransaction?> GetByDocketId(string docketId);
	Task UpdateTransactionStatus(long transactionId, string status, DateTime? completedAt, DateTime updatedAt , string? signedPdfPath = null);

	
	//Task UpdateSignedPdfPath(long transactionId, string signedPdfPath, DateTime updatedAt);
}

public interface IESignSignerRepository
{
	Task InsertSigner(ESignSigner signer);
	Task<List<ESignSigner>> GetSignersByTransactionId(long transactionId);
	Task UpdateSignerStatus(string signerRefId, string status, DateTime signedAt, DateTime updatedAt);
}
}