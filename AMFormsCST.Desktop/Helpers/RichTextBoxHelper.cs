using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;

namespace AMFormsCST.Desktop.Helpers;

public static class RichTextBoxHelper
{
    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.RegisterAttached(
            "Document",
            typeof(FlowDocument),
            typeof(RichTextBoxHelper),
            new FrameworkPropertyMetadata(null, OnDocumentChanged));

    public static FlowDocument GetDocument(DependencyObject dp)
    {
        return (FlowDocument)dp.GetValue(DocumentProperty);
    }

    public static void SetDocument(DependencyObject dp, FlowDocument value)
    {
        dp.SetValue(DocumentProperty, value);
    }

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichTextBox rtb)
        {
            rtb.Document = e.NewValue as FlowDocument ?? new FlowDocument();
        }
    }

    public static readonly DependencyProperty TextChangedCommandProperty =
        DependencyProperty.RegisterAttached("TextChangedCommand", typeof(ICommand), typeof(RichTextBoxHelper), new PropertyMetadata(null, OnTextChangedCommandChanged));

    public static ICommand GetTextChangedCommand(DependencyObject obj)
    {
        return (ICommand)obj.GetValue(TextChangedCommandProperty);
    }

    public static void SetTextChangedCommand(DependencyObject obj, ICommand value)
    {
        obj.SetValue(TextChangedCommandProperty, value);
    }

    private static void OnTextChangedCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichTextBox rtb)
        {
            rtb.TextChanged -= RichTextBox_TextChanged; // Unsubscribe to prevent multiple subscriptions
            if (e.NewValue is ICommand)
            {
                rtb.TextChanged += RichTextBox_TextChanged;
            }
        }
    }

    private static void RichTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is RichTextBox rtb)
        {
            ICommand command = GetTextChangedCommand(rtb);
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }
        }
    }

    public static readonly DependencyProperty XamlProperty = DependencyProperty.RegisterAttached(
        "Xaml",
        typeof(string),
        typeof(RichTextBoxHelper),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnXamlChanged));

    public static string GetXaml(DependencyObject obj)
    {
        return (string)obj.GetValue(XamlProperty);
    }

    public static void SetXaml(DependencyObject obj, string value)
    {
        obj.SetValue(XamlProperty, value);
    }

    private static void OnXamlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RichTextBox rtb)
        {
            string xaml = GetXaml(rtb);
            string currentXaml = XamlWriter.Save(rtb.Document);

            if (xaml == currentXaml)
                return;

            rtb.TextChanged -= RichTextBox_TextChanged_UpdateXaml;
            if (string.IsNullOrWhiteSpace(xaml))
            {
                rtb.Document = new FlowDocument();
            }
            else
            {
                try
                {
                    using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml)))
                    {
                        rtb.Document = (FlowDocument)XamlReader.Load(stream);
                    }
                }
                catch
                {
                    rtb.Document = new FlowDocument();
                }
            }
            rtb.TextChanged += RichTextBox_TextChanged_UpdateXaml;
        }
    }

    private static void RichTextBox_TextChanged_UpdateXaml(object sender, TextChangedEventArgs e)
    {
        if (sender is RichTextBox rtb)
        {
            string xaml = XamlWriter.Save(rtb.Document);
            SetXaml(rtb, xaml);
        }
    }
}
