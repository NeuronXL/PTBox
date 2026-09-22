using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PTBox.Launcher.Models;
using PTBox.Launcher.Services;
using PTBox.Launcher.ViewModels;

namespace PTBox.Launcher.Views;

public sealed class AppArtworkConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length > 1 && values[0] is LauncherItem app && values[1] is string directory ? new TileViewModel(app,0,directory) : Binding.DoNothing;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
