using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace CompanyDirectory_Desktop.Converters;

public class Base64ToImageSourceConverter : IValueConverter
{
    public static BitmapImage? FromBase64(string? base64)
    {
        if (string.IsNullOrEmpty(base64)) return null;
        try
        {
            var bytes = System.Convert.FromBase64String(base64);
            using var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteBytes(bytes);
            writer.StoreAsync().GetResults();
            writer.DetachStream();
            stream.Seek(0);
            var bitmap = new BitmapImage();
            bitmap.SetSource(stream);
            return bitmap;
        }
        catch { return null; }
    }

    public object? Convert(object value, Type targetType, object parameter, string language)
        => value is string s ? FromBase64(s) : null;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
