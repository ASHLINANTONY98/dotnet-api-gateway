using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Polly.Timeout;

namespace ESS.Infrastructure.Services
{
    public class HttpForwardingService(
        HttpClient client,
        IHttpContextAccessor httpContextAccessor,
        ILogger<HttpForwardingService> logger)
    {
        private readonly HttpClient _client = client;
        private readonly IHttpContextAccessor _httpContextAccessor =
            httpContextAccessor;
        private readonly ILogger<HttpForwardingService> _logger = logger;

        private string? GetToken()
        {
            var authHeader = _httpContextAccessor.HttpContext?
                .Request.Headers["Authorization"].ToString();

            // No Authorization header: allow anonymous forwarding.
            if (string.IsNullOrWhiteSpace(authHeader))
            {
                return null;
            }

            // Reject malformed Authorization headers.
            if (!AuthenticationHeaderValue.TryParse(
                    authHeader,
                    out var parsedHeader))
            {
                throw new UnauthorizedAccessException(
                    "Invalid Authorization header.");
            }

            // Only forward Bearer tokens.
            if (!string.Equals(
                    parsedHeader.Scheme,
                    "Bearer",
                    StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(parsedHeader.Parameter)
                || parsedHeader.Parameter.Any(char.IsWhiteSpace))
            {
                throw new UnauthorizedAccessException(
                    "Invalid Authorization header.");
            }

            return parsedHeader.Parameter;
        }

        private string? GetCorrelationId()
        {
            var correlationId = _httpContextAccessor.HttpContext?
                .Request.Headers["X-Correlation-ID"].ToString();

            if (!Guid.TryParseExact(correlationId, "D", out var parsedId))
            {
                return null;
            }

            return parsedId.ToString("D");
        }

        public async Task<T?> GetAsync<T>(
            string endpoint,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    endpoint);

                var token = GetToken();

                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                }

                var correlationId = GetCorrelationId();

                if (!string.IsNullOrEmpty(correlationId))
                {
                    request.Headers.Add(
                        "X-Correlation-ID",
                        correlationId);
                }

                using var response = await _client.SendAsync(
                    request,
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException(
                        "Private API returned Unauthorized.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Private API error: {response.StatusCode}",
                        null,
                        response.StatusCode);
                }

                return await response.Content.ReadFromJsonAsync<T>(
                    cancellationToken: cancellationToken);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(
                    ex,
                    "[CIRCUIT OPEN] {Endpoint}",
                    endpoint);

                throw;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "HTTP request cancelled by caller for {Endpoint}",
                    endpoint);

                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(
                    ex,
                    "[TIMEOUT] {Endpoint}",
                    endpoint);

                throw;
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogError(
                    ex,
                    "[POLLY TIMEOUT] {Endpoint}",
                    endpoint);

                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    ex,
                    "HTTP forwarding failed | StatusCode: {StatusCode} | " +
                    "Endpoint: {Endpoint} | CorrelationId: {CorrelationId}",
                    ex.StatusCode is null ? null : (int)ex.StatusCode,
                    endpoint,
                    GetCorrelationId());

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[EXCEPTION] {Endpoint}",
                    endpoint);

                throw;
            }
        }

        public async Task<T?> PostAsync<T>(
            string endpoint,
            object payload,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    endpoint)
                {
                    Content = JsonContent.Create(payload)
                };

                var token = GetToken();

                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                }

                var correlationId = GetCorrelationId();

                if (!string.IsNullOrEmpty(correlationId))
                {
                    request.Headers.Add(
                        "X-Correlation-ID",
                        correlationId);
                }

                using var response = await _client.SendAsync(
                    request,
                    cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException(
                        "Private API returned Unauthorized.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Private API error: {response.StatusCode}",
                        null,
                        response.StatusCode);
                }
                return await response.Content.ReadFromJsonAsync<T>(
                    cancellationToken: cancellationToken);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(
                    ex,
                    "[CIRCUIT OPEN] {Endpoint}",
                    endpoint);

                throw;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "HTTP request cancelled by caller for {Endpoint}",
                    endpoint);

                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(
                    ex,
                    "[TIMEOUT] {Endpoint}",
                    endpoint);

                throw;
            }
            catch (TimeoutRejectedException ex)
            {
                _logger.LogError(
                    ex,
                    "[POLLY TIMEOUT] {Endpoint}",
                    endpoint);

                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    ex,
                    "HTTP forwarding failed | StatusCode: {StatusCode} | " +
                    "Endpoint: {Endpoint} | CorrelationId: {CorrelationId}",
                    ex.StatusCode is null ? null : (int)ex.StatusCode,
                    endpoint,
                    GetCorrelationId());

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[EXCEPTION] {Endpoint}",
                    endpoint);

                throw;
            }
        }
    }
}