using DocVault.Application.Abstractions.Qa;
using DocVault.Infrastructure.Qa;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DocVault.UnitTests.Infrastructure.Qa;

public sealed class ResilientQuestionAnsweringServiceTests
{
  private static readonly IReadOnlyList<QaContextChunk> Contexts =
  [
    new QaContextChunk(Guid.NewGuid(), "Doc", "Some relevant text about the topic.", 0.9, 0)
  ];

  private static ResilientQuestionAnsweringService Build(
    IQuestionAnsweringService inner,
    IQuestionAnsweringService? fallback = null) =>
    new(
      inner,
      fallback ?? new FallbackQuestionAnsweringService(),
      new Mock<ILogger<ResilientQuestionAnsweringService>>().Object);

  // -------------------------------------------------------------------------
  // AnswerAsync — success path
  // -------------------------------------------------------------------------

  [Fact]
  public async Task AnswerAsync_WhenInnerSucceeds_ReturnsPrimaryAnswer()
  {
    var inner = new Mock<IQuestionAnsweringService>();
    inner
      .Setup(s => s.AnswerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new QaAnswerResult("Model answer", AnsweredByModel: true));

    var result = await Build(inner.Object).AnswerAsync("question", Contexts);

    Assert.True(result.AnsweredByModel);
    Assert.Equal("Model answer", result.Answer);
  }

  [Fact]
  public async Task AnswerAsync_WhenInnerSucceeds_FallbackIsNeverInvoked()
  {
    var inner    = new Mock<IQuestionAnsweringService>();
    var fallback = new Mock<IQuestionAnsweringService>();

    inner
      .Setup(s => s.AnswerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new QaAnswerResult("Model answer", AnsweredByModel: true));

    await Build(inner.Object, fallback.Object).AnswerAsync("question", Contexts);

    fallback.Verify(
      s => s.AnswerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  // -------------------------------------------------------------------------
  // AnswerAsync — failure / fallback path
  // -------------------------------------------------------------------------

  [Fact]
  public async Task AnswerAsync_WhenInnerThrows_UsesFallbackService()
  {
    var inner = new Mock<IQuestionAnsweringService>();
    inner
      .Setup(s => s.AnswerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new HttpRequestException("Connection refused"));

    var result = await Build(inner.Object).AnswerAsync("question", Contexts);

    Assert.False(result.AnsweredByModel);
    Assert.False(string.IsNullOrWhiteSpace(result.Answer));
  }

  [Fact]
  public async Task AnswerAsync_WhenInnerThrows_FallbackAnswerDoesNotLeakExceptionDetails()
  {
    var inner = new Mock<IQuestionAnsweringService>();
    inner
      .Setup(s => s.AnswerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new HttpRequestException("Connection refused http://internal-llm:11434"));

    var result = await Build(inner.Object).AnswerAsync("question", Contexts);

    Assert.DoesNotContain("http://internal-llm:11434", result.Answer, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("Connection refused", result.Answer, StringComparison.OrdinalIgnoreCase);
  }

  // -------------------------------------------------------------------------
  // AnswerAsync — cancellation must propagate
  // -------------------------------------------------------------------------

  [Fact]
  public async Task AnswerAsync_WhenCancelled_PropagatesCancellationWithoutFallback()
  {
    var fallback = new Mock<IQuestionAnsweringService>();
    var inner    = new Mock<IQuestionAnsweringService>();
    inner
      .Setup(s => s.AnswerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()))
      .ThrowsAsync(new OperationCanceledException());

    await Assert.ThrowsAsync<OperationCanceledException>(
      () => Build(inner.Object, fallback.Object).AnswerAsync("question", Contexts));

    fallback.Verify(
      s => s.AnswerAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  // -------------------------------------------------------------------------
  // AnswerStreamAsync — success path
  // -------------------------------------------------------------------------

  [Fact]
  public async Task AnswerStreamAsync_WhenInnerSucceeds_StreamsAllTokensFromInner()
  {
    var inner = new Mock<IQuestionAnsweringService>();
    inner
      .Setup(s => s.AnswerStreamAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()))
      .Returns(TokenStream("Hello", " world", "!"));

    var tokens = await CollectAsync(Build(inner.Object).AnswerStreamAsync("question", Contexts));

    Assert.Equal(["Hello", " world", "!"], tokens);
  }

  [Fact]
  public async Task AnswerStreamAsync_WhenInnerSucceeds_FallbackIsNeverInvoked()
  {
    var inner    = new Mock<IQuestionAnsweringService>();
    var fallback = new Mock<IQuestionAnsweringService>();

    inner
      .Setup(s => s.AnswerStreamAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()))
      .Returns(TokenStream("token"));
    fallback
      .Setup(s => s.AnswerStreamAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()))
      .Returns(TokenStream("fallback"));

    await CollectAsync(Build(inner.Object, fallback.Object).AnswerStreamAsync("question", Contexts));

    fallback.Verify(
      s => s.AnswerStreamAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  // -------------------------------------------------------------------------
  // AnswerStreamAsync — failure / fallback path
  // -------------------------------------------------------------------------

  [Fact]
  public async Task AnswerStreamAsync_WhenInnerThrowsBeforeFirstToken_YieldsFallbackText()
  {
    var inner = new Mock<IQuestionAnsweringService>();
    inner
      .Setup(s => s.AnswerStreamAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()))
      .Returns(ThrowingStream(new HttpRequestException("503 Service Unavailable")));

    var tokens = await CollectAsync(Build(inner.Object).AnswerStreamAsync("question", Contexts));

    Assert.NotEmpty(tokens);
    Assert.False(string.IsNullOrWhiteSpace(string.Concat(tokens)));
  }

  [Fact]
  public async Task AnswerStreamAsync_WhenInnerThrowsBeforeFirstToken_FallbackAnswerDoesNotLeakExceptionDetails()
  {
    var inner = new Mock<IQuestionAnsweringService>();
    inner
      .Setup(s => s.AnswerStreamAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<QaContextChunk>>(), It.IsAny<CancellationToken>()))
      .Returns(ThrowingStream(new HttpRequestException("Connection refused http://internal-llm:11434")));

    var tokens = await CollectAsync(Build(inner.Object).AnswerStreamAsync("question", Contexts));
    var text   = string.Concat(tokens);

    Assert.DoesNotContain("http://internal-llm:11434", text, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("Connection refused", text, StringComparison.OrdinalIgnoreCase);
  }

  // -------------------------------------------------------------------------
  // Helpers
  // -------------------------------------------------------------------------

  private static async Task<List<string>> CollectAsync(IAsyncEnumerable<string> source)
  {
    var list = new List<string>();
    await foreach (var t in source)
      list.Add(t);
    return list;
  }

  private static async IAsyncEnumerable<string> TokenStream(params string[] tokens)
  {
    foreach (var t in tokens)
      yield return t;
    await Task.CompletedTask;
  }

  private static async IAsyncEnumerable<string> ThrowingStream(Exception ex)
  {
    await Task.Yield();
    throw ex;
#pragma warning disable CS0162
    yield break; // Required for the compiler to recognize this as an async iterator
#pragma warning restore CS0162
  }
}
