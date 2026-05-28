
using ESign.API.Infrastructure.Entities;

namespace ESign.API.Infrastructure.Repositories.Interfaces;

// IESignMasterRepository — reads provider config from DB
// Called on startup (cache warmup) and as fallback when cache is empty
public interface IESignMasterRepository
{
	// GetAllActiveProviders — returns all rows from esign_providers where is_active = true
	// Ordered by priority ASC (priority 1 is tried first)
	Task<List<ESignProviderConfig>> GetAllActiveProviders();
}