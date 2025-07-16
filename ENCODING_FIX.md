# Sửa lỗi Encoding CSV - Giữ nguyên ký tự đặc biệt

## Vấn đề đã giải quyết

Trước đây, khi export file CSV có chứa ký tự đặc biệt (như tiếng Đức: ä, ö, ü, ß; tiếng Việt: á, à, ằ, ắ; hay các ngôn ngữ khác), những ký tự này không được giữ nguyên trong file output mà bị hiển thị sai.

### Nguyên nhân:
- Các `StreamWriter` và `StreamReader` không chỉ định encoding rõ ràng
- Hệ thống sử dụng encoding mặc định (thường là ASCII hoặc UTF-8 không có BOM)
- CsvHelper configuration không được cấu hình encoding phù hợp

## Giải pháp triển khai

### 1. **Cập nhật StreamWriter cho Export (3 methods)**

**Trước:**
```csharp
using var writer = new StreamWriter(filePath);
```

**Sau:**
```csharp
using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
```

**Các method được cập nhật:**
- `ExportTargetFileAsync()` - Export line-by-line results
- `ExportAlignedTargetRowsAsync()` - Export aligned target results  
- `ExportAlignedDisplayRowsAsync()` - Export display data (bao gồm unmatched targets)

### 2. **Cập nhật StreamReader cho Import**

**Trước:**
```csharp
using var reader = new StreamReader(filePath);
```

**Sau:**
```csharp
using var reader = new StreamReader(filePath, Encoding.UTF8);
```

**Method được cập nhật:**
- `ReadContentRowsAsync()` - Đọc CSV input files

### 3. **Cải thiện CSV Configuration**

**Trước:**
```csharp
return new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    TrimOptions = TrimOptions.Trim,
    MissingFieldFound = null,
    BadDataFound = null
};
```

**Sau:**
```csharp
return new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    TrimOptions = TrimOptions.Trim,
    MissingFieldFound = null,
    BadDataFound = null,
    Encoding = Encoding.UTF8 // Đảm bảo UTF-8 cho ký tự đặc biệt
};
```

## Kết quả

### ✅ **Trước khi sửa:**
- Ký tự tiếng Đức: `Müller` → `M?ller` hoặc `MÃ¼ller`
- Ký tự tiếng Việt: `Nguyễn` → `Nguy?n` hoặc `NguyÃªn`

### ✅ **Sau khi sửa:**
- Ký tự tiếng Đức: `Müller` → `Müller` ✓
- Ký tự tiếng Việt: `Nguyễn` → `Nguyễn` ✓
- Các ký tự khác: `čšř`, `åäö`, `ñáé` đều được giữ nguyên ✓

## Tương thích

- **UTF-8 with BOM**: Đảm bảo tương thích với Excel và các ứng dụng CSV khác
- **Cross-platform**: Hoạt động nhất quán trên Windows, macOS, Linux
- **Backward compatible**: Các file CSV cũ vẫn đọc được bình thường

## Testing

Để test encoding fix:

1. **Tạo file CSV test** với nội dung đặc biệt:
```csv
ContentId,Content
DE001,"Müller Straße"
VN001,"Nguyễn Văn An"
FR001,"Café français"
```

2. **Chạy tool** và so sánh với file target tương tự

3. **Kiểm tra file export** đảm bảo ký tự không bị thay đổi

4. **Mở bằng Excel/Notepad++** để verify encoding

## Lưu ý kỹ thuật

- **UTF-8 với BOM**: Đảm bảo Excel đọc được ký tự đặc biệt
- **Performance**: Không ảnh hưởng đáng kể đến tốc độ xử lý
- **Memory**: Encoding UTF-8 hiệu quả về memory cho hầu hết ngôn ngữ
- **File size**: Có thể tăng nhẹ file size do BOM header (3 bytes) 