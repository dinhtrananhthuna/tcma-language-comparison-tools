# Ẩn Tab Multi-page Mode Tạm Thời

## Tổng quan
Tab "Multi-page Mode" đã được tạm ẩn để đưa cho người dùng sử dụng Single File Mode trước. Tab này có thể được hiển thị lại dễ dàng để tiếp tục phát triển.

## Thay đổi thực hiện

### 1. Ẩn Tab trong XAML
**File**: `src/Tcma.LanguageComparison.Gui/MainWindow.xaml`
**Dòng 99**: Thêm `Visibility="Collapsed"` vào TabItem
```xml
<TabItem Header="📚 Multi-Page Mode" x:Name="MultiPageTab" Visibility="Collapsed">
```

### 2. Cập nhật Logic Code-behind  
**File**: `src/Tcma.LanguageComparison.Gui/MainWindow.xaml.cs`
**Dòng 397**: Thêm kiểm tra visibility để tránh lỗi khi tab bị ẩn
```csharp
else if (ProcessingModeTabControl.SelectedItem == MultiPageTab && MultiPageTab.Visibility == Visibility.Visible)
```

## Cách khôi phục Tab Multi-page Mode

### Bước 1: Hiển thị lại Tab
Trong `MainWindow.xaml`, thay đổi:
```xml
<TabItem Header="📚 Multi-Page Mode" x:Name="MultiPageTab" Visibility="Collapsed">
```
Thành:
```xml
<TabItem Header="📚 Multi-Page Mode" x:Name="MultiPageTab">
```

### Bước 2: Tùy chọn - Đơn giản hóa Code-behind
Có thể xóa điều kiện `&& MultiPageTab.Visibility == Visibility.Visible` trong `MainWindow.xaml.cs` nếu muốn:
```csharp
else if (ProcessingModeTabControl.SelectedItem == MultiPageTab)
```

## Lợi ích của cách tiếp cận này

1. **Đơn giản**: Chỉ cần thay đổi 1-2 dòng code
2. **An toàn**: Tất cả code Multi-page được giữ nguyên, chỉ ẩn giao diện
3. **Dễ khôi phục**: Có thể bật lại bất cứ lúc nào
4. **Không ảnh hưởng**: Single File Mode hoạt động bình thường
5. **Phát triển tiếp**: Vẫn có thể phát triển Multi-page ở background

## Trạng thái hiện tại
- ✅ Single File Mode: Hoạt động đầy đủ
- 🔒 Multi-page Mode: Ẩn tạm thời (code vẫn hoàn chỉnh)
- ✅ Settings, Export: Hoạt động bình thường
- ✅ Alignment bug đã được fix
- ✅ UTF-8 encoding đã được fix

## Kế hoạch tương lai
1. Đưa Single File Mode cho người dùng sử dụng
2. Thu thập feedback và cải thiện
3. Tiếp tục phát triển Multi-page Mode  
4. Hiển thị lại khi ready
5. Phát hành version đầy đủ 