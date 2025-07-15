# Configuration Guide - TCMA Language Comparison Tools

## 📁 File cấu hình: `appsettings.json`

Tất cả settings có thể điều chỉnh trong file `appsettings.json` nằm trong thư mục project:

```
src/Tcma.LanguageComparison.Core/appsettings.json
```

## ⚙️ Các settings có thể điều chỉnh

### 🎯 Language Comparison Settings

```json
"LanguageComparison": {
  "SimilarityThreshold": 0.5,        // Ngưỡng similarity để coi là match tốt (0.0-1.0)
  "MaxEmbeddingBatchSize": 50,       // Số lượng embedding xử lý cùng lúc
  "MaxConcurrentRequests": 5,        // Số request API đồng thời tối đa
  "MinContentLength": 3,             // Độ dài nội dung tối thiểu
  "MaxContentLength": 8000,          // Độ dài nội dung tối đa cho API
  "DemoRowLimit": 10                 // Giới hạn số rows cho demo (0 = không giới hạn)
}
```

**💡 Điều chỉnh Similarity Threshold:**
- `0.8-1.0`: Rất strict - chỉ match những content rất giống
- `0.6-0.8`: Moderate - balance giữa precision và recall
- `0.4-0.6`: Relaxed - match nhiều hơn nhưng có thể có false positive
- `0.0-0.4`: Very loose - gần như match tất cả

### 📊 Output Settings

```json
"Output": {
  "ShowDetailedResults": true,        // Hiển thị bảng kết quả chi tiết
  "ShowProgressMessages": true,       // Hiển thị thông báo tiến độ
  "ExportUnmatchedAsPlaceholder": true // Export placeholder cho unmatched items
}
```

### 🧹 Preprocessing Settings

```json
"Preprocessing": {
  "StripHtmlTags": true,             // Loại bỏ HTML tags
  "NormalizeWhitespace": true,       // Chuẩn hóa khoảng trắng
  "RemoveSpecialCharacters": true    // Loại bỏ ký tự đặc biệt
}
```

## 🚀 Ví dụ Scenarios

### Scenario 1: Test với full dataset
```json
{
  "LanguageComparison": {
    "SimilarityThreshold": 0.7,
    "DemoRowLimit": 0  // Không giới hạn
  }
}
```

### Scenario 2: Strict matching cho production
```json
{
  "LanguageComparison": {
    "SimilarityThreshold": 0.8,
    "MaxConcurrentRequests": 3  // Giảm load API
  },
  "Output": {
    "ShowDetailedResults": false,  // Chỉ hiện statistics
    "ShowProgressMessages": false
  }
}
```

### Scenario 3: Debug mode
```json
{
  "LanguageComparison": {
    "SimilarityThreshold": 0.3,
    "DemoRowLimit": 5
  },
  "Output": {
    "ShowDetailedResults": true,
    "ShowProgressMessages": true
  }
}
```

## 📈 Recommendations

### 🎯 Similarity Threshold Guidelines:

| Content Type | Recommended Threshold | Lý do |
|--------------|---------------------|-------|
| Technical Terms | 0.8-0.9 | Thuật ngữ kỹ thuật cần chính xác cao |
| UI Text | 0.6-0.7 | Text giao diện có thể linh hoạt hơn |
| Marketing Content | 0.5-0.6 | Nội dung marketing có thể sáng tạo |
| Navigation/Menu | 0.7-0.8 | Menu cần consistency |

### ⚡ Performance Tuning:

- **Xử lý file lớn**: Tăng `MaxEmbeddingBatchSize` lên 100-200
- **API rate limit**: Giảm `MaxConcurrentRequests` xuống 2-3
- **Memory constraints**: Giảm `DemoRowLimit` cho testing

## 🔄 Real-time Configuration Updates

App sẽ load configuration mỗi lần chạy. Để áp dụng changes:

1. Edit `appsettings.json`
2. Save file
3. Restart app

## 📝 Configuration Backup

Nên backup file `appsettings.json` trước khi thay đổi:

```bash
cp appsettings.json appsettings.backup.json
```

## 🆘 Troubleshooting

**Config không load được:**
- Kiểm tra JSON syntax (dùng JSON validator)
- Đảm bảo file có trong cùng thư mục với executable

**Performance Issues:**
- Giảm `MaxConcurrentRequests` nếu API throttle
- Tăng `SimilarityThreshold` để giảm processing time

**Quality Issues:**
- Điều chỉnh `SimilarityThreshold` theo kết quả thực tế
- Test với sample nhỏ trước khi chạy full dataset 