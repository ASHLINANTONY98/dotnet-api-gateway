using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ESS.Infrastructure.Services
{
    public class HttpForwardingService(
        HttpClient client,
        IHttpContextAccessor httpContextAccessor,
        ILogger<HttpForwardingService> logger)
    {
        private readonly HttpClient _client = client;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly ILogger<HttpForwardingService> _logger = logger;

        private string? GetToken()
        {
            var authHeader = _httpContextAccessor.HttpContext?
                .Request.Headers["Authorization"].ToString();


            return string.IsNullOrEmpty(authHeader)
                ? null
                : authHeader.Replace("Bearer ", "");
        }


        private string? GetCorrelationId()
        {
            return _httpContextAccessor.HttpContext?
                .Request.Headers["X-Correlation-ID"].ToString();
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

                var token = GetToken();
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                }

                var correlationId = GetCorrelationId();
                if (!string.IsNullOrEmpty(correlationId))
                {
                    request.Headers.Add("X-Correlation-ID", correlationId);
                }

                var response = await _client.SendAsync(request);

                //response.EnsureSuccessStatusCode();
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var error = await response.Content.ReadAsStringAsync();

                    throw new UnauthorizedAccessException(error);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Private API error: {response.StatusCode}");
                }

                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "[CIRCUIT OPEN] {Endpoint}", endpoint);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "[TIMEOUT] {Endpoint}", endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EXCEPTION] {Endpoint}", endpoint);
                throw;
            }
        }

        public async Task<T?> PostAsync<T>(string endpoint, object payload)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
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
                    request.Headers.Add("X-Correlation-ID", correlationId);
                }

                var response = await _client.SendAsync(request);

                //response.EnsureSuccessStatusCode();
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var error = await response.Content.ReadAsStringAsync();

                    throw new UnauthorizedAccessException(error);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Private API error: {response.StatusCode}");
                }

                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(ex, "[CIRCUIT OPEN] {Endpoint}", endpoint);
                throw;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "[TIMEOUT] {Endpoint}", endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EXCEPTION] {Endpoint}", endpoint);
                throw;
            }
        }
    }
}