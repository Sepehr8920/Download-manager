using System;
using System.Net;
using System.Net.Http;

namespace SepDownloadManager
{
    // Shared HttpClient for all downloads (much faster)
    public static class HttpClientProvider
    {
        static HttpClient _client;

        public static HttpClient Get()
        {
            if (_client != null) return _client;

            // Allow up to 32 simultaneous connections
            ServicePointManager.DefaultConnectionLimit = 32;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 32,
                AutomaticDecompression = DecompressionMethods.None,
                UseProxy = false
            };

            _client = new HttpClient(handler);
            _client.Timeout = TimeSpan.FromMinutes(30);

            // Some servers reject requests without a User-Agent
            _client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) SepDownloadManager/2.0");

            return _client;
        }
    }
}
