using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Tcma.LanguageComparison.Core.Models;

namespace Tcma.LanguageComparison.Core.Services
{
    /// <summary>
    /// Service for reading CSV files containing localization content
    /// </summary>
    public class CsvReaderService
    {
        /// <summary>
        /// Reads content rows from a CSV file
        /// </summary>
        /// <param name="filePath">Path to the CSV file</param>
        /// <returns>OperationResult containing list of ContentRow objects or error info</returns>
        public async Task<OperationResult<List<ContentRow>>> ReadContentRowsAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return OperationResult<List<ContentRow>>.Failure(
                        CommonErrors.InvalidFilePath(filePath ?? "null"));
                }

                if (!File.Exists(filePath))
                {
                    return OperationResult<List<ContentRow>>.Failure(
                        CommonErrors.FileNotFound(filePath));
                }

                // Validate file extension
                var extension = Path.GetExtension(filePath).ToLower();
                if (extension != ".csv")
                {
                    return OperationResult<List<ContentRow>>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.High,
                        UserMessage = "File không phải định dạng CSV.",
                        TechnicalDetails = $"Expected .csv extension, got: {extension}",
                        SuggestedAction = "Vui lòng chọn file có định dạng .csv"
                    });
                }

                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, GetCsvConfiguration());

                var records = new List<ContentRow>();
                var index = 0;

                await foreach (var record in csv.GetRecordsAsync<CsvRowDto>())
                {
                    // Validate required fields
                    if (string.IsNullOrEmpty(record.ContentId) && string.IsNullOrEmpty(record.Content))
                    {
                        continue; // Skip empty rows
                    }

                    records.Add(new ContentRow
                    {
                        ContentId = record.ContentId ?? string.Empty,
                        Content = record.Content ?? string.Empty,
                        OriginalIndex = index++
                    });
                }

                // Validate that we have data
                if (records.Count == 0)
                {
                    return OperationResult<List<ContentRow>>.Failure(
                        CommonErrors.EmptyFile(filePath));
                }

                // Validate CSV structure
                var validationResult = ValidateCsvStructure(records);
                if (!validationResult.IsSuccess)
                {
                    return validationResult;
                }

                return OperationResult<List<ContentRow>>.Success(records);
            }
            catch (UnauthorizedAccessException ex)
            {
                return OperationResult<List<ContentRow>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.FileAccess,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Không có quyền truy cập file.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng kiểm tra quyền truy cập file hoặc chọn file khác.",
                    OriginalException = ex,
                    ContextInfo = filePath
                });
            }
            catch (IOException ex)
            {
                return OperationResult<List<ContentRow>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.FileAccess,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Lỗi đọc file CSV.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng đóng file nếu đang mở và thử lại.",
                    OriginalException = ex,
                    ContextInfo = filePath
                });
            }
            catch (Exception ex)
            {
                return OperationResult<List<ContentRow>>.Failure(
                    CommonErrors.InvalidCsvFormat(filePath, ex.Message));
            }
        }

        /// <summary>
        /// Writes content rows to a CSV file
        /// </summary>
        /// <param name="filePath">Output file path</param>
        /// <param name="contentRows">Content rows to write</param>
        /// <returns>OperationResult indicating success or error</returns>
        public async Task<OperationResult<bool>> WriteContentRowsAsync(string filePath, IEnumerable<ContentRow> contentRows)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return OperationResult<bool>.Failure(
                        CommonErrors.InvalidFilePath(filePath ?? "null"));
                }

                var rows = contentRows?.ToList();
                if (rows == null || rows.Count == 0)
                {
                    return OperationResult<bool>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.Medium,
                        UserMessage = "Không có dữ liệu để ghi vào file.",
                        TechnicalDetails = "Content rows collection is null or empty",
                        SuggestedAction = "Vui lòng đảm bảo có dữ liệu trước khi xuất file."
                    });
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var writer = new StreamWriter(filePath);
                using var csv = new CsvWriter(writer, GetCsvConfiguration());

                var records = rows.Select(row => new CsvRowDto
                {
                    ContentId = row.ContentId,
                    Content = row.Content
                });

                await csv.WriteRecordsAsync(records);
                return OperationResult<bool>.Success(true);
            }
            catch (UnauthorizedAccessException ex)
            {
                return OperationResult<bool>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.FileAccess,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Không có quyền ghi file.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng chọn vị trí khác hoặc chạy với quyền Admin.",
                    OriginalException = ex,
                    ContextInfo = filePath
                });
            }
            catch (IOException ex)
            {
                return OperationResult<bool>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.FileAccess,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Lỗi ghi file CSV.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng đóng file nếu đang mở và thử lại.",
                    OriginalException = ex,
                    ContextInfo = filePath
                });
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.FileAccess,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Lỗi ghi file CSV.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng kiểm tra đường dẫn và quyền truy cập.",
                    OriginalException = ex,
                    ContextInfo = filePath
                });
            }
        }

        /// <summary>
        /// Export aligned target rows ra file CSV (dòng thiếu sẽ để trống, có thêm cột Status và SimilarityScore)
        /// </summary>
        public async Task<OperationResult<bool>> ExportAlignedTargetRowsAsync(string filePath, AlignedTargetResult alignedResult)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return OperationResult<bool>.Failure(
                        CommonErrors.InvalidFilePath(filePath ?? "null"));
                }

                var rows = alignedResult.AlignedRows;
                if (rows == null || rows.Count == 0)
                {
                    return OperationResult<bool>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.Medium,
                        UserMessage = "Không có dữ liệu để ghi vào file.",
                        TechnicalDetails = "Aligned rows collection is null or empty",
                        SuggestedAction = "Vui lòng đảm bảo có dữ liệu trước khi xuất file."
                    });
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var writer = new StreamWriter(filePath);
                using var csv = new CsvWriter(writer, GetCsvConfiguration());

                // Write header
                csv.WriteField("ContentId");
                csv.WriteField("Content");
                csv.WriteField("Status");
                csv.WriteField("SimilarityScore");
                csv.NextRecord();

                // Write rows
                foreach (var row in rows)
                {
                    if (row.HasMatch && row.TargetRow != null)
                    {
                        csv.WriteField(row.TargetRow.ContentId);
                        csv.WriteField(row.TargetRow.Content);
                        csv.WriteField("Matched");
                        csv.WriteField(row.SimilarityScore?.ToString("F3") ?? "");
                    }
                    else
                    {
                        csv.WriteField("");
                        csv.WriteField("");
                        csv.WriteField("Missing");
                        csv.WriteField("");
                    }
                    csv.NextRecord();
                }
                writer.Flush();
                return OperationResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.FileAccess,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Lỗi ghi file CSV aligned target.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng đóng file nếu đang mở và thử lại.",
                    OriginalException = ex,
                    ContextInfo = filePath
                });
            }
        }

        /// <summary>
        /// Export aligned display data to CSV file (bao gồm cả unmatched target rows)
        /// </summary>
        public async Task<OperationResult<bool>> ExportAlignedDisplayRowsAsync(string filePath, List<AlignedDisplayRow> alignedDisplayRows)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return OperationResult<bool>.Failure(
                        CommonErrors.InvalidFilePath(filePath ?? "null"));
                }

                if (alignedDisplayRows == null || alignedDisplayRows.Count == 0)
                {
                    return OperationResult<bool>.Failure(new ErrorInfo
                    {
                        Category = ErrorCategory.DataValidation,
                        Severity = ErrorSeverity.Medium,
                        UserMessage = "Không có dữ liệu để ghi vào file.",
                        TechnicalDetails = "Aligned display rows collection is null or empty",
                        SuggestedAction = "Vui lòng đảm bảo có dữ liệu trước khi xuất file."
                    });
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var writer = new StreamWriter(filePath);
                using var csv = new CsvWriter(writer, GetCsvConfiguration());

                // Write header
                csv.WriteField("ContentId");
                csv.WriteField("Content");
                csv.WriteField("Status");
                csv.WriteField("SimilarityScore");
                csv.WriteField("RowType");
                csv.NextRecord();

                // Write rows - bao gồm tất cả các loại dòng
                foreach (var displayRow in alignedDisplayRows)
                {
                    switch (displayRow.Status)
                    {
                        case "Matched":
                            csv.WriteField(displayRow.TargetContentId);
                            csv.WriteField(displayRow.TargetContent);
                            csv.WriteField("Matched");
                            csv.WriteField(displayRow.SimilarityScore?.ToString("F3") ?? "");
                            csv.WriteField("Reference Aligned");
                            break;
                        
                        case "Unmatched Target":
                            csv.WriteField(displayRow.TargetContentId);
                            csv.WriteField(displayRow.TargetContent);
                            csv.WriteField("Unmatched Target");
                            csv.WriteField(""); // No similarity score for unmatched
                            csv.WriteField("Extra Target");
                            break;
                            
                        default: // Missing or other statuses
                            csv.WriteField("");
                            csv.WriteField("");
                            csv.WriteField(displayRow.Status);
                            csv.WriteField("");
                            csv.WriteField("Reference Aligned");
                            break;
                    }
                    csv.NextRecord();
                }
                
                writer.Flush();
                return OperationResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.FileAccess,
                    Severity = ErrorSeverity.High,
                    UserMessage = "Lỗi ghi file CSV aligned target.",
                    TechnicalDetails = ex.Message,
                    SuggestedAction = "Vui lòng đóng file nếu đang mở và thử lại.",
                    OriginalException = ex,
                    ContextInfo = filePath
                });
            }
        }

        /// <summary>
        /// Validates the structure of loaded CSV data
        /// </summary>
        private OperationResult<List<ContentRow>> ValidateCsvStructure(List<ContentRow> records)
        {
            // Check for minimum required fields
            var rowsWithoutContentId = records.Count(r => string.IsNullOrWhiteSpace(r.ContentId));
            var rowsWithoutContent = records.Count(r => string.IsNullOrWhiteSpace(r.Content));

            if (rowsWithoutContentId > records.Count * 0.5) // More than 50% missing ContentId
            {
                return OperationResult<List<ContentRow>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.High,
                    UserMessage = "File CSV thiếu cột ContentId hoặc có quá nhiều dòng trống.",
                    TechnicalDetails = $"{rowsWithoutContentId}/{records.Count} rows missing ContentId",
                    SuggestedAction = "Vui lòng kiểm tra file CSV có header 'ContentId,Content' và dữ liệu đầy đủ."
                });
            }

            if (rowsWithoutContent > records.Count * 0.5) // More than 50% missing Content
            {
                return OperationResult<List<ContentRow>>.Failure(new ErrorInfo
                {
                    Category = ErrorCategory.DataValidation,
                    Severity = ErrorSeverity.High,
                    UserMessage = "File CSV thiếu cột Content hoặc có quá nhiều dòng trống.",
                    TechnicalDetails = $"{rowsWithoutContent}/{records.Count} rows missing Content",
                    SuggestedAction = "Vui lòng kiểm tra file CSV có header 'ContentId,Content' và dữ liệu đầy đủ."
                });
            }

            return OperationResult<List<ContentRow>>.Success(records);
        }

        /// <summary>
        /// Gets CSV configuration for consistent parsing/writing
        /// </summary>
        private static CsvConfiguration GetCsvConfiguration()
        {
            return new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null, // Ignore missing fields
                BadDataFound = null // Ignore bad data
            };
        }
    }

    /// <summary>
    /// DTO class for CSV mapping
    /// </summary>
    internal class CsvRowDto
    {
        public string? ContentId { get; set; }
        public string? Content { get; set; }
    }
} 