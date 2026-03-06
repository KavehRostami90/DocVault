using DocVault.Api.Contracts.Common;
using DocVault.Api.Mappers;
using DocVault.Domain.Documents;
using DocVault.Domain.Documents.ValueObjects;
using DocVault.Application.Common.Paging;
using Xunit;

namespace DocVault.UnitTests.Api.Mappers;

public class DocumentResponseMapperTests
{
  [Fact]
  public void ToListItem_Maps_All_Fields()
  {
    var id = DocumentId.New();
    var doc = new Document(id, "Title", "file.pdf", "application/pdf", 123, new FileHash("hash"));
    doc.AttachText("content");
    doc.MarkIndexed();

    var dto = DocumentResponseMapper.ToListItem(doc);

    Assert.Equal(id.Value, dto.Id);
    Assert.Equal("Title", dto.Title);
    Assert.Equal("file.pdf", dto.FileName);
    Assert.Equal(doc.Status.ToString(), dto.Status);
  }

  [Fact]
  public void ToRead_Maps_All_Fields_Including_Tags()
  {
    var id = DocumentId.New();
    var doc = new Document(id, "Doc", "file.txt", "text/plain", 42, new FileHash("hash"));
    doc.AttachText("hello");
    doc.ReplaceTags(new[] { new Tag("tag1"), new Tag("tag2") });

    var dto = DocumentResponseMapper.ToRead(doc);

    Assert.Equal(id.Value, dto.Id);
    Assert.Equal("Doc", dto.Title);
    Assert.Equal("file.txt", dto.FileName);
    Assert.Equal("text/plain", dto.ContentType);
    Assert.Equal(42, dto.Size);
    Assert.Equal(doc.Status.ToString(), dto.Status);
    Assert.Contains("tag1", dto.Tags);
    Assert.Contains("tag2", dto.Tags);
  }

  [Fact]
  public void ToPage_Maps_Page_Metadata_And_Items()
  {
    var docs = new[]
    {
      new Document(DocumentId.New(), "A", "a.pdf", "application/pdf", 10, new FileHash("h1")),
      new Document(DocumentId.New(), "B", "b.pdf", "application/pdf", 20, new FileHash("h2"))
    };

    var page = new Page<Document>(docs, pageNumber: 2, pageSize: 5, totalCount: 12);

    var dto = DocumentResponseMapper.ToPage(page);

    Assert.Equal(2, dto.Page);
    Assert.Equal(5, dto.Size);
    Assert.Equal(12, dto.TotalCount);
    Assert.Equal(2, dto.Items.Count);
    Assert.Contains(dto.Items, i => i.Title == "A");
    Assert.Contains(dto.Items, i => i.Title == "B");
  }
}
