using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace HRMS.Infrastructure.Services
{
    public class HttpForwardingService(HttpClient client, IHttpContextAccessor httpContextAccessor)
    {
        private readonly HttpClient _client = client;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        private string? GetToken()
        {
            var authHeader = _httpContextAccessor.HttpContext?
                .Request.Headers["Authorization"].ToString();

            return string.IsNullOrEmpty(authHeader)
                ? null
                : authHeader.Replace("Bearer ", "");
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            var token = GetToken();
            if (!string.IsNullOrEmpty(token))
            {
                _client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _client.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }

        public async Task<T?> PostAsync<T>(string endpoint, object payload)
        {
            var token = GetToken();
            if (!string.IsNullOrEmpty(token))
            {
                _client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _client.PostAsJsonAsync(endpoint, payload);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
    }
}
