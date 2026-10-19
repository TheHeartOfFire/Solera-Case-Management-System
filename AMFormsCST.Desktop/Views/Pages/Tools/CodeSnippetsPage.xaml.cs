using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.CodeBlocks;
using AMFormsCST.Desktop.ControlsLookup;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;
using Nodify;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop.Views.Pages.Tools;

/// <summary>
/// Interaction logic for CodeSnippetsPage.xaml
/// </summary>
[GalleryPage("Here you can find some common snippets of code for use in FormGen programming", SymbolRegular.Code20)]
public partial class CodeSnippetsPage : Page
{
    private const string DragDataBlockNode = "PuzzleBlockNode";
    private const string DragDataToolboxTemplate = "PuzzleToolboxTemplate";
    private const string DragDataToolboxValue = "PuzzleToolboxValue";

    public CodeSnippetsViewModel ViewModel { get; }
    private readonly ILogService? _logger;

    private Point _dragStartPoint;
    private bool _isDraggingPossible;
    private INodeViewModel? _draggingCanvasRootNode;
    private CodeInputViewModel? _highlightedSocket;
    private readonly List<ScrollViewer> _constrainedScrollViewers = [];
    private readonly List<Frame> _constrainedFrames = [];

    private NavigationView? _hostNavigationView;
    private Thickness _savedNavPadding;
    private Thickness _savedHeaderMargin;
    private FrameworkElement? _savedHeaderElement;

