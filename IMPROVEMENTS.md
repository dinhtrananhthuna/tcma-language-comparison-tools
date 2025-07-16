# Cải tiến: Hiển thị dòng target không match

## Tổng quan

Tôi đã cải tiến logic so sánh của tool để có thể hiển thị và xuất những dòng target không match với bất kỳ dòng reference nào. Điều này giúp người dùng có cái nhìn hoàn chính về tất cả nội dung trong file target.

## Các thay đổi chính

### 1. Model mở rộng (MatchResult.cs)

- **Thêm enum `AlignedRowType`**: Phân biệt giữa dòng reference-aligned và unmatched target
- **Mở rộng `AlignedDisplayRow`**: 
  - Thêm `RowType` property để xác định loại dòng
  - Cập nhật `RowBackground` để hỗ trợ màu nền khác cho unmatched target (vàng nhạt #FFF8DC)
  - Thêm factory method `FromUnmatchedTargetRow()` để tạo display row từ target row không match

### 2. Logic so sánh cải tiến (ContentMatchingService.cs)

- **`GenerateAlignedDisplayDataAsync()`**: 
  - Bây giờ sử dụng lại logic từ `GenerateAlignedTargetFileAsync()`
  - Thêm các dòng target không match ở cuối danh sách hiển thị
  - Sắp xếp unmatched rows theo `OriginalIndex`

- **Thuật toán matching**: Vẫn giữ nguyên thuật toán optimal bipartite matching, chỉ mở rộng hiển thị

### 3. UI cải thiện (MainWindow.xaml)

- **Thêm cột "Row Type"** trong cả hai DataGrid (Single File và Multi-Page mode)
- **Tự động styling**: Dòng unmatched target sẽ có nền màu vàng nhạt để dễ nhận biết
- **Hiển thị thông tin đầy đủ**: Status sẽ hiển thị "Unmatched Target" cho những dòng này

### 4. Export mở rộng (CsvReaderService.cs)

- **`ExportAlignedDisplayRowsAsync()`**: 
  - Bây giờ xuất tất cả các loại dòng, bao gồm unmatched target
  - Thêm cột "RowType" để phân biệt trong file export
  - Unmatched target rows sẽ có status "Unmatched Target" và RowType "Extra Target"

### 5. Statistics cập nhật (MainWindow.xaml.cs)

- **Thống kê mở rộng**: Hiển thị thêm thông tin về target rows
- **Format mới**: `Target: {totalTargetRows} total, {unmatchedTargetCount} unmatched`

## Cách sử dụng

### Trong DataGrid
1. **Dòng reference-aligned**: Có nền theo quality (xanh/cam/đỏ/xám)
2. **Dòng unmatched target**: Có nền vàng nhạt (#FFF8DC)
3. **Cột Row Type**: Hiển thị "ReferenceAligned" hoặc "UnmatchedTarget"

### Trong file export
- **Dòng aligned**: Status = "Matched"/"Missing", RowType = "Reference Aligned"
- **Dòng unmatched**: Status = "Unmatched Target", RowType = "Extra Target"
- **Thứ tự**: Reference aligned rows trước, unmatched target rows ở cuối

## Ví dụ kết quả

### Trong DataGrid:
| Ref Line # | Reference Content | Target Line # | Target Content | Status | Row Type | Similarity Score |
|------------|-------------------|---------------|----------------|--------|----------|------------------|
| 1 | "Hello" | 1 | "안녕하세요" | Matched | ReferenceAligned | 0.852 |
| 2 | "Good morning" | 3 | "좋은 아침입니다" | Matched | ReferenceAligned | 0.789 |
| 3 | "Thank you" | | | Missing | ReferenceAligned | |
| | | 2 | "추가 내용" | Unmatched Target | UnmatchedTarget | |

### Trong file CSV:
```csv
ContentId,Content,Status,SimilarityScore,RowType
ID1,"안녕하세요",Matched,0.852,Reference Aligned
ID3,"좋은 아침입니다",Matched,0.789,Reference Aligned
,"",Missing,,Reference Aligned
ID2,"추가 내용",Unmatched Target,,Extra Target
```

## Lợi ích

1. **Phát hiện nội dung thừa**: Không bỏ sót bất kỳ dòng nào trong file target
2. **Đánh giá hoàn chỉnh**: Biết được file target có bao nhiêu dòng không match
3. **Xuất đầy đủ**: File export chứa toàn bộ thông tin, không mất dữ liệu
4. **Phân biệt rõ ràng**: Màu sắc và cột Row Type giúp dễ nhận biết các loại dòng
5. **Thống kê chính xác**: Hiển thị số liệu thực tế về cả reference và target

## Tương thích ngược

- Tất cả API hiện tại vẫn hoạt động bình thường
- Chỉ mở rộng thêm tính năng, không thay đổi behavior cũ
- File export cũ vẫn tương thích, chỉ thêm cột và dòng mới 