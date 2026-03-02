using System.Reflection;
using DocVault.Api.Validation;

namespace DocVault.Api.Contracts.Documents;

/// <summary>
/// Bound from multipart/form-data. File content drives FileName, ContentType and Size;
/// Title and Tags are explicit form fields.
/// </summary>
public sealed record DocumentCreateRequest(
  IFormFile? File,
  string Title,
  IReadOnlyList<string> Tags)
  : IBindableFromHttpContext<DocumentCreateRequest>
{
  public static async ValueTask<DocumentCreateRequest?> BindAsync(HttpContext context, ParameterInfo parameter)
  {
    if (!context.Request.HasFormContentType)
      throw new JsonBindingException(
        new Dictionary<string, string[]> { ["body"] = ["Request must be multipart/form-data."] });

    var form = await context.Request.ReadFormAsync(context.RequestAborted);

    var file  = form.Files.GetFile("file");
    var title = form["title"].FirstOrDefault() ?? string.Empty;
    var tags  = form["tags"].Where(t => !string.IsNullOrWhiteSpace(t)).ToList()!;

    return new DocumentCreateRequest(file, title, tags);
  }
}
