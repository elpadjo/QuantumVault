using System.Net.Http.Json;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace QuantumVault.Tests
{
    public class KeyValueStoreTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;        

        public KeyValueStoreTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Disable HTTPS redirection in tests
                    services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
                    {
                        options.HttpsPort = null;
                    });
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false // Prevent unnecessary redirects in tests
            });
        }

        [Fact]
        public async Task Put_ShouldStoreKeyValue()
        {
            // Arrange
            var keyValue = new { Key = "testKey", Value = "testValue" };

            // Act
            var response = await _client.PostAsJsonAsync("/quantumvault/put", keyValue);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact] //FAILS FOR NOW
        public async Task Read_ShouldReturnStoredValue()
        {
            // Arrange
            var keyValue = new { Key = "readKey", Value = "readValue" };
            await _client.PostAsJsonAsync("/quantumvault/put", keyValue);

            // Act
            var response = await _client.GetAsync($"/quantumvault/read?key={keyValue.Key}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            Assert.Equal("readValue", content["value"]);
        }

        [Fact]
        public async Task Delete_ShouldRemoveKey()
        {
            // Arrange
            var keyValue = new { Key = "deleteKey", Value = "deleteValue" };
            await _client.PostAsJsonAsync("/quantumvault/put", keyValue);

            // Act
            var response = await _client.DeleteAsync($"/quantumvault/delete?key={keyValue.Key}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Range_ShouldReturnMultipleKeys()
        {
            // Arrange
            var keyValues = new List<object>
        {
            new { Key = "A", Value = "1" },
            new { Key = "B", Value = "2" },
            new { Key = "C", Value = "3" }
        };

            foreach (var kv in keyValues)
            {
                await _client.PostAsJsonAsync("/quantumvault/put", kv);
            }

            // Act
            var response = await _client.GetAsync("/quantumvault/range?startKey=A&endKey=C");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            Assert.NotNull(content);
            Assert.Equal(3, content.Count);
        }

        [Fact]
        public async Task BatchPut_ShouldStoreMultipleKeys()
        {
            // Arrange
            var keyValues = new Dictionary<string, string>
        {
            { "batch1", "value1" },
            { "batch2", "value2" },
            { "batch3", "value3" }
        };

            // Act
            var response = await _client.PostAsJsonAsync("/quantumvault/batchput", keyValues);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
