using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Gui.Services;

public interface IErrorHandlingService
{
    ErrorInfo ProcessException(Exception ex, string? context = null);
    Task HandleErrorAsync(ErrorInfo error, bool showDialog = true);
    Task<T?> HandleResultAsync<T>(OperationResult<T> result, bool showDialog = true);
    Task<bool> TestNetworkConnectivityAsync();
    string GetRecoveryGuidance(ErrorInfo error);
}

/// <summary>
/// Centralized service for handling errors, logging, and user notifications
/// </summary>
public class ErrorHandlingService : IErrorHandlingService
{
    private readonly Action<string>? _statusUpdater;
    private readonly Action<string>? _progressUpdater;
    private readonly Action? _hideProgress;

    public ErrorHandlingService(
        Action<string>? statusUpdater = null,
        Action<string>? progressUpdater = null,
        Action? hideProgress = null)
    {
        _statusUpdater = statusUpdater;
        _progressUpdater = progressUpdater;
        _hideProgress = hideProgress;
    }

    /// <summary>
    /// Processes an exception and converts it to structured error information
    /// </summary>
    public ErrorInfo ProcessException(Exception ex, string? context = null)
    {
        return ex switch
        {
            FileNotFoundException fnfEx => CommonErrors.FileNotFound(fnfEx.FileName ?? "Unknown"),
            UnauthorizedAccessException => new ErrorInfo
            {
                Category = ErrorCategory.FileAccess,
                Severity = ErrorSeverity.High,
                UserMessage = "Không có quyền truy cập file.",
                TechnicalDetails = ex.Message,
                SuggestedAction = "Vui lòng kiểm tra quyền truy cập file hoặc chọn file khác.",
                OriginalException = ex,
                ContextInfo = context
            },
            DirectoryNotFoundException => new ErrorInfo
            {
                Category = ErrorCategory.FileAccess,
                Severity = ErrorSeverity.High,
                UserMessage = "Không tìm thấy thư mục được chỉ định.",
                TechnicalDetails = ex.Message,
                SuggestedAction = "Vui lòng kiểm tra đường dẫn thư mục và thử lại.",
                OriginalException = ex,
                ContextInfo = context
            },
            InvalidDataException => CommonErrors.InvalidCsvFormat(context ?? "Unknown", ex.Message),
            ArgumentException argEx when argEx.ParamName == "apiKey" => CommonErrors.InvalidApiKey(),
            HttpRequestException httpEx => AnalyzeHttpException(httpEx),
            WebException webEx => AnalyzeWebException(webEx),
            TaskCanceledException => new ErrorInfo
            {
                Category = ErrorCategory.NetworkConnection,
                Severity = ErrorSeverity.Medium,
                UserMessage = "Thao tác bị timeout.",
                TechnicalDetails = ex.Message,
                SuggestedAction = "Vui lòng kiểm tra kết nối mạng và thử lại.",
                OriginalException = ex,
                ContextInfo = context
            },
            InvalidOperationException ioEx when ioEx.Message.Contains("API") => new ErrorInfo
            {
                Category = ErrorCategory.ApiProcessing,
                Severity = ErrorSeverity.High,
                UserMessage = "Lỗi xử lý API.",
                TechnicalDetails = ex.Message,
                SuggestedAction = "Vui lòng thử lại hoặc kiểm tra API key.",
                OriginalException = ex,
                ContextInfo = context
            },
            _ => CommonErrors.UnexpectedError(ex) with { ContextInfo = context }
        };
    }

    /// <summary>
    /// Handles an error with appropriate user notification and logging
    /// </summary>
    public async Task HandleErrorAsync(ErrorInfo error, bool showDialog = true)
    {
        // Update status/progress based on severity
        switch (error.Severity)
        {
            case ErrorSeverity.Critical:
                _hideProgress?.Invoke();
                _statusUpdater?.Invoke($"CRITICAL ERROR: {error.UserMessage}");
                break;
            case ErrorSeverity.High:
                _hideProgress?.Invoke();
                _statusUpdater?.Invoke($"ERROR: {error.UserMessage}");
                break;
            case ErrorSeverity.Medium:
                _progressUpdater?.Invoke($"WARNING: {error.UserMessage}");
                break;
            case ErrorSeverity.Low:
                _statusUpdater?.Invoke($"Info: {error.UserMessage}");
                break;
        }

        // Log error (could be extended to file logging)
        await LogErrorAsync(error);

        // Show dialog for high severity errors
        if (showDialog && error.Severity >= ErrorSeverity.High)
        {
            ShowErrorDialog(error);
        }
    }

