# TCMA Language Comparison Tool - GUI Application

Đây là Phase 2 của TCMA Language Comparison Tool - một Windows desktop application cho phép so sánh và align nội dung localization giữa hai file CSV sử dụng AI embeddings.

## Tính năng chính

- **Giao diện trực quan**: Interface dễ sử dụng với file dialogs, progress bars, và result visualization
- **AI-powered matching**: Sử dụng Google Gemini embeddings để tìm matches dựa trên semantic similarity
- **Visual feedback**: Color-coded results (xanh lá = high quality, vàng = medium, hồng = low quality matches)
- **Export functionality**: Xuất file CSV đã được corrected theo thứ tự reference file
- **Real-time progress**: Progress tracking và status updates trong quá trình processing

## Yêu cầu hệ thống

- Windows 10/11
- .NET 8.0 Runtime
- Google Gemini API key

## Cách sử dụng

### 1. Khởi động ứng dụng
```bash
cd src/Tcma.LanguageComparison.Gui
dotnet run
```

### 2. Upload files
- **Reference File**: Click "Browse..." để chọn file CSV tham chiếu (thường là tiếng Anh)
- **Target File**: Click "Browse..." để chọn file CSV cần align (ví dụ: tiếng Hàn)

### 3. Nhập API Key
- Nhập Google Gemini API key vào field "Gemini API Key"
- Cần API key để generate embeddings cho content matching

### 4. So sánh files
- Click "Compare Files" để bắt đầu quá trình so sánh
- Ứng dụng sẽ:
  - Load và preprocess CSV files
  - Generate embeddings cho tất cả content
  - Tìm best matches dựa trên cosine similarity
  - Hiển thị kết quả trong DataGrid

### 5. Xem kết quả
- **DataGrid**: Hiển thị các matches được tìm thấy với color coding:
  - 🟢 **Xanh lá**: High quality matches (similarity > 0.7)
  - 🟡 **Vàng**: Medium quality matches (0.5 < similarity ≤ 0.7)
  - 🔴 **Hồng**: Low quality matches (similarity ≤ 0.5)
- **Statistics**: Thống kê tổng quan ở cuối DataGrid

### 6. Export kết quả
- Click "Export Results" để lưu file CSV đã được corrected
- File output sẽ có thứ tự rows aligned với reference file

## Cấu hình

### Similarity Threshold
Hiện tại tool sử dụng threshold thấp (0.35) để cải thiện cross-language matching. Có thể fine-tune sau dựa trên kết quả testing.

### CSV Format
Tool expects CSV files với format:
```csv
ContentId,Content
ID001,"Sample content with possible HTML tags"
ID002,"Another piece of content"
```

## Lưu ý kỹ thuật

### Cross-Language Matching
- Tool đã được optimize cho cross-language comparison
- Sử dụng threshold thấp (0.35) để capture semantic similarity giữa các ngôn ngữ khác nhau
- HTML tags được stripped trước khi generate embeddings

### Performance
- Batch processing cho embedding generation
- Rate limiting để tránh API limits
- Progress feedback để user biết processing status

### Error Handling
- File validation và error messages rõ ràng
- API error handling với user-friendly messages
- Graceful handling của network issues

## Troubleshooting

### API Key Issues
- Đảm bảo Gemini API key hợp lệ
- Check API quotas và billing settings
- Verify network connectivity

### File Format Issues
- Đảm bảo CSV files có correct format
- Check encoding (UTF-8 recommended)
- Verify column headers match expected format

### Performance Issues
- Large files có thể mất thời gian processing
- Network speed ảnh hưởng đến API calls
- Consider using demo mode cho testing với limited rows

## Ví dụ sử dụng

1. Load sample files từ `../../sample/` directory
2. Nhập API key
3. Click Compare Files
4. Xem results trong DataGrid với color-coded quality indicators
5. Export corrected file nếu results satisfactory

## Phát triển tiếp theo

- Configurable thresholds trong UI
- Manual override functionality
- Bulk processing support
- Advanced filtering và sorting options
- Export to different formats 