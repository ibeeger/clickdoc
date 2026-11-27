using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ClickDoc.Editor;

/// The split export button: the main part exports a Markdown folder, the arrow offers every format.
sealed class ExportMenu : StackPanel
{
    public ExportMenu(AppState state, Window owner)
    {
        Orientation = Orientation.Horizontal;
        var main = new Button { Content = L("导出"), MinWidth = 72 };
        main.Click += (_, _) => state.Export(ExportFormat.MarkdownFolder, owner);
        var arrow = new Button { Content = "▾", Margin = new Thickness(1, 0, 0, 0), Padding = new Thickness(8, 0, 8, 0) };
        var menu = new ContextMenu { PlacementTarget = arrow, Placement = PlacementMode.Bottom };
        foreach (var (title, format) in new[]
                 {
                     (L("Markdown 文件夹…"), ExportFormat.MarkdownFolder),
                     (L("Notion 导入包（.zip）…"), ExportFormat.NotionZip),
                     ("PDF…", ExportFormat.Pdf),
                 })
        {
            var item = new MenuItem { Header = new TextBlock { Text = title } };
            item.Click += (_, _) => state.Export(format, owner);
            menu.Items.Add(item);
        }
        arrow.Click += (_, _) => menu.IsOpen = true;
        Children.Add(main);
        Children.Add(arrow);
    }
}