    /// <summary>
    /// Handles an operation result and processes any errors
    /// </summary>
    public async Task<T?> HandleResultAsync<T>(OperationResult<T> result, bool showDialog = true)
    {
        if (result.IsSuccess)
        {
            return result.Data;
        }

        if (result.Error != null)
        {
            await HandleErrorAsync(result.Error, showDialog);
        }

        return default(T);
    }

    /// <summary>
    /// Tests network connectivity
    /// </summary>
    public async Task<bool> TestNetworkConnectivityAsync()
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 5000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Provides intelligent error recovery suggestions
    /// </summary>
    public string GetRecoveryGuidance(ErrorInfo error)
    {
        return error.Category switch
        {
            ErrorCategory.NetworkConnection => 
                "1. Kiểm tra kết nối internet\n2. Tắt firewall/proxy tạm thời\n3. Thử lại sau vài phút",
            ErrorCategory.ApiAuthentication => 
                "1. Vào Settings để kiểm tra API Key\n2. Xác nhận API Key còn hiệu lực\n3. Kiểm tra billing/quota của Google Cloud",
            ErrorCategory.FileAccess => 
                "1. Kiểm tra file có tồn tại không\n2. Đảm bảo có quyền đọc file\n3. Thử copy file ra vị trí khác",
            ErrorCategory.DataValidation => 
                "1. Mở file CSV bằng Excel/Notepad\n2. Kiểm tra có header 'ContentId,Content'\n3. Đảm bảo không có dòng trống",
            _ => error.SuggestedAction
        };
    }

    private ErrorInfo AnalyzeHttpException(HttpRequestException ex)
    {
        if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            return CommonErrors.InvalidApiKey();
        }
        
        if (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests"))
        {
            return CommonErrors.ApiRateLimitExceeded();
        }

        if (ex.Message.Contains("timeout") || ex.Message.Contains("No response"))
        {
            return CommonErrors.NetworkConnectionError();
        }

        return new ErrorInfo
        {
            Category = ErrorCategory.ApiProcessing,
            Severity = ErrorSeverity.High,
            UserMessage = "Lỗi kết nối API.",
            TechnicalDetails = ex.Message,
            SuggestedAction = "Vui lòng kiểm tra kết nối mạng và API key.",
            OriginalException = ex
        };
    }

    private ErrorInfo AnalyzeWebException(WebException ex)
    {
        return ex.Status switch
        {
            WebExceptionStatus.ConnectFailure or WebExceptionStatus.Timeout => CommonErrors.NetworkConnectionError(),
            WebExceptionStatus.NameResolutionFailure => new ErrorInfo
            {
                Category = ErrorCategory.NetworkConnection,
                Severity = ErrorSeverity.High,
                UserMessage = "Không thể giải quyết tên miền API.",
                TechnicalDetails = ex.Message,
                SuggestedAction = "Kiểm tra DNS settings hoặc kết nối internet.",
                OriginalException = ex
            },
            _ => new ErrorInfo
            {
                Category = ErrorCategory.NetworkConnection,
                Severity = ErrorSeverity.High,
                UserMessage = "Lỗi kết nối mạng.",
                TechnicalDetails = ex.Message,
                SuggestedAction = "Kiểm tra kết nối internet và thử lại.",
                OriginalException = ex
            }
        };
    }

    private void ShowErrorDialog(ErrorInfo error)
    {
        var title = error.Severity switch
        {
            ErrorSeverity.Critical => "Critical Error",
            ErrorSeverity.High => "Error",
            ErrorSeverity.Medium => "Warning",
            _ => "Information"
        };

        var icon = error.Severity switch
        {
            ErrorSeverity.Critical => MessageBoxImage.Stop,
            ErrorSeverity.High => MessageBoxImage.Error,
            ErrorSeverity.Medium => MessageBoxImage.Warning,
            _ => MessageBoxImage.Information
        };

        var message = $"{error.UserMessage}\n\n";
        
        if (!string.IsNullOrEmpty(error.SuggestedAction))
        {
            message += $"Giải pháp:\n{GetRecoveryGuidance(error)}";
        }

        MessageBox.Show(message, title, MessageBoxButton.OK, icon);
    }

    private async Task LogErrorAsync(ErrorInfo error)
    {
        // Simple console logging for now - could be extended to file logging
        await Task.Run(() =>
        {
            var logMessage = $"[{error.Timestamp:yyyy-MM-dd HH:mm:ss}] " +
                           $"{error.Severity} - {error.Category}: {error.UserMessage}";
            
            if (!string.IsNullOrEmpty(error.TechnicalDetails))
            {
                logMessage += $"\nDetails: {error.TechnicalDetails}";
            }
            
            if (!string.IsNullOrEmpty(error.ContextInfo))
            {
                logMessage += $"\nContext: {error.ContextInfo}";
            }

            Console.WriteLine(logMessage);
        });
    }
} 