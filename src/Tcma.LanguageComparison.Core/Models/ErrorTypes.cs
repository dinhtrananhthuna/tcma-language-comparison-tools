namespace Tcma.LanguageComparison.Core.Models;

/// <summary>
/// Categories of errors that can occur in the application
/// </summary>
public enum ErrorCategory
{
    FileAccess,
    DataValidation,
    NetworkConnection,
    ApiAuthentication,
    ApiRateLimit,
    ApiProcessing,
    Configuration,
    UserInput,
    SystemResource,
    UnexpectedError
}

/// <summary>
/// Severity levels for errors
/// </summary>
public enum ErrorSeverity
{
    Low,        // User can continue with minor issues
    Medium,     // Feature unavailable but app functional
    High,       // Current operation failed
    Critical    // App state compromised
}

/// <summary>
/// Standardized error information
/// </summary>
public record ErrorInfo
{
    public ErrorCategory Category { get; init; }
    public ErrorSeverity Severity { get; init; }
    public string UserMessage { get; init; } = string.Empty;
    public string TechnicalDetails { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public Exception? OriginalException { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string? ContextInfo { get; init; }
}

/// <summary>
/// Result wrapper that can contain either success data or error information
/// </summary>
public record OperationResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public ErrorInfo? Error { get; init; }
    
    public static OperationResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static OperationResult<T> Failure(ErrorInfo error) => new() { IsSuccess = false, Error = error };
}

/// <summary>
/// Result for operations that don't return data
/// </summary>
public record OperationResult
{
    public bool IsSuccess { get; init; }
    public ErrorInfo? Error { get; init; }
    
    public static OperationResult Success() => new() { IsSuccess = true };
    public static OperationResult Failure(ErrorInfo error) => new() { IsSuccess = false, Error = error };
}

/// <summary>
/// Pre-defined common errors
/// </summary>
public static class CommonErrors
{
    public static ErrorInfo FileNotFound(string filePath) => new()
    {
        Category = ErrorCategory.FileAccess,
        Severity = ErrorSeverity.High,
        UserMessage = "Không thể tìm thấy file được chỉ định.",
        TechnicalDetails = $"File not found: {filePath}",
        SuggestedAction = "Vui lòng kiểm tra đường dẫn file và thử lại."
    };

    public static ErrorInfo InvalidCsvFormat(string filePath, string details) => new()
    {
        Category = ErrorCategory.DataValidation,
        Severity = ErrorSeverity.High,
        UserMessage = "File CSV có định dạng không hợp lệ.",
        TechnicalDetails = $"Invalid CSV format in {filePath}: {details}",
        SuggestedAction = "Vui lòng kiểm tra file CSV có đúng định dạng với header 'ContentId,Content'."
    };

    public static ErrorInfo NetworkConnectionError() => new()
    {
        Category = ErrorCategory.NetworkConnection,
        Severity = ErrorSeverity.High,
        UserMessage = "Không thể kết nối internet.",
        TechnicalDetails = "Network connection failed",
        SuggestedAction = "Vui lòng kiểm tra kết nối internet và thử lại."
    };

    public static ErrorInfo InvalidApiKey() => new()
    {
        Category = ErrorCategory.ApiAuthentication,
        Severity = ErrorSeverity.High,
        UserMessage = "API Key không hợp lệ hoặc đã hết hạn.",
        TechnicalDetails = "API authentication failed",
        SuggestedAction = "Vui lòng kiểm tra lại API Key trong Settings."
    };

    public static ErrorInfo ApiRateLimitExceeded() => new()
    {
        Category = ErrorCategory.ApiRateLimit,
        Severity = ErrorSeverity.Medium,
        UserMessage = "Đã vượt quá giới hạn API calls.",
        TechnicalDetails = "API rate limit exceeded",
        SuggestedAction = "Vui lòng chờ một chút rồi thử lại hoặc kiểm tra quota API."
    };

    public static ErrorInfo EmptyFile(string filePath) => new()
    {
        Category = ErrorCategory.DataValidation,
        Severity = ErrorSeverity.High,
        UserMessage = "File CSV trống hoặc không có dữ liệu hợp lệ.",
        TechnicalDetails = $"Empty or invalid data in file: {filePath}",
        SuggestedAction = "Vui lòng chọn file CSV khác có chứa dữ liệu."
    };

    public static ErrorInfo InvalidFilePath(string filePath) => new()
    {
        Category = ErrorCategory.DataValidation,
        Severity = ErrorSeverity.High,
        UserMessage = "Đường dẫn file không hợp lệ.",
        TechnicalDetails = $"Invalid file path: {filePath}",
        SuggestedAction = "Vui lòng chọn file hợp lệ."
    };

    public static ErrorInfo UnexpectedError(Exception ex) => new()
    {
        Category = ErrorCategory.UnexpectedError,
        Severity = ErrorSeverity.Critical,
        UserMessage = "Đã xảy ra lỗi không mong muốn.",
        TechnicalDetails = ex.Message,
        SuggestedAction = "Vui lòng thử lại hoặc liên hệ hỗ trợ nếu vấn đề tiếp tục.",
        OriginalException = ex
    };
} 