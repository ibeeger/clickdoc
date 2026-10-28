using System.Windows.Markup;

namespace ClickDoc.Localization;

/// `{l:L 开始录制}` in XAML.
[MarkupExtensionReturnType(typeof(string))]
public sealed class LExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.L(key);
}
