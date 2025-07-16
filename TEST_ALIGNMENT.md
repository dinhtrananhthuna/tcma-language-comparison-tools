# Test Alignment Algorithm - Hướng dẫn

## Mục đích

Test này được tạo để kiểm tra thuật toán alignment hoạt động đúng bằng cách:
1. So sánh file English (reference) và German (target) 
2. Xuất file aligned output
3. Phân tích ContentId consistency để đảm bảo alignment chính xác

## Chuẩn bị

### 1. Cài đặt API Key

Trước khi chạy test, bạn cần set Gemini API Key:

**Windows PowerShell:**
```powershell
$env:GEMINI_API_KEY="your_gemini_api_key_here"
```

**Windows Command Prompt:**
```cmd
set GEMINI_API_KEY=your_gemini_api_key_here
```

**Linux/Mac:**
```bash
export GEMINI_API_KEY="your_gemini_api_key_here"
```

### 2. Files được test

Test sẽ sử dụng 2 files có sẵn trong thư mục `sample/`:
- **Reference**: `NX Essentials Google Ads Page_20250529-EN.csv` (English)
- **Target**: `NX Essentials Google Ads Page_20250529-DE.csv` (German)

## Chạy Test

### Command để chạy test:

```bash
dotnet run --project src/Tcma.LanguageComparison.Core test
```

### Quá trình test:

1. **📂 Load Files**: Đọc reference và target files
2. **🔧 Preprocessing**: Xử lý và clean content  
3. **🧠 Generate Embeddings**: Tạo embeddings cho cả 2 files
4. **⚡ Run Alignment**: Chạy thuật toán alignment
5. **💾 Export Results**: Xuất kết quả ra file `test_output_aligned.csv`
6. **📊 Analysis**: Phân tích kết quả

## Kết quả Test

### Metrics được đo:

1. **Matched Rows**: Số dòng được match thành công
2. **Missing Rows**: Số dòng reference không có match
3. **Unmatched Target Rows**: Số dòng target không match với reference nào
4. **ContentId Consistency**: Phần trăm ContentId match chính xác

### Tiêu chí PASS:

- ✅ **Accuracy ≥ 80%**: ContentId consistency phải đạt 80% trở lên
- ✅ **No Critical Errors**: Không có lỗi trong quá trình xử lý
- ✅ **Export Success**: File output được tạo thành công

### Ví dụ output:

```
🧪 TCMA Alignment Test Mode
==================================================
🔑 API Key found, starting test...

🧪 Starting Alignment Test
==================================================
📂 Loading reference file (EN)...
   Loaded 60 reference rows
📂 Loading target file (DE)...
   Loaded 54 target rows

🔧 Preprocessing content...
🧠 Generating embeddings for reference...
🧠 Generating embeddings for target...

⚡ Running alignment algorithm...
   Generated 65 aligned display rows
💾 Exporting aligned result...

📊 Analyzing alignment results...
🔍 Analyzing in-memory alignment data:
   ✅ Matched rows: 45
   ❌ Missing rows: 15
   🔄 Unmatched target rows: 5

📈 ContentId Consistency Analysis:
   ✅ ContentId matches: 42/45
   📊 Accuracy: 93.33%

🎯 Examples of successful matches:
   ✅ ContentId: DE001
      Ref:    Get the power of NX X Essentials for your busine...
      Target: Holen Sie sich die Macht von NX X Essentials f...
      Score:  0.851

🏁 Test Summary:
   🎉 PASSED! Alignment algorithm working correctly.
   ✅ 93.3% accuracy meets the 80% threshold

🏁 Test completed!
```

## Phân tích kết quả nâng cao

### File output: `test_output_aligned.csv`

Sau khi test hoàn thành, kiểm tra file output:

```csv
ContentId,Content,Status,SimilarityScore,RowType
DE001,"Holen Sie sich die Macht von NX X Essentials","Matched","0.851","Reference Aligned"
DE002,"Erstellen Sie atemberaubende Designs","Matched","0.823","Reference Aligned"
...
DE999,"Extra German content","Unmatched Target","","Extra Target"
```

### Kiểm tra manual:

1. **Mở file reference** và chọn một ContentId (vd: DE015)
2. **Tìm trong output file** cùng ContentId  
3. **Verify** rằng content German match với content English tương ứng

## Troubleshooting

### Lỗi thường gặp:

1. **❌ API Key not set**
   - **Fix**: Set GEMINI_API_KEY environment variable

2. **❌ File not found**
   - **Fix**: Đảm bảo files EN và DE có trong thư mục `sample/`

3. **❌ Low accuracy < 80%**
   - **Nguyên nhân**: Bug trong thuật toán alignment
   - **Fix**: Cần debug và sửa lỗi algorithm

4. **❌ API rate limit**
   - **Fix**: Chờ vài phút rồi chạy lại

### Debug mode:

Để xem thêm chi tiết, bạn có thể modify code trong `AlignmentTest.cs` để log thêm thông tin.

## Kết luận

Test này đảm bảo rằng:
- ✅ Thuật toán alignment hoạt động chính xác
- ✅ ContentId được preserve đúng trong output
- ✅ Export file có format đúng và encoding UTF-8
- ✅ Bug ReferenceEquals đã được fix hoàn toàn

**Khi test PASS với accuracy ≥ 80%, bạn có thể tin tưởng tool hoạt động đúng trong production!** 🎉 