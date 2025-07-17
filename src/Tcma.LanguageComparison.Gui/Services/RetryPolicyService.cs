using System;
using System.Threading.Tasks;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Gui.Services;

public interface IRetryPolicyService
{
    Task<OperationResult<T>> ExecuteWithRetryAsync<T>(Func<Task<OperationResult<T>>> operation, Func<ErrorInfo, bool>? isRetriable = null, IProgress<string>? progressCallback = null, string? context = null);
    Task<OperationResult> ExecuteWithRetryAsync(Func<Task<OperationResult>> operation, Func<ErrorInfo, bool>? isRetriable = null, IProgress<string>? progressCallback = null, string? context = null);
    Task<OperationResult<T>[]> ExecuteParallelWithRetryAsync<T>(Func<Task<OperationResult<T>>>[] operations, Func<ErrorInfo, bool>? isRetriable = null, IProgress<string>? progressCallback = null, string? context = null);
}

/// <summary>
/// Service for handling retry policies for API calls and other operations
/// </summary>
public class RetryPolicyService : IRetryPolicyService
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;
    private readonly ErrorHandlingService _errorHandler;

    public RetryPolicyService(int maxRetries = 3, TimeSpan? baseDelay = null, ErrorHandlingService? errorHandler = null)
    {
        _maxRetries = maxRetries;
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(2);
        _errorHandler = errorHandler ?? new ErrorHandlingService();
    }

    /// <summary>
    /// Executes an operation with exponential backoff retry policy
    /// </summary>
    /// <typeparam name="T">Type of operation result</typeparam>
    /// <param name="operation">Operation to execute</param>
    /// <param name="isRetriable">Function to determine if error is retriable</param>
    /// <param name="progressCallback">Optional progress reporting</param>
    /// <param name="context">Context information for error reporting</param>
    /// <returns>Operation result</returns>
    public async Task<OperationResult<T>> ExecuteWithRetryAsync<T>(
        Func<Task<OperationResult<T>>> operation,
        Func<ErrorInfo, bool>? isRetriable = null,
        IProgress<string>? progressCallback = null,
        string? context = null)
    {
        isRetriable ??= IsDefaultRetriable;
        var attempt = 0;

        while (attempt <= _maxRetries)
        {
            try
            {
                var result = await operation();
                
                if (result.IsSuccess)
                {
                    if (attempt > 0)
                    {
                        progressCallback?.Report($"Thành công sau {attempt + 1} lần thử.");
                    }
                    return result;
                }

                // Check if error is retriable
                if (!isRetriable(result.Error!) || attempt >= _maxRetries)
                {
                    // Log the final failure
                    await _errorHandler.HandleErrorAsync(result.Error!, showDialog: false);
                    return result;
                }

                // Calculate delay with exponential backoff
                var delay = CalculateDelay(attempt);
                progressCallback?.Report($"Lần thử {attempt + 1} thất bại. Thử lại sau {delay.TotalSeconds:F1} giây...");
                
                await Task.Delay(delay);
                attempt++;
            }
            catch (Exception ex)
            {
                var error = _errorHandler.ProcessException(ex, context);
                
                if (!isRetriable(error) || attempt >= _maxRetries)
                {
                    await _errorHandler.HandleErrorAsync(error, showDialog: false);
                    return OperationResult<T>.Failure(error);
                }

                var delay = CalculateDelay(attempt);
                progressCallback?.Report($"Exception ở lần thử {attempt + 1}. Thử lại sau {delay.TotalSeconds:F1} giây...");
                
                await Task.Delay(delay);
                attempt++;
            }
        }

        // This should never be reached, but just in case
        var finalError = new ErrorInfo
        {
            Category = ErrorCategory.UnexpectedError,
            Severity = ErrorSeverity.Critical,
            UserMessage = "Đã vượt quá số lần thử tối đa.",
            TechnicalDetails = $"Max retries ({_maxRetries}) exceeded",
            SuggestedAction = "Vui lòng thử lại sau hoặc liên hệ hỗ trợ."
        };

        return OperationResult<T>.Failure(finalError);
    }

    /// <summary>
    /// Executes a simple operation with retry policy
    /// </summary>
    /// <param name="operation">Operation to execute</param>
    /// <param name="isRetriable">Function to determine if error is retriable</param>
    /// <param name="progressCallback">Optional progress reporting</param>
    /// <param name="context">Context information for error reporting</param>
    /// <returns>Operation result</returns>
    public async Task<OperationResult> ExecuteWithRetryAsync(
        Func<Task<OperationResult>> operation,
        Func<ErrorInfo, bool>? isRetriable = null,
        IProgress<string>? progressCallback = null,
        string? context = null)
    {
        var wrappedOperation = async () =>
        {
            var result = await operation();
            return result.IsSuccess 
                ? OperationResult<bool>.Success(true)
                : OperationResult<bool>.Failure(result.Error!);
        };

        var typedResult = await ExecuteWithRetryAsync(wrappedOperation, isRetriable, progressCallback, context);
        
        return typedResult.IsSuccess 
            ? OperationResult.Success()
            : OperationResult.Failure(typedResult.Error!);
    }

    /// <summary>
    /// Executes multiple operations in parallel with retry policies
    /// </summary>
    /// <typeparam name="T">Type of operation results</typeparam>
    /// <param name="operations">Operations to execute</param>
    /// <param name="isRetriable">Function to determine if error is retriable</param>
    /// <param name="progressCallback">Optional progress reporting</param>
    /// <param name="context">Context information for error reporting</param>
    /// <returns>Array of operation results</returns>
    public async Task<OperationResult<T>[]> ExecuteParallelWithRetryAsync<T>(
        Func<Task<OperationResult<T>>>[] operations,
        Func<ErrorInfo, bool>? isRetriable = null,
        IProgress<string>? progressCallback = null,
        string? context = null)
    {
        var tasks = operations.Select((op, index) => 
            ExecuteWithRetryAsync(op, isRetriable, progressCallback, $"{context} - Operation {index + 1}"));

        return await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Calculates delay for exponential backoff
    /// </summary>
    /// <param name="attempt">Current attempt number (0-based)</param>
    /// <returns>Delay timespan</returns>
    private TimeSpan CalculateDelay(int attempt)
    {
        // Exponential backoff with jitter
        var exponentialDelay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
        
        // Add random jitter (±25%)
        var jitter = Random.Shared.NextDouble() * 0.5 + 0.75; // 0.75 to 1.25
        var finalDelay = TimeSpan.FromMilliseconds(exponentialDelay.TotalMilliseconds * jitter);
        
        // Cap the maximum delay at 30 seconds
        return finalDelay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : finalDelay;
    }

    /// <summary>
    /// Default logic to determine if an error is retriable
    /// </summary>
    /// <param name="error">Error to evaluate</param>
    /// <returns>True if error is retriable</returns>
    private static bool IsDefaultRetriable(ErrorInfo error)
    {
        return error.Category switch
        {
            ErrorCategory.NetworkConnection => true,
            ErrorCategory.ApiRateLimit => true,
            ErrorCategory.ApiProcessing when error.TechnicalDetails.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
            ErrorCategory.ApiProcessing when error.TechnicalDetails.Contains("temporary", StringComparison.OrdinalIgnoreCase) => true,
            ErrorCategory.ApiProcessing when error.TechnicalDetails.Contains("503") => true, // Service unavailable
            ErrorCategory.ApiProcessing when error.TechnicalDetails.Contains("502") => true, // Bad gateway
            ErrorCategory.ApiProcessing when error.TechnicalDetails.Contains("504") => true, // Gateway timeout
            _ => false
        };
    }

    /// <summary>
    /// Creates a custom retry policy for API calls
    /// </summary>
    /// <returns>Retry policy function</returns>
    public static Func<ErrorInfo, bool> CreateApiRetryPolicy()
    {
        return error => error.Category switch
        {
            ErrorCategory.NetworkConnection => true,
            ErrorCategory.ApiRateLimit => true,
            ErrorCategory.ApiProcessing => !error.TechnicalDetails.Contains("401") && // Don't retry auth errors
                                         !error.TechnicalDetails.Contains("403") && // Don't retry forbidden
                                         !error.TechnicalDetails.Contains("400"),   // Don't retry bad request
            _ => false
        };
    }

    /// <summary>
    /// Creates a custom retry policy for file operations
    /// </summary>
    /// <returns>Retry policy function</returns>
    public static Func<ErrorInfo, bool> CreateFileRetryPolicy()
    {
        return error => error.Category switch
        {
            ErrorCategory.FileAccess when error.TechnicalDetails.Contains("being used by another process") => true,
            ErrorCategory.FileAccess when error.TechnicalDetails.Contains("temporarily unavailable") => true,
            _ => false
        };
    }
} 