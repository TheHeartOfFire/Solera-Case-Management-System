using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using WinForms = System.Windows.Forms;

namespace AMFormsCST.Desktop.Controls
{
    public partial class RichTextToolbar : UserControl
    {
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register(
                "Target",
                typeof(RichTextBox),
                typeof(RichTextToolbar),
                new PropertyMetadata(null, OnTargetChanged));

        public RichTextBox Target
        {
            get => (RichTextBox)GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }

        public RichTextToolbar()
        {
            InitializeComponent();
        }

        private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RichTextToolbar toolbar)
            {
                if (e.OldValue is RichTextBox oldRtb)
                {
                    oldRtb.SelectionChanged -= toolbar.OnRichTextBoxSelectionChanged;
                }
                if (e.NewValue is RichTextBox newRtb)
                {
                    newRtb.SelectionChanged += toolbar.OnRichTextBoxSelectionChanged;
                }
            }
        }

        private void OnRichTextBoxSelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateToolbar();
        }

        private void UpdateToolbar()
        {
            if (Target == null) return;

            BtnBold.IsChecked = IsSelectionPropertyActive(TextElement.FontWeightProperty, FontWeights.Bold);
            BtnItalic.IsChecked = IsSelectionPropertyActive(TextElement.FontStyleProperty, FontStyles.Italic);
            BtnUnderline.IsChecked = IsSelectionPropertyActive(TextDecorations.Underline);

            var currentList = GetSelectionList();
            BtnBullets.IsChecked = currentList?.MarkerStyle == TextMarkerStyle.Disc;
            BtnNumbering.IsChecked = currentList?.MarkerStyle == TextMarkerStyle.Decimal;
        }

        private List? GetSelectionList()
        {
            if (Target == null || Target.Selection.IsEmpty) return null;

            var startPara = Target.Selection.Start.Paragraph;
            var endPara = Target.Selection.End.Paragraph;

            if (startPara == null || endPara == null || startPara != endPara) return null;

            if (startPara.Parent is ListItem listItem)
            {
                return listItem.Parent as List;
            }

            return null;
        }

        private bool IsSelectionPropertyActive(DependencyProperty property, object expectedValue)
        {
            var value = Target.Selection.GetPropertyValue(property);
            return value != DependencyProperty.UnsetValue && value != null && value.Equals(expectedValue);
        }

        private bool IsSelectionPropertyActive(TextDecorationCollection expectedValue)
        {
            var value = Target.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
            return value != DependencyProperty.UnsetValue && value is TextDecorationCollection tdc && tdc.Any() && tdc.Equals(expectedValue);
        }

        private void BtnBold_Click(object sender, RoutedEventArgs e)
        {
            if (Target == null) return;
            EditingCommands.ToggleBold.Execute(null, Target);
            Target.Focus();
        }

        private void BtnItalic_Click(object sender, RoutedEventArgs e)
        {
            if (Target == null) return;
            EditingCommands.ToggleItalic.Execute(null, Target);
            Target.Focus();
        }

        private void BtnUnderline_Click(object sender, RoutedEventArgs e)
        {
            if (Target == null) return;
            EditingCommands.ToggleUnderline.Execute(null, Target);
            Target.Focus();
        }

        private void BtnBullets_Click(object sender, RoutedEventArgs e)
        {
            if (Target == null) return;
            EditingCommands.ToggleBullets.Execute(null, Target);
            Target.Focus();
        }

        private void BtnNumbering_Click(object sender, RoutedEventArgs e)
        {
            if (Target == null) return;
            EditingCommands.ToggleNumbering.Execute(null, Target);
            Target.Focus();
        }

        private void BtnDecreaseFont_Click(object sender, RoutedEventArgs e)
        {
            ChangeFontSize(-2);
        }

        private void BtnIncreaseFont_Click(object sender, RoutedEventArgs e)
        {
            ChangeFontSize(2);
        }

        private void ChangeFontSize(double delta)
        {
            if (Target == null) return;

            var currentSizeValue = Target.Selection.GetPropertyValue(TextElement.FontSizeProperty);
            if (currentSizeValue == DependencyProperty.UnsetValue || currentSizeValue == null) return;

            var currentSize = (double)currentSizeValue;
            var newSize = currentSize + delta;

            if (newSize < 1) newSize = 1;

            Target.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, newSize);
            Target.Focus();
        }

        private void BtnTextColor_Click(object sender, RoutedEventArgs e)
        {
            if (Target == null) return;

            using (var colorDialog = new WinForms.ColorDialog())
            {
                // Optionally set initial color based on selection
                var currentForeground = Target.Selection.GetPropertyValue(TextElement.ForegroundProperty);
                if (currentForeground != DependencyProperty.UnsetValue && currentForeground is SolidColorBrush solidBrush)
                {
                    colorDialog.Color = System.Drawing.Color.FromArgb(
                        solidBrush.Color.A, 
                        solidBrush.Color.R, 
                        solidBrush.Color.G, 
                        solidBrush.Color.B);
                }

                if (colorDialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    var newColor = Color.FromArgb(
                        colorDialog.Color.A, 
                        colorDialog.Color.R, 
                        colorDialog.Color.G, 
                        colorDialog.Color.B);

                    Target.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(newColor));
                    Target.Focus();
                }
            }
        }

    }
}