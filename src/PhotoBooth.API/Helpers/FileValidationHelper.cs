namespace PhotoBooth.API.Helpers;

public static class FileValidationHelper
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png" };
    public const long MaxFileSize = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Validates image upload: size, extension, content-type, magic bytes.
    /// F2-FIX: Opens an independent stream per IFormFile.OpenReadStream() contract
    /// so the validation stream does NOT affect subsequent CopyToAsync calls.
    /// </summary>
    /// <returns>Null if valid, error message string if invalid.</returns>
    public static string? Validate(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return "No file provided";

        // File size limit (10MB)
        if (file.Length > MaxFileSize)
            return $"File too large. Maximum {MaxFileSize / 1024 / 1024}MB";

        // Check extension
        var ext = Path.GetExtension(file.FileName)?.ToLower();
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return "Invalid file type. Only .jpg, .jpeg, .png allowed";

        // Check content-type
        if (!AllowedContentTypes.Contains(file.ContentType?.ToLower()))
            return "Invalid content type";

        // F2-FIX: Read magic bytes in a dedicated scoped stream.
        // IFormFile.OpenReadStream() returns an independent RangeReadStream each call,
        // so reading here does NOT advance the stream used by CopyToAsync later.
        // CA2022 / M4-FIX: Stream.Read may return fewer bytes than requested (partial reads).
        // Use ReadExactly (net8+) or a full-read loop to guarantee all 8 header bytes are read.
        using var headerStream = file.OpenReadStream();
        int headerLen = (int)Math.Min(8, file.Length);
        var header = new byte[headerLen];
        int totalRead = 0;
        while (totalRead < headerLen)
        {
            int n = headerStream.Read(header, totalRead, headerLen - totalRead);
            if (n == 0) break; // EOF (should not happen on form file stream)
            totalRead += n;
        }
        // headerStream disposed here — CopyToAsync gets a fresh stream via its own OpenReadStream() call

        bool isJpeg = header.Length >= 2 && header[0] == 0xFF && header[1] == 0xD8;
        bool isPng  = header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50
                      && header[2] == 0x4E && header[3] == 0x47;
        if (!isJpeg && !isPng)
            return "File content is not a valid image";

        return null; // valid
    }
}
