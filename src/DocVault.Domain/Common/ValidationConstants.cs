namespace DocVault.Domain.Common;

/// <summary>
/// Central location for validation constants used across the domain
/// </summary>
public static class ValidationConstants
{
    public static class Documents
    {
        public const int MAX_TITLE_LENGTH = 256;
        public const int MIN_TITLE_LENGTH = 1;
        public const long MAX_FILE_SIZE_BYTES = 50L * 1024 * 1024; // 50 MB
        public const long MIN_FILE_SIZE_BYTES = 1;
        
        public static readonly string[] ALLOWED_CONTENT_TYPES =
        [
            "application/pdf",
            "text/plain", 
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        ];
    }
    
    public static class Tags
    {
        public const int MAX_NAME_LENGTH = 50;
        public const int MIN_NAME_LENGTH = 1;
        public const int MAX_TAGS_PER_DOCUMENT = 20;
    }
    
    public static class Search
    {
        public const int MIN_QUERY_LENGTH = 2;
        public const int MAX_QUERY_LENGTH = 512;
        public const int MIN_PAGE = 1;
        public const int MIN_PAGE_SIZE = 1;
        public const int MAX_PAGE_SIZE = 100;
        public const int DEFAULT_PAGE_SIZE = 20;
    }
    
    public static class Paging
    {
        public const int DEFAULT_PAGE = 1;
        public const int DEFAULT_SIZE = 20;
        public const int MAX_FILTER_LENGTH = 100;
        
        public static readonly string[] VALID_DOCUMENT_SORT_FIELDS =
        [
            "title", "fileName", "size", "status", "createdAt", "updatedAt"
        ];
        
        public static readonly string[] VALID_DOCUMENT_STATUSES =
        [
            "pending", "imported", "indexed", "failed"
        ];
    }
}
