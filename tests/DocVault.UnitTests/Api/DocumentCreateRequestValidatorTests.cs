using DocVault.Api.Composition;
using DocVault.Api.Contracts.Documents;
using DocVault.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace DocVault.UnitTests.Api;

public sealed class DocumentCreateRequestValidatorTests
{
    private static DocumentCreateRequestValidator CreateValidator(long maxFileSizeBytes)
        => new(Options.Create(new UploadOptions { MaxFileSizeBytes = maxFileSizeBytes }));

    private static IFormFile CreateFile(long size, string fileName = "doc.txt", string contentType = "text/plain")
    {
        var content = new byte[Math.Max(1, (int)size)];
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, size, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
    }

    [Fact]
    public void Validate_WhenFileIsWithinConfiguredLimit_Succeeds()
    {
        var validator = CreateValidator(1_024);
        var request = new DocumentCreateRequest(CreateFile(1_024), "Doc", []);

        var result = validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenFileExceedsConfiguredLimit_Fails()
    {
        var validator = CreateValidator(1_024);
        var request = new DocumentCreateRequest(CreateFile(1_025), "Doc", []);

        var result = validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("1.0 KB"));
    }
}
