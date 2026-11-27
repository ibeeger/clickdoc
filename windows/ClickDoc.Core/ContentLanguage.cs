using System.Globalization;

namespace ClickDoc.Core;

/// Language of generated content (step descriptions, Markdown/PDF text). Follows the UI
/// language: Chinese when Windows displays Chinese, English otherwise.
public enum ContentLanguage { Chinese, English }

public static class ContentLanguageExtensions
{
    static ContentLanguage? _override;

    extension(ContentLanguage)
    {
        /// Set by tests; null in the app.
        public static ContentLanguage? Override
        {
            get => _override;
            set => _override = value;
        }

        public static ContentLanguage Current => _override ?? ContentLanguage.Resolve(CultureInfo.CurrentUICulture.Name);

        public static ContentLanguage Resolve(string? cultureName) =>
            cultureName?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true ? ContentLanguage.Chinese : ContentLanguage.English;
    }

    extension(ContentLanguage lang)
    {
        public string UntitledTitle => lang == ContentLanguage.Chinese ? "未命名教程" : "Untitled Tutorial";

        public string ImageProcessingFailed => lang == ContentLanguage.Chinese ? "处理截图失败" : "Could not process the screenshot";

        public string StepHeading(int n) => lang == ContentLanguage.Chinese ? $"步骤 {n}" : $"Step {n}";

        internal string Describe(ClickAction action, string? app, string? element)
        {
            if (lang == ContentLanguage.Chinese)
            {
                var verb = action switch { ClickAction.DoubleClick => "双击", ClickAction.RightClick => "右键点击", _ => "点击" };
                return (app is null ? "" : $"在 {app} 中") + verb + (element is null ? "" : $"「{element}」");
            }
            var en = action switch { ClickAction.DoubleClick => "Double-click", ClickAction.RightClick => "Right-click", _ => "Click" };
            return en + (element is null ? "" : $" \"{element}\"") + (app is null ? "" : $" in {app}");
        }
    }
}
