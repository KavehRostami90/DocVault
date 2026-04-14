namespace DocVault.IntegrationTests.Infrastructure;

/// <summary>
/// Test data for document uploads following the Factory pattern.
/// </summary>
public static class DocumentUploadTestData
{
    public static readonly byte[] PdfBytes = "%PDF-1.4 test"u8.ToArray();
    public static readonly byte[] TxtBytes = "Hello, integration test."u8.ToArray();
    public static readonly byte[] AudioBytes = [0xFF, 0xFB]; // MP3 frame sync bytes

    public static class ValidFiles
    {
        public static (byte[] Bytes, string FileName, string ContentType) Pdf => 
            (PdfBytes, "report.pdf", "application/pdf");
            
        public static (byte[] Bytes, string FileName, string ContentType) TextFile => 
            (TxtBytes, "notes.txt", "text/plain");
    }

    public static class InvalidFiles
    {
        public static (byte[] Bytes, string FileName, string ContentType) UnsupportedAudio => 
            (AudioBytes, "track.mp3", "audio/mpeg");
    }

    public static class ValidTitles
    {
        public const string QuarterlyReport = "Quarterly Report";
        public const string MeetingNotes = "Meeting notes";
        public const string PlainDocument = "Plain document";
    }

    public static class InvalidTitles
    {
        public const string Empty = "";
        public static readonly string TooLong = new('x', 1001); // Assuming 1000 char limit
    }

    public static class ValidTags
    {
        public static readonly string[] Finance = ["finance", "q4"];
        public static readonly string[] Meeting = ["meeting", "internal"];
        public static readonly string[] Empty = [];
    }
}

