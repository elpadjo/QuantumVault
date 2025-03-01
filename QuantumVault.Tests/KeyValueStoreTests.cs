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
                    services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
                    {
                        options.HttpsPort = null;
                    });
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        // Basic Functionality Tests
        [Fact]
        public async Task Put_ShouldStoreKeyValue()
        {
            var keyValue = new { Key = "testKey", Value = "testValue" };
            var response = await _client.PostAsJsonAsync("/quantumvault/v1/put", keyValue);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Read_ShouldReturnStoredValue()
        {
            var keyValue = new { Key = "readKey", Value = "readValue" };
            await _client.PostAsJsonAsync("/quantumvault/v1/put", keyValue);
            var response = await _client.GetAsync($"/quantumvault/v1/read/{keyValue.Key}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            Assert.Equal("readValue", content["value"]);
        }

        [Fact]
        public async Task Delete_ShouldRemoveKey()
        {
            var keyValue = new { Key = "deleteKey", Value = "deleteValue" };
            await _client.PostAsJsonAsync("/quantumvault/v1/put", keyValue);
            var response = await _client.DeleteAsync($"/quantumvault/v1/delete/{keyValue.Key}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ReadKeyRange_ShouldReturnKeysInRange()
        {
            var requestPayload = new KeyValueBatchModel
            {
                KeyValues = new Dictionary<string, string>
                {
                    { "batch1", "value1" },
                    { "batch2", "value2" },
                    { "batch3", "value3" }
                }
            };
            await _client.PostAsJsonAsync("/quantumvault/v1/batchput", requestPayload);
            var response = await _client.GetAsync("/quantumvault/v1/range?startKey=batch1&endKey=batch3");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task BatchPut_ShouldStoreMultipleKeys()
        {
            var requestPayload = new KeyValueBatchModel
            {
                KeyValues = new Dictionary<string, string>
                {
                    { "batch1", "value1" },
                    { "batch2", "value2" },
                    { "batch3", "value3" }
                }
            };
            var response = await _client.PostAsJsonAsync("/quantumvault/v1/batchput", requestPayload);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Edge Case Tests
        [Fact]
        public async Task Read_NonExistentKey_ShouldReturnNotFound()
        {
            var response = await _client.GetAsync("/quantumvault/v1/read/nonExistentKey");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Put_EmptyKeyOrValue_ShouldReturnBadRequest()
        {
            var keyValue = new { Key = "", Value = "" };
            var response = await _client.PostAsJsonAsync("/quantumvault/v1/put", keyValue);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // Performance & Load Tests
        [Fact]
        public async Task HighThroughput_ShouldHandleManyWrites()
        {
            var tasks = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                var keyValue = new { Key = $"key{i}", Value = $"value{i}" };
                tasks.Add(_client.PostAsJsonAsync("/quantumvault/v1/put", keyValue));
            }
            await Task.WhenAll(tasks);
            foreach (var response in tasks)
            {
                Assert.Equal(HttpStatusCode.OK, response.Result.StatusCode);
            }
        }

        [Fact]
        public async Task ConcurrentReadWrite_ShouldNotFail()
        {
            var writeTasks = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                var keyValue = new { Key = $"concurrentKey{i}", Value = $"value{i}" };
                writeTasks.Add(_client.PostAsJsonAsync("/quantumvault/v1/put", keyValue));
            }
            await Task.WhenAll(writeTasks);

            var readTasks = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 50; i++)
            {
                readTasks.Add(_client.GetAsync($"/quantumvault/v1/read/concurrentKey{i}"));
            }
            await Task.WhenAll(readTasks);

            foreach (var response in readTasks)
            {
                Assert.Equal(HttpStatusCode.OK, response.Result.StatusCode);
            }
        }
    }
}
