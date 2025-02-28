using System.Net.Http.Json;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using QuantumVault.Core.Models;

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
            var response = await _client.PostAsJsonAsync("/quantumvault/v1/put", keyValue);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Read_ShouldReturnStoredValue()
        {
            // Arrange
            var keyValue = new { Key = "readKey", Value = "readValue" };
            await _client.PostAsJsonAsync("/quantumvault/v1/put", keyValue);

            // Act
            var response = await _client.GetAsync($"/quantumvault/v1/read/{keyValue.Key}");

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
            await _client.PostAsJsonAsync("/quantumvault/v1/put", keyValue);

            // Act
            var response = await _client.DeleteAsync($"/quantumvault/v1/delete/{keyValue.Key}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Range_ShouldReturnMultipleKeys()
        {
            // Arrange            
            var requestPayload = new KeyValueBatchModel
            {
                KeyValues = new Dictionary<string, string>
                {
                    { "batch1", "value1" },
                    { "batch2", "value2" },
                    { "batch3", "value3" }
                }
            };

            // Act
            await _client.PostAsJsonAsync("/quantumvault/v1/batchput", requestPayload);

            // Act
            var response = await _client.GetAsync("/quantumvault/v1/range?startKey=batch1&endKey=batch3");

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
            var requestPayload = new KeyValueBatchModel
            {
                KeyValues = new Dictionary<string, string>
                {
                    { "batch1", "value1" },
                    { "batch2", "value2" },
                    { "batch3", "value3" }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/quantumvault/v1/batchput", requestPayload);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
