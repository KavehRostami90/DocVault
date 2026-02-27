namespace DocVault.Application.Abstractions.Text;

public interface IContentTypeDetector
{
  string Detect(string fileName);
}
