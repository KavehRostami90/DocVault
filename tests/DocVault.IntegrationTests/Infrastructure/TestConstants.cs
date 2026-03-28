namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Test constants following the Single Responsibility Principle.
/// Each set of constants has a single, well-defined purpose.
/// </summary>
public static class SearchTestConstants
{
    // Unique sentinel terms so seeded docs don't collide with anything uploaded
    // by other test classes sharing infrastructure.
    public const string TITLE_ONLY_TERM = "Photosynthesis8472";
    public const string TEXT_ONLY_TERM = "chlorophyll9318";
    public const string SHARED_TERM = "biology5501";
    public const string NO_MATCH_TERM = "zxkqwerty00000";
}

public static class ValidationTestConstants
{
    public const int MAX_QUERY_LENGTH = 512;
    public const int MIN_PAGE_NUMBER = 1;
    public const int MAX_PAGE_SIZE = 200;
}

public static class DocumentTestConstants
{
    public const string DEFAULT_CONTENT_TYPE = "text/plain";
    public const string DEFAULT_FILENAME = "test.txt";
}

