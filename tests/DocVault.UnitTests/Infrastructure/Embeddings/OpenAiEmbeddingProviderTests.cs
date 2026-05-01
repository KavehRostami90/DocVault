using System.Net;
using System.Text;
using System.Text.Json;
using DocVault.Infrastructure.Embeddings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DocVault.UnitTests.Infrastructure.Embeddings;

public sealed class OpenAiEmbeddingProviderTests
{
  [Fact]
  public async Task EmbedBatchAsync_SplitsBatches_AndTruncatesInputs()
  {
    var handler = new RecordingEmbeddingHandler();
    var http = new HttpClient(handler);
    var options = Options.Create(new OpenAiOptions
    {
      ApiKey = "key",
      BaseUrl = "http://localhost:11434/v1",
      Model = "nomic-embed-text",
      Dimensions = 0,
      MaxInputCharacters = 5,
      MaxBatchSize = 2,
    });

    var sut = new OpenAiEmbeddingProvider(http, options, NullLogger<OpenAiEmbeddingProvider>.Instance);
    var result = await sut.EmbedBatchAsync(["abcdefgh", "1234567", "xyz"]);

    Assert.Equal(3, result.Count);
    Assert.Equal(2, handler.Requests.Count);
    Assert.All(handler.Requests.SelectMany(x => x), text => Assert.True(text.Length <= 5));
    Assert.Equal("abcde", handler.Requests[0][0]);
    Assert.Equal("12345", handler.Requests[0][1]);
    Assert.Equal("xyz", handler.Requests[1][0]);
  }

  private sealed class RecordingEmbeddingHandler : HttpMessageHandler
  {
    public List<List<string>> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var body = await request.Content!.ReadAsStringAsync(cancellationToken);
      using var doc = JsonDocument.Parse(body);
      var inputs = doc.RootElement.GetProperty("input")
        .EnumerateArray()
        .Select(x => x.GetString() ?? string.Empty)
        .ToList();

      Requests.Add(inputs);

      var response = new
      {
        data = inputs
          .Select((_, i) => new { embedding = new[] { (float)(i + 1), 0f }, index = i })
          .ToArray()
      };

      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json")
      };
    }
  }
}
