using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;

namespace AMFormsCST.Desktop.Converters
{
    public class FlowDocumentToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is FlowDocument doc)
            {
                return new TextRange(doc.ContentStart, doc.ContentEnd).Text;
            }
            if (value is string xaml && !string.IsNullOrWhiteSpace(xaml))
            {
                // Try to parse as XAML FlowDocument first
                try
                {
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml)))
                    {
                        if (XamlReader.Load(stream) is FlowDocument fd)
                        {
                            return new TextRange(fd.ContentStart, fd.ContentEnd).Text;
                        }
                    }
                }
                catch
                {
                    // Fallback: maybe it's plain text?
                    return value;
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flowDocument = new FlowDocument();
            if (value is string text)
            {
                flowDocument.Blocks.Add(new Paragraph(new Run(text)));
            }
            return flowDocument;
        }
    }
}