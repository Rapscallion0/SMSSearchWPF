# WPF Intellisense Integration Suggestion

This document outlines how to implement a custom WPF-based Intellisense UI using standard controls (`ItemsControl` or `ListBox`) instead of AvalonEdit's built-in `CompletionWindow`.

## Architecture

1.  **ViewModel**: Create a `CompletionViewModel` to manage the completion state:
    *   `ObservableCollection<CompletionItem> Items`: The data source.
    *   `CompletionItem SelectedItem`: The currently highlighted item.
    *   `string FilterText`: The text typed by the user to filter results.
    *   `bool IsVisible`: Controls the visibility of the completion popup.
    *   `double X`, `double Y`: Position coordinates relative to the editor.

2.  **View**: Use a `Popup` control (or a windowless transparent overlay) containing a `ListBox` or `ItemsControl`.
    *   Bind `ItemsSource` to `Items`.
    *   Bind `SelectedItem` to `SelectedItem`.
    *   Use a `CollectionViewSource` to handle filtering efficiently based on `FilterText`.

## Implementation Details

### 1. Binding to ItemsControl

```xml
<Popup IsOpen="{Binding IsVisible}" Placement="Bottom" HorizontalOffset="{Binding X}" VerticalOffset="{Binding Y}" StaysOpen="False">
    <Border Background="White" BorderBrush="Gray" BorderThickness="1" CornerRadius="2" Padding="2">
        <ListBox x:Name="CompletionList"
                 ItemsSource="{Binding Items}"
                 SelectedItem="{Binding SelectedItem}"
                 MaxHeight="200" Width="300"
                 ScrollViewer.HorizontalScrollBarVisibility="Disabled">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <Grid Margin="2">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="20"/>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <!-- Icon based on Type (e.g., Table vs Column) -->
                        <Image Source="{Binding Type, Converter={StaticResource TypeToIconConverter}}" Width="16" Height="16"/>

                        <!-- Main Text -->
                        <TextBlock Text="{Binding Text}" FontWeight="Bold" Grid.Column="1" VerticalAlignment="Center" Margin="5,0,0,0"/>

                        <!-- Description/Type Hint -->
                        <TextBlock Text="{Binding Description}" Foreground="Gray" Grid.Column="2" FontStyle="Italic" VerticalAlignment="Center" Margin="10,0,0,0"/>
                    </Grid>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>
    </Border>
</Popup>
```

### 2. Interaction Logic (Code-Behind or Behavior)

*   **Keyboard Interception**: Handle `PreviewKeyDown` on the Editor (TextBox/AvalonEdit).
    *   `Down`/`Up`: Move selection in the `CompletionList` (update `SelectedItem` in ViewModel).
    *   `Tab`/`Enter`: Commit selection (insert text into editor and close popup).
    *   `Escape`: Set `IsVisible = false`.
*   **Filtering**:
    *   On `TextChanged`, update `FilterText` in the ViewModel.
    *   Implement `CollectionViewSource.Filter` logic: `return item.Text.StartsWith(FilterText, StringComparison.OrdinalIgnoreCase);`.

### 3. Positioning

To position the popup correctly near the caret:
*   Use `TextBox.GetRectFromCharacterIndex(caretIndex)` (for standard TextBox).
*   Use `TextArea.TextView.GetVisualPosition(TextArea.Caret.Position, VisualYPosition.LineBottom)` (for AvalonEdit).
*   Convert these coordinates to screen or window-relative points and update `X`/`Y` properties in the ViewModel.

## Pros/Cons vs AvalonEdit CompletionWindow

### Pros
*   **Full Customization**: Complete control over styling, templates, animations, and behavior.
*   **MVVM Integration**: Easier to integrate with complex MVVM patterns and dependency injection.
*   **Accessibility**: Standard controls like `ListBox` often have better built-in accessibility support than custom overlays.

### Cons
*   **Complexity**: Re-implementing core editor features (caret positioning, overlay management, keyboard interception, scrolling synchronization) is complex and error-prone.
*   **Performance**: Handling filtering and UI updates manually for large lists can be slower than AvalonEdit's optimized implementation.
*   **Maintenance**: Requires maintaining custom code for standard editor behavior that AvalonEdit handles out-of-the-box.
