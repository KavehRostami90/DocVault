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
        public const int MAX_UPLOAD_COUNT = 10;
        
        public static readonly string[] ALLOWED_CONTENT_TYPES =
        [
            "application/pdf",
            "text/plain",
            "text/markdown",
            "text/x-markdown",
            "application/json",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "image/png",
            "image/jpeg",
            "image/gif",
            "image/tiff",
            "image/bmp",
            "image/webp",
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
    
    public static class Auth
    {
        public const int MIN_PASSWORD_LENGTH = 8;
        public const int MAX_PASSWORD_LENGTH = 128;
        public const int MAX_EMAIL_LENGTH = 254;
        public const int MAX_DISPLAY_NAME_LENGTH = 100;
    }

    public static class Paging
    {
        public const int DEFAULT_PAGE = 1;
        public const int DEFAULT_SIZE = 20;
        public const int MAX_PAGE_SIZE = 100;
        public const int MAX_FILTER_LENGTH = 100;

        /// <summary>
        /// Canonical lowercase sort field identifiers shared by the validator and the repository.
        /// The sort value received from the client is lower-cased before matching, so "Title",
        /// "TITLE" and "title" all resolve correctly.
        /// </summary>
        public static class SortFields
        {
            public const string TITLE      = "title";
            public const string FILE_NAME  = "filename";
            public const string SIZE       = "size";
            public const string STATUS     = "status";
            public const string CREATED_AT = "createdat";
            public const string UPDATED_AT = "updatedat";
        }

        public static readonly string[] VALID_DOCUMENT_SORT_FIELDS =
        [
            SortFields.TITLE,
            SortFields.FILE_NAME,
            SortFields.SIZE,
            SortFields.STATUS,
            SortFields.CREATED_AT,
            SortFields.UPDATED_AT,
        ];

        public static readonly string[] VALID_DOCUMENT_STATUSES =
        [
            "pending", "imported", "indexed", "failed"
        ];
    }
}
