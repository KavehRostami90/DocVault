using DocVault.Infrastructure.Embeddings;
using Xunit;

namespace DocVault.UnitTests.Infrastructure.Embeddings;

public sealed class OpenAiOptionsTests
{
  [Fact]
  public void IsConfigured_WithApiKey_ReturnsTrue()
  {
    var options = new OpenAiOptions { ApiKey = "key", BaseUrl = "https://api.openai.com/v1" };
    Assert.True(options.IsConfigured);
  }

  [Fact]
  public void IsConfigured_WithCustomBaseUrlAndNoApiKey_ReturnsTrue()
  {
    var options = new OpenAiOptions { ApiKey = "", BaseUrl = "http://ollama:11434/v1" };
    Assert.True(options.IsConfigured);
  }

  [Fact]
  public void IsConfigured_WithDefaultBaseUrlAndNoApiKey_ReturnsFalse()
  {
    var options = new OpenAiOptions { ApiKey = "", BaseUrl = "https://api.openai.com/v1" };
    Assert.False(options.IsConfigured);
  }
}
