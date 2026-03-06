using DocVault.Api.Contracts.Common;
using DocVault.Api.Contracts.Documents;
using DocVault.Domain.Documents;
using DocVault.Application.Common.Paging;

namespace DocVault.Api.Mappers;

/// <summary>
/// Maps domain document entities to API response DTOs.
/// </summary>
public static class DocumentResponseMapper
{
  public static DocumentListItemResponse ToListItem(Document doc)
    => new(doc.Id.Value, doc.Title, doc.FileName, doc.Status.ToString());

  public static DocumentReadResponse ToRead(Document doc)
    => new(doc.Id.Value, doc.Title, doc.FileName, doc.ContentType, doc.Size, doc.Status.ToString(), doc.Tags.Select(t => t.Name).ToList());

  public static PageResponse<DocumentListItemResponse> ToPage(Page<Document> source)
    => new(source.Items.Select(ToListItem).ToList(), source.PageNumber, source.PageSize, source.TotalCount);
}
