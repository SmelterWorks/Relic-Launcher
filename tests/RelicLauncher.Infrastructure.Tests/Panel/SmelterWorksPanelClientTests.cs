using RelicLauncher.Core.Models;
using RelicLauncher.Infrastructure.Endpoints;
using RelicLauncher.Infrastructure.Panel;
using Xunit;

namespace RelicLauncher.Infrastructure.Tests.Panel;

public sealed class SmelterWorksPanelClientTests
{
    [Fact]
    public async Task GetMyServersAsync_parses_panel_payload()
    {
        var handler = new StubHandler("""
            {"schemaVersion":1,"servers":[{"uuid":"abc","name":"Home","type":"byos","status":"pending","connect_address":"127.0.0.1:42420","daemon_online":true}]}
            """);
        var endpoints = new EndpointProvider();
        endpoints.Apply(new EndpointSettings { PanelApiBaseUrl = "https://panel.example.test" });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://panel.example.test") };
        using var client = new SmelterWorksPanelClient(endpoints, Microsoft.Extensions.Logging.Abstractions.NullLogger<SmelterWorksPanelClient>.Instance, http);

        var result = await client.GetMyServersAsync("token");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("Home", result.Value![0].Name);
        Assert.True(result.Value![0].DaemonOnline);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;

        public StubHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_json, System.Text.Encoding.UTF8, "application/json"),
            });
    }
}
