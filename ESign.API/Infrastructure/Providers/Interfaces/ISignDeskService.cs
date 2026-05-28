

using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Infrastructure.Entities;

namespace ESign.API.Infrastructure.Providers.Interfaces;

// ISignDeskService — contract for calling the SignDesk e-sign provider
// Only one implementation exists today: SignDeskProvider
// Having an interface allows easy mocking in tests and future provider swaps
public interface ISignDeskService
{
	// CreateESignAsync — calls SignDesk's /api/sandbox/signRequest endpoint
	// Takes the normalized ESignRequest (our internal DTO) and the provider config from DB
	// Returns ESignCommonResponseDto — normalized provider response
	Task<(ESignCommonResponseDto response, string rawJson)> CreateESignAsync(
		ESignRequest request,
		ESignProviderConfig providerConfig,
		string correlationId);
}