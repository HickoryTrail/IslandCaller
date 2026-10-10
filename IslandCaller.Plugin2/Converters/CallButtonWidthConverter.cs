using System.Globalization;
using Avalonia.Data.Converters;

namespace IslandCaller.Converters;

/// <summary>
/// 把 Call 按钮宽度（double，单位像素）格式化为显示文本。
/// 0（或任何非正数）表示自动，显示「自动」而非数字。
/// </summary>
public class CallButtonWidthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double width && width > 0)
        {
            return ((int)Math.Round(width)).ToString(CultureInfo.CurrentCulture);
        }

        return "自动";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}