    public CodeSnippetsPage(CodeSnippetsViewModel viewModel, ILogService? logger = null)
    {
        ViewModel = viewModel;
        _logger = logger;
        DataContext = ViewModel;

        InitializeComponent();
        ViewModel.RecenterRequested += OnRecenterRequested;
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;

        // Bind Ctrl+V to paste and parse FormGen code when focus is not inside an editable TextBox
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, (s, e) =>
        {
            if (Keyboard.FocusedElement is not System.Windows.Controls.TextBox tb || tb.IsReadOnly)
            {
                ViewModel.PasteFromClipboard();
                e.Handled = true;
            }
        }));

        _logger?.LogInfo("CodeSnippetsPage initialized.");
    }

    private void OnRecenterRequested()
    {
        NodifyEditorControl.ViewportZoom = ViewModel.ViewportZoom;
        NodifyEditorControl.ViewportLocation = ViewModel.ViewportLocation;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        ApplyHostLayoutConstraints();
        Application.Current?.Dispatcher?.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(ApplyHostLayoutConstraints));
    }

    private void ApplyHostLayoutConstraints()
    {
        // Constrain page height when hosted inside MainWindow's NavigationView ScrollViewer
        // so the canvas does not stretch vertically and push the Formula Tester / Generated Code off-screen.
        _constrainedScrollViewers.Clear();
        _constrainedFrames.Clear();

        DependencyObject? current = VisualTreeHelper.GetParent(this);
        while (current != null)
        {
            if (current is ScrollViewer sv)
            {
                sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                _constrainedScrollViewers.Add(sv);
            }
            else if (current is Frame frame)
            {
                ScrollViewer.SetVerticalScrollBarVisibility(frame, ScrollBarVisibility.Disabled);
                ScrollViewer.SetHorizontalScrollBarVisibility(frame, ScrollBarVisibility.Disabled);
                _constrainedFrames.Add(frame);
            }
            else if (current is NavigationView navigationView)
            {
                if (_hostNavigationView == null)
                {
                    _hostNavigationView = navigationView;
                    _savedNavPadding = navigationView.Padding;
                    navigationView.Padding = new Thickness(0);

                    if (navigationView.Header is FrameworkElement headerElement)
                    {
                        _savedHeaderElement = headerElement;
                        _savedHeaderMargin = headerElement.Margin;
                        // Keep header margin tastefully small and aligned with the page edge
                        headerElement.Margin = new Thickness(12, 10, 12, 2);
                    }
                }
                navigationView.IsBackButtonVisible = NavigationViewBackButtonVisible.Visible;
            }
            current = VisualTreeHelper.GetParent(current);
        }

        // In PageHostDialog (popup from Dashboard), leave a 56px bottom margin under the Generated Code bar
        // so the dialog 'X' close button in the bottom-right corner displays cleanly without overlapping.
        // In MainWindow (from Tools page), use consistent, tastefully small margins (10px all around).
        var hostWindow = Window.GetWindow(this);
        if (hostWindow is Views.Dialogs.PageHostDialog)
        {
            RootLayoutGrid.Margin = new Thickness(6, 4, 6, 56);
        }
        else if (hostWindow != null)
        {
            RootLayoutGrid.Margin = new Thickness(10, 4, 10, 10);
        }
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        foreach (var sv in _constrainedScrollViewers)
        {
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
        foreach (var frame in _constrainedFrames)
        {
            ScrollViewer.SetVerticalScrollBarVisibility(frame, ScrollBarVisibility.Auto);
        }
        _constrainedScrollViewers.Clear();
        _constrainedFrames.Clear();

        if (_hostNavigationView != null)
        {
            _hostNavigationView.Padding = _savedNavPadding;
            if (_savedHeaderElement != null)
            {
                _savedHeaderElement.Margin = _savedHeaderMargin;
                _savedHeaderElement = null;
            }
            _hostNavigationView = null;
        }
    }

    #region Toolbox Drag Initiation

    private void ToolboxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) != null)
        {
            return;
        }
        _dragStartPoint = e.GetPosition(this);
        _isDraggingPossible = true;
    }

    private void ToolboxItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingPossible || e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(this);
        if ((pos - _dragStartPoint).Length < SystemParameters.MinimumHorizontalDragDistance) return;

        _isDraggingPossible = false;
        if (sender is FrameworkElement fe)
        {
            ICodeBase? template = fe.DataContext switch
            {
                CodeSnippetItemViewModel itemVm => itemVm.CodeBase,
                ICodeBase cb => cb,
                _ => null
            };

            if (template != null)
            {
                var data = new DataObject(DragDataToolboxTemplate, template);
                DragDrop.DoDragDrop(fe, data, DragDropEffects.Copy | DragDropEffects.Move);
                ClearSocketHighlight();
            }
        }
    }

    private void ToolboxValueButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
        _isDraggingPossible = true;
    }

    private void ToolboxValueButton_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingPossible || e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(this);
        if ((pos - _dragStartPoint).Length < SystemParameters.MinimumHorizontalDragDistance) return;

        _isDraggingPossible = false;
        if (sender is FrameworkElement fe)
        {
            var data = new DataObject(DragDataToolboxValue, UserInputViewModel.TypeFieldRaw);
            DragDrop.DoDragDrop(fe, data, DragDropEffects.Copy | DragDropEffects.Move);
            ClearSocketHighlight();
        }
    }

    #endregion

    #region Nested Puzzle Block Header Drag Initiation

    private void PuzzleHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) != null ||
            FindAncestor<ComboBox>(e.OriginalSource as DependencyObject) != null)
        {
            return;
        }

        if (sender is FrameworkElement fe && fe.DataContext is INodeViewModel nodeVm)
        {
            ViewModel.OnNodeSelected(nodeVm);
            if (nodeVm.IsDocked)
            {
                _dragStartPoint = e.GetPosition(this);
                _isDraggingPossible = true;
                e.Handled = true;
            }
        }
    }

    private void PuzzleHeader_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingPossible || e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(this);
        if ((pos - _dragStartPoint).Length < SystemParameters.MinimumHorizontalDragDistance) return;

        if (sender is FrameworkElement fe && fe.DataContext is INodeViewModel nodeVm && nodeVm.IsDocked)
        {
            _isDraggingPossible = false;
            var data = new DataObject(DragDataBlockNode, nodeVm);
            DragDrop.DoDragDrop(fe, data, DragDropEffects.Move);
            ClearSocketHighlight();
            e.Handled = true;
        }
    }

    #endregion

    #region Root Canvas Block Magnetic Snapping into Sockets

    private void ItemContainer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) != null ||
            FindAncestor<System.Windows.Controls.TextBox>(e.OriginalSource as DependencyObject) != null ||
            FindAncestor<ComboBox>(e.OriginalSource as DependencyObject) != null)
        {
            return;
        }

        if (sender is ItemContainer container && container.DataContext is INodeViewModel rootNode)
        {
            _draggingCanvasRootNode = rootNode;
            ViewModel.OnNodeSelected(rootNode);
        }
    }

    private void NodifyEditorControl_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingCanvasRootNode == null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var socket = FindSocketUnderMouse(e.GetPosition(NodifyEditorControl), _draggingCanvasRootNode);
        SetSocketHighlight(socket);
    }

    private void NodifyEditorControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingCanvasRootNode != null)
        {
            var draggedNode = _draggingCanvasRootNode;
            _draggingCanvasRootNode = null;

            var targetSocket = _highlightedSocket ?? FindSocketUnderMouse(e.GetPosition(NodifyEditorControl), draggedNode);
            ClearSocketHighlight();

            if (targetSocket != null)
            {
                ViewModel.DockBlockIntoSlot(draggedNode, targetSocket);
            }
        }
    }

    private CodeInputViewModel? FindSocketUnderMouse(Point pointInEditor, INodeViewModel draggedRoot)
    {
        CodeInputViewModel? foundSocket = null;

        VisualTreeHelper.HitTest(
            NodifyEditorControl,
            null,
            result =>
            {
                var hitElement = result.VisualHit;
                while (hitElement != null && hitElement != NodifyEditorControl)
                {
                    if (hitElement is FrameworkElement fe &&
                        fe.Tag as string == "PuzzleSocket" &&
                        fe.DataContext is CodeInputViewModel socketVm)
                    {
                        // Ignore sockets belonging to the dragged block itself or its descendants
                        var socketRoot = socketVm.Parent is INodeViewModel parentNode
                            ? CodeSnippetsViewModel.GetRootNode(parentNode)
                            : null;

                        if (!ReferenceEquals(socketRoot, draggedRoot))
                        {
                            foundSocket = socketVm;
                            return HitTestResultBehavior.Stop;
                        }
                    }
                    hitElement = VisualTreeHelper.GetParent(hitElement);
                }
                return HitTestResultBehavior.Continue;
            },
            new PointHitTestParameters(pointInEditor));

        return foundSocket;
    }

    private void SetSocketHighlight(CodeInputViewModel? socket)
    {
        if (ReferenceEquals(_highlightedSocket, socket)) return;
        if (_highlightedSocket != null)
        {
            _highlightedSocket.IsDragOver = false;
        }
        _highlightedSocket = socket;
        if (_highlightedSocket != null)
        {
            _highlightedSocket.IsDragOver = true;
        }
    }

    private void ClearSocketHighlight()
    {
        SetSocketHighlight(null);
    }

    #endregion

    #region Socket & Canvas Drop Handlers

    private void SocketBorder_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CodeInputViewModel socketVm)
        {
            SetSocketHighlight(socketVm);
            e.Effects = DragDropEffects.Move | DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void SocketBorder_DragOver(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CodeInputViewModel socketVm)
        {
            SetSocketHighlight(socketVm);
            e.Effects = DragDropEffects.Move | DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void SocketBorder_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CodeInputViewModel socketVm &&
            ReferenceEquals(_highlightedSocket, socketVm))
        {
            ClearSocketHighlight();
        }
    }

    private void SocketBorder_Drop(object sender, DragEventArgs e)
    {
        ClearSocketHighlight();
        if (sender is not FrameworkElement fe || fe.DataContext is not CodeInputViewModel socketVm)
        {
            return;
        }

        if (e.Data.GetDataPresent(DragDataBlockNode) &&
            e.Data.GetData(DragDataBlockNode) is INodeViewModel blockNode)
        {
            ViewModel.DockBlockIntoSlot(blockNode, socketVm);
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DragDataToolboxTemplate) &&
                 e.Data.GetData(DragDataToolboxTemplate) is ICodeBase template)
        {
            ViewModel.InsertBlockTemplateIntoSlot(template, socketVm);
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DragDataToolboxValue))
        {
            ViewModel.InsertValueBlockIntoSlot(socketVm);
            e.Handled = true;
        }
    }

    private void NodifyEditorControl_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DragDataBlockNode) ||
            e.Data.GetDataPresent(DragDataToolboxTemplate) ||
            e.Data.GetDataPresent(DragDataToolboxValue))
        {
            e.Effects = DragDropEffects.Move | DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void NodifyEditorControl_Drop(object sender, DragEventArgs e)
    {
        if (e.Handled) return;
        ClearSocketHighlight();

        var dropPos = NodifyEditorControl.GetLocationInsideEditor(e);

        if (e.Data.GetDataPresent(DragDataBlockNode) &&
            e.Data.GetData(DragDataBlockNode) is INodeViewModel blockNode)
        {
            ViewModel.UndockBlockToCanvas(blockNode, dropPos);
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DragDataToolboxTemplate) &&
                 e.Data.GetData(DragDataToolboxTemplate) is ICodeBase template)
        {
            ViewModel.AddBlockToCanvas(template, dropPos);
            e.Handled = true;
        }
        else if (e.Data.GetDataPresent(DragDataToolboxValue))
        {
            ViewModel.AddUserInputAt(dropPos);
            e.Handled = true;
        }
    }

    #endregion

    #region Collapsible & Resizable Right Explainer Panel

    private GridLength _savedExplainerWidth = new(300);

    private void ToggleExplainerButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsExplainerExpanded)
        {
            if (ExplainerColumn.ActualWidth >= 180)
            {
                _savedExplainerWidth = new GridLength(ExplainerColumn.ActualWidth);
            }
            ViewModel.IsExplainerExpanded = false;
            ExplainerColumn.MinWidth = 0;
            ExplainerColumn.Width = new GridLength(0);
        }
        else
        {
            ViewModel.IsExplainerExpanded = true;
            ExplainerColumn.MinWidth = 210;
            ExplainerColumn.Width = _savedExplainerWidth.Value >= 210 ? _savedExplainerWidth : new GridLength(300);
        }
        e.Handled = true;
    }

    #endregion

    #region Quick '+' Slot Insert Context Menu

    private void SlotInsertButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button || button.DataContext is not CodeInputViewModel targetSlot)
        {
            return;
        }

        var menu = new ContextMenu();

        // 1. Value / Field Block
        var addValueItem = new System.Windows.Controls.MenuItem
        {
            Header = "Insert Value / Field Block"
        };
        addValueItem.Click += (_, _) => ViewModel.InsertValueBlockIntoSlot(targetSlot);
        menu.Items.Add(addValueItem);

        // 2. Quoted String Value Block
        var addStringItem = new System.Windows.Controls.MenuItem
        {
            Header = "Insert Quoted String Block ('...')"
        };
        addStringItem.Click += (_, _) => ViewModel.InsertValueBlockIntoSlot(targetSlot, UserInputViewModel.TypeStringQuoted);
        menu.Items.Add(addStringItem);

        menu.Items.Add(new Separator());

        // 3. Functions Submenu (Categorized)
        var functionsMenu = new System.Windows.Controls.MenuItem
        {
            Header = "Insert Function Block"
        };
        if (ViewModel.FunctionCategories.Count > 0)
        {
            foreach (var category in ViewModel.FunctionCategories)
            {
                var catMenu = new System.Windows.Controls.MenuItem
                {
                    Header = category.Header
                };
                foreach (var func in category.Functions)
                {
                    var item = new System.Windows.Controls.MenuItem
                    {
                        Header = string.IsNullOrWhiteSpace(func.Prefix) ? func.Name : $"{func.Name} ({func.Prefix})"
                    };
                    var capturedFunc = func;
                    item.Click += (_, _) => ViewModel.InsertBlockTemplateIntoSlot(capturedFunc, targetSlot);
                    catMenu.Items.Add(item);
                }
                functionsMenu.Items.Add(catMenu);
            }
        }
        else
        {
            foreach (var func in ViewModel.AvailableBlocks)
            {
                var item = new System.Windows.Controls.MenuItem
                {
                    Header = string.IsNullOrWhiteSpace(func.Prefix) ? func.Name : $"{func.Name} ({func.Prefix})"
                };
                var capturedFunc = func;
                item.Click += (_, _) => ViewModel.InsertBlockTemplateIntoSlot(capturedFunc, targetSlot);
                functionsMenu.Items.Add(item);
            }
        }
        menu.Items.Add(functionsMenu);

        // 4. Snippets Submenu
        var snippetsMenu = new System.Windows.Controls.MenuItem
        {
            Header = "Insert Snippet (Formula)"
        };
        foreach (var snippet in ViewModel.AvailableSnippets)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = snippet.Name
            };
            var capturedSnippet = snippet;
            item.Click += (_, _) => ViewModel.InsertBlockTemplateIntoSlot(capturedSnippet, targetSlot);
            snippetsMenu.Items.Add(item);
        }
        menu.Items.Add(snippetsMenu);

        menu.PlacementTarget = button;
        menu.IsOpen = true;
        e.Handled = true;
    }

    #endregion

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}


