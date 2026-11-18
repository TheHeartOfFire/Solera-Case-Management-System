using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Core.Utils;
using AMFormsCST.Desktop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop.Views.Dialogs;

public partial class AutoCloseDialog : FluentWindow
{
    private readonly NoteModel _note;
    private readonly ITemplateAssemblyService _assemblyService;
    private readonly IUserSettings? _userSettings;
    private readonly List<TextTemplate> _closureTemplates;
    private string? _fallbackEmail;

    public string ResultClosureComment { get; private set; } = string.Empty;
    public CaseTargetStatus ResultStatus { get; private set; } = CaseTargetStatus.Closed;
    public string ResultKbArticles { get; private set; } = string.Empty;

    public AutoCloseDialog(
        NoteModel note,
        ITemplateAssemblyService assemblyService,
        IUserSettings? userSettings,
        IEnumerable<TextTemplate>? templates)
    {
        _note = note;
        _assemblyService = assemblyService;
        _userSettings = userSettings;

        var fromEnforcer = templates?
            .Where(t => t.Type == TextTemplate.TemplateType.ClosureComments)
            .ToList() ?? [];

        _closureTemplates = fromEnforcer.Count > 0
            ? fromEnforcer
            : DefaultTemplatesProvider.GetDefaultClosureTemplates();

        InitializeComponent();

        ClosureTemplateCombo.ItemsSource = _closureTemplates;
        if (_closureTemplates.Count > 0)
        {
            ClosureTemplateCombo.SelectedIndex = 0;
        }
        else
        {
            ClosureCommentBox.Text = _assemblyService.AssembleClosureRecap(_note.CoreType!, null, _userSettings);
        }

        RunValidation();
    }

    private void CustomTitleBarArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void TargetStatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        RunValidation();
    }

    private void ClosureTemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ClosureTemplateCombo.SelectedItem is TextTemplate selectedTemplate && _note.CoreType != null)
        {
            ClosureCommentBox.Text = _assemblyService.AssembleClosureRecap(_note.CoreType, selectedTemplate, _userSettings);
        }
        if (IsLoaded) RunValidation();
    }

    private void FieldBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) RunValidation();
    }

    private void RunValidation()
    {
        var status = TargetStatusCombo.SelectedIndex == 1
            ? CaseTargetStatus.Resolved
            : CaseTargetStatus.Closed;

        var request = new CaseValidationRequest
        {
            Note = _note.CoreType,
            TargetStatus = status,
            ParentProduct = ParentProductBox.Text,
            CaseReason = CaseReasonBox.Text,
            SubCategory = SubCategoryBox.Text,
            SupportCategory = SupportCategoryBox.Text,
            ClosureComment = ClosureCommentBox.Text
        };

        var result = CaseClosureValidator.Validate(request);
        ValidationIssuesList.ItemsSource = result.Issues;
        _fallbackEmail = result.FallbackEmailFromDescription;
        ApplyDescriptionEmailBtn.Visibility = string.IsNullOrWhiteSpace(_fallbackEmail)
            ? Visibility.Collapsed
            : Visibility.Visible;

        ValidationSummaryText.Text = result.IsValid
            ? "All required Salesforce fields and comments are ready."
            : $"{result.Issues.Count} item(s) need attention before closing:";
    }

    private void ApplyDescriptionEmailBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_fallbackEmail) && _note.SelectedContact != null)
        {
            _note.SelectedContact.Email = _fallbackEmail;
            _note.UpdateCore();
            RunValidation();
        }
    }

    private void CopyClosureBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ClosureCommentBox.Text))
        {
            Clipboard.SetText(ClosureCommentBox.Text);
        }
    }

    private void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        ResultClosureComment = ClosureCommentBox.Text;
        ResultStatus = TargetStatusCombo.SelectedIndex == 1 ? CaseTargetStatus.Resolved : CaseTargetStatus.Closed;
        ResultKbArticles = KbArticlesBox.Text;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

