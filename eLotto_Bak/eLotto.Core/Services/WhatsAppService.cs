using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace eLotto.Core.Services
{
    public interface IWhatsAppService
    {
        Task<bool> SendTextAsync(string whatsApp, string message);
        Task<bool> SendDocumentAsync(
            string whatsApp,
            string fileName,
            byte[] document,
            string caption,
            CancellationToken cancellationToken = default);
    }

    public class UltraMsgWhatsAppService : IWhatsAppService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UltraMsgWhatsAppService> _logger;

        public UltraMsgWhatsAppService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<UltraMsgWhatsAppService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendTextAsync(string whatsApp, string message)
        {
            try
            {
                var instance = GetActiveInstance();
                if (instance is null || !await IsAuthenticatedAsync(instance, CancellationToken.None))
                    return false;

                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = instance.Token,
                    ["to"] = NormalizeWhatsApp(whatsApp),
                    ["body"] = message
                });
                using var response = await _httpClient.PostAsync(
                    $"https://api.ultramsg.com/{Uri.EscapeDataString(instance.InstanceId)}/messages/chat",
                    content);
                if (!response.IsSuccessStatusCode)
                    return false;

                var result = await response.Content.ReadFromJsonAsync<UltraMsgSendResponse>();
                return result?.Sent == "true" || result?.Id > 0;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "No fue posible enviar el mensaje de WhatsApp a {WhatsApp}.", MaskWhatsApp(whatsApp));
                return false;
            }
        }

        public async Task<bool> SendDocumentAsync(
            string whatsApp,
            string fileName,
            byte[] document,
            string caption,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentNullException.ThrowIfNull(document);

            var instance = GetActiveInstance();
            if (instance is null || !await IsAuthenticatedAsync(instance, cancellationToken))
                return false;

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["token"] = instance.Token,
                ["to"] = NormalizeWhatsApp(whatsApp),
                ["filename"] = fileName,
                ["document"] = Convert.ToBase64String(document),
                ["caption"] = caption
            });
            using var response = await _httpClient.PostAsync(
                $"https://api.ultramsg.com/{Uri.EscapeDataString(instance.InstanceId)}/messages/document",
                content,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<UltraMsgSendResponse>(cancellationToken);
            return result?.Sent == "true" || result?.Id > 0;
        }

        private ActiveInstance GetActiveInstance()
        {
            var instance = _configuration
                .GetSection("UltraMsg:Instances")
                .GetChildren()
                .FirstOrDefault(configuredInstance =>
                    bool.TryParse(configuredInstance["IsActive"], out var isActive) && isActive);

            var instanceId = instance?["InstanceId"];
            var token = instance?["Token"];
            return string.IsNullOrWhiteSpace(instanceId) || string.IsNullOrWhiteSpace(token)
                ? null
                : new(instanceId, token);
        }

        private async Task<bool> IsAuthenticatedAsync(
            ActiveInstance instance,
            CancellationToken cancellationToken)
        {
            var status = await _httpClient.GetFromJsonAsync<UltraMsgInstanceResponse>(
                $"https://api.ultramsg.com/{Uri.EscapeDataString(instance.InstanceId)}/instance/status?token={Uri.EscapeDataString(instance.Token)}",
                cancellationToken);
            return string.Equals(
                status?.Status?.AccountStatus?.Status,
                "authenticated",
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeWhatsApp(string whatsApp)
        {
            var digits = new string(whatsApp.Where(char.IsDigit).ToArray());
            return $"+{digits}";
        }

        private static string MaskWhatsApp(string whatsApp)
        {
            var digits = new string((whatsApp ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length <= 4 ? "****" : $"******{digits[^4..]}";
        }

        private sealed class UltraMsgInstanceResponse
        {
            [JsonPropertyName("status")]
            public UltraMsgStatus Status { get; set; }
        }

        private sealed class UltraMsgStatus
        {
            [JsonPropertyName("accountStatus")]
            public UltraMsgAccountStatus AccountStatus { get; set; }
        }

        private sealed class UltraMsgAccountStatus
        {
            [JsonPropertyName("status")]
            public string Status { get; set; }
        }

        private sealed class UltraMsgSendResponse
        {
            [JsonPropertyName("sent")]
            public string Sent { get; set; }

            [JsonPropertyName("id")]
            public int Id { get; set; }
        }

        private sealed record ActiveInstance(string InstanceId, string Token);
    }
}
