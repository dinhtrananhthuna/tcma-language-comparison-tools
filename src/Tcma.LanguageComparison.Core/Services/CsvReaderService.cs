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
        /// <returns>List of ContentRow objects</returns>
        /// <exception cref="FileNotFoundException">Thrown when file doesn't exist</exception>
        /// <exception cref="InvalidDataException">Thrown when CSV format is invalid</exception>
        public async Task<List<ContentRow>> ReadContentRowsAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file not found: {filePath}");
            }

            try
            {
                using var reader = new StreamReader(filePath);
                using var csv = new CsvReader(reader, GetCsvConfiguration());

                var records = new List<ContentRow>();
                var index = 0;

                await foreach (var record in csv.GetRecordsAsync<CsvRowDto>())
                {
                    records.Add(new ContentRow
                    {
                        ContentId = record.ContentId ?? string.Empty,
                        Content = record.Content ?? string.Empty,
                        OriginalIndex = index++
                    });
                }

                return records;
            }
            catch (Exception ex) when (!(ex is FileNotFoundException))
            {
                throw new InvalidDataException($"Error reading CSV file '{filePath}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Writes content rows to a CSV file
        /// </summary>
        /// <param name="filePath">Output file path</param>
        /// <param name="contentRows">Content rows to write</param>
        public async Task WriteContentRowsAsync(string filePath, IEnumerable<ContentRow> contentRows)
        {
            try
            {
                using var writer = new StreamWriter(filePath);
                using var csv = new CsvWriter(writer, GetCsvConfiguration());

                var records = contentRows.Select(row => new CsvRowDto
                {
                    ContentId = row.ContentId,
                    Content = row.Content
                });

                await csv.WriteRecordsAsync(records);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error writing CSV file '{filePath}': {ex.Message}", ex);
            }
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