namespace DocVault.Domain.Primitives;

/// <summary>
/// Machine-readable domain error codes. Clients can switch on these codes
/// to provide targeted error handling without parsing human-readable messages.
/// </summary>
public static class DomainErrorCodes
{
    // Document – Title
    public const string TitleRequired = "TITLE_REQUIRED";
    public const string TitleLength = "TITLE_LENGTH";

    // Document – File
    public const string FileNameRequired = "FILE_NAME_REQUIRED";
    public const string FileNameInvalid = "FILE_NAME_INVALID";
    public const string ContentTypeRequired = "CONTENT_TYPE_REQUIRED";
    public const string FileSizeOutOfRange = "FILE_SIZE_OUT_OF_RANGE";

    // Document – Tags
    public const string TagsRequired = "TAGS_REQUIRED";
    public const string TagLimitExceeded = "TAG_LIMIT_EXCEEDED";

    // Document – Hash
    public const string DuplicateHash = "DUPLICATE_HASH";

    // Import
    public const string ImportAlreadyCompleted = "IMPORT_ALREADY_COMPLETED";
}
