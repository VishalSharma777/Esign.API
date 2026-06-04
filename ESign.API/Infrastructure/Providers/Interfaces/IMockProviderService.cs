using ESign.API.Application.DTOs.Common;
using ESign.API.Application.DTOs.Request;
using ESign.API.Infrastructure.Entities;

namespace ESign.API.Infrastructure.Providers.Interfaces;


public interface IMockProviderService
{
	Task<(ESignCommonResponseDto response, string rawJson)> CreateESignAsync(
		ESignRequest request,
		ESignProviderConfig providerConfig,
		string correlationId);
}