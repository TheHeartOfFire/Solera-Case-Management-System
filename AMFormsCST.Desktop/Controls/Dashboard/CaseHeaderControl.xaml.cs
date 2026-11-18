using System.Windows;
using System.Windows.Controls;

namespace AMFormsCST.Desktop.Controls.Dashboard;

public partial class CaseHeaderControl : UserControl
{
    public CaseHeaderControl()
    {
        InitializeComponent();
    }

    private void ContactName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && !string.IsNullOrEmpty(textBox.Text))
        {
            var trimmed = textBox.Text.Trim();
            if (!string.Equals(textBox.Text, trimmed, System.StringComparison.Ordinal))
            {
                textBox.Text = trimmed;
            }
        }
    }
}

