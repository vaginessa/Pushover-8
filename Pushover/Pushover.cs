using Pushover.Entities.Reponse;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Pushover
{
    public class Pushover
    {
        private readonly string _baseUrl = "https://api.pushover.net/1";

        private readonly string _apiMessage = "/messages.json";
        private readonly string _apiRateLimit = "/apps/limits.json";

        private readonly int _titleMaxLength = 250;
        private readonly int _messageMaxLength = 1024;

        private readonly string _user;
        private readonly string _token;

        public Pushover(string user, string token)
        {
            _user = user.Trim();
            _token = token.Trim();
        }

        public RateLimit SendMessage((string title, string message) titleMessage)
            => SendMessage(titleMessage.title, titleMessage.message);

        public RateLimit SendMessage(string title, string message) 
            => Task.Run(async () => await SendMessageAsync(title, message)).Result;

        public async Task<RateLimit> SendMessageAsync((string title, string message) titleMessage) 
            => await SendMessageAsync(titleMessage.title, titleMessage.message);

        public async Task<RateLimit> SendMessageAsync(string title, string message)
        {
            var requestUrl = $"{_baseUrl}{_apiMessage}";

            title = title.Substring(0, Math.Min(_titleMaxLength, title.Length));
            message = message.Substring(0, Math.Min(_messageMaxLength - title.Length, message.Length));

            object postData = new
            {
                token = _token,
                user = _user,
                title,
                message,
            };

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(requestUrl),
                Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(postData), Encoding.UTF8, "application/json")
            };

            using var client = new HttpClient();

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();

                return ParseSendMessageResponse(response.Headers);
            }

            return null;
        }

        public RateLimit GetRateLimit()
            => Task.Run(async () => await GetRateLimitAsync()).Result;

        public async Task<RateLimit> GetRateLimitAsync()
        {
            string requestUrl = $"{_baseUrl}{_apiRateLimit}?token={_token}";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(requestUrl)
            };

            using var client = new HttpClient();

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();

                return Newtonsoft.Json.JsonConvert.DeserializeObject<RateLimit>(responseContent);
            }

            return null;
        }

        private RateLimit ParseSendMessageResponse(HttpResponseHeaders headers)
        {
            if (!headers.Any())
                return null;

            if (!int.TryParse(headers.FirstOrDefault(x => string.Equals("X-Limit-App-Limit", x.Key)).Value?.FirstOrDefault() ?? string.Empty, out int limit))
                return null;

            if (!int.TryParse(headers.FirstOrDefault(x => string.Equals("X-Limit-App-Remaining", x.Key)).Value?.FirstOrDefault() ?? string.Empty, out int remaining))
                return null;

            if (!int.TryParse(headers.FirstOrDefault(x => string.Equals("X-Limit-App-Reset", x.Key)).Value?.FirstOrDefault() ?? string.Empty, out int reset))
                return null;

            return new RateLimit
            {
                Limit = limit,
                Remaining = remaining,
                Reset = DateTimeOffset.FromUnixTimeSeconds(reset).LocalDateTime,
                Status = string.Empty
            };
        }
    }
}
