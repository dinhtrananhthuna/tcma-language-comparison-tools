using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Core.Services
{
    /// <summary>
    /// Service for extracting CSV files from ZIP archives
    /// </summary>
    public class ZipExtractionService
    {
        private readonly string _tempDirectory;

        public ZipExtractionService()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "TcmaLanguageComparison", Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Extracts CSV files from a ZIP archive
        /// </summary>
        /// <param name="zipFilePath">Path to the ZIP file</param>
        /// <returns>OperationResult containing list of extracted CSV file paths</returns>
        public async Task<OperationResult<List<string>>> ExtractCsvFilesAsync(string zipFilePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(zipFilePath))
                {
                    return OperationResult<List<string>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.UserInput,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Đường dẫn file ZIP không hợp lệ.",
                        TechnicalDetails = "ZIP file path is null or empty",
                        SuggestedAction = "Vui lòng chọn file ZIP hợp lệ."
                    });
                }

                if (!File.Exists(zipFilePath))
                {
                    return OperationResult<List<string>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.FileAccess,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Không tìm thấy file ZIP.",
                        TechnicalDetails = $"File not found: {zipFilePath}",
                        SuggestedAction = "Vui lòng kiểm tra đường dẫn file ZIP."
                    });
                }

                // Validate ZIP file
                var validationResult = await ValidateZipFileAsync(zipFilePath);
                if (!validationResult.IsSuccess)
                {
                    return OperationResult<List<string>>.Failure(validationResult.Error!);
                }

                // Create extraction directory
                Directory.CreateDirectory(_tempDirectory);

                var extractedFiles = new List<string>();

                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        // Skip directories and non-CSV files
                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                            continue;

                        if (!IsCsvFile(entry.Name))
                            continue;

                        // Extract to flat structure (ignore subdirectories)
                        var fileName = Path.GetFileName(entry.Name);
                        var destinationPath = Path.Combine(_tempDirectory, fileName);

                        // Handle duplicate filenames by adding suffix
                        if (File.Exists(destinationPath))
                        {
                            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                            var extension = Path.GetExtension(fileName);
                            var counter = 1;
                            
                            do
                            {
                                fileName = $"{nameWithoutExt}_{counter}{extension}";
                                destinationPath = Path.Combine(_tempDirectory, fileName);
                                counter++;
                            } while (File.Exists(destinationPath));
                        }

                        // Extract the file
                        entry.ExtractToFile(destinationPath, overwrite: false);
                        extractedFiles.Add(destinationPath);
                    }
                }

                if (extractedFiles.Count == 0)
                {
                    return OperationResult<List<string>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.High,
                        UserMessage = "Không tìm thấy file CSV nào trong ZIP.",
                        TechnicalDetails = $"No CSV files found in ZIP archive: {zipFilePath}",
                        SuggestedAction = "Vui lòng đảm bảo ZIP file chứa các file CSV."
                    });
                }

                return OperationResult<List<string>>.Success(extractedFiles);
            }
            catch (InvalidDataException ex)
            {
                return OperationResult<List<string>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.High,
                    UserMessage = "File ZIP bị hỏng hoặc không hợp lệ.",
                    TechnicalDetails = $"Invalid ZIP file format: {ex.Message}",
                    SuggestedAction = "Vui lòng kiểm tra file ZIP và thử lại."
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return OperationResult<List<string>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.FileAccess,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Không có quyền truy cập file ZIP.",
                    TechnicalDetails = $"Access denied: {ex.Message}",
                    SuggestedAction = "Vui lòng kiểm tra quyền truy cập file."
                });
            }
            catch (Exception ex)
            {
                return OperationResult<List<string>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.UnexpectedError,
                    Severity = ErrorSeverity.Critical,
                    UserMessage = "Lỗi không xác định khi giải nén ZIP.",
                    TechnicalDetails = $"Unexpected error during ZIP extraction: {ex.Message}",
                    SuggestedAction = "Vui lòng thử lại hoặc liên hệ hỗ trợ."
                });
            }
        }

        /// <summary>
        /// Validates if the ZIP file is accessible and contains valid data
        /// </summary>
        /// <param name="zipFilePath">Path to the ZIP file</param>
        /// <returns>OperationResult indicating validation success</returns>
        private async Task<OperationResult<bool>> ValidateZipFileAsync(string zipFilePath)
        {
            try
            {
                // Check file size (basic validation)
                var fileInfo = new FileInfo(zipFilePath);
                if (fileInfo.Length == 0)
                {
                    return OperationResult<bool>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.High,
                        UserMessage = "File ZIP trống.",
                        TechnicalDetails = "ZIP file has zero bytes",
                        SuggestedAction = "Vui lòng chọn file ZIP có dữ liệu."
                    });
                }

                // Try to open the ZIP file to verify it's valid
                using var archive = ZipFile.OpenRead(zipFilePath);
                
                // Check if archive contains any entries
                if (!archive.Entries.Any())
                {
                    return OperationResult<bool>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.Medium,
                        UserMessage = "File ZIP không chứa file nào.",
                        TechnicalDetails = "ZIP archive is empty",
                        SuggestedAction = "Vui lòng chọn file ZIP có chứa CSV files."
                    });
                }

                return OperationResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Không thể mở file ZIP.",
                    TechnicalDetails = $"Failed to open ZIP file: {ex.Message}",
                    SuggestedAction = "Vui lòng kiểm tra file ZIP có hợp lệ không."
                });
            }
        }

        /// <summary>
        /// Checks if a file is a CSV file based on its extension
        /// </summary>
        /// <param name="fileName">Name of the file</param>
        /// <returns>True if the file is a CSV file</returns>
        private static bool IsCsvFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension == ".csv";
        }

        /// <summary>
        /// Gets the temporary extraction directory path
        /// </summary>
        public string ExtractionDirectory => _tempDirectory;

        /// <summary>
        /// Cleans up the temporary extraction directory
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors - temp files will be cleaned up eventually
            }
        }

        /// <summary>
        /// Finalizer to ensure cleanup happens
        /// </summary>
        ~ZipExtractionService()
        {
            Cleanup();
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            Cleanup();
            GC.SuppressFinalize(this);
        }
    }
} 