using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Desktop.BaseClasses;
using AMFormsCST.Desktop.Types;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog.Context;
using System.Collections.Specialized;
using System.ComponentModel;

namespace AMFormsCST.Desktop.Models
{
    public partial class Dealer : ManagedObservableCollectionItem
    {
        private bool _isInitializing;

        [ObservableProperty]
        private string? _name = string.Empty;
        [ObservableProperty]
        private string? _serverCode = string.Empty;
        [ObservableProperty]
        private bool _notable = true;

        public ManagedObservableCollection<Company> Companies { get; set; }

        internal NoteModel? Parent { get; set; }
        internal IDealer? CoreType { get; set; }

        public Company? SelectedCompany => Companies.SelectedItem;
        public override Guid Id { get; } = Guid.NewGuid();

        public override bool IsBlank
        {
            get
            {
                if (!string.IsNullOrEmpty(Name) || !string.IsNullOrEmpty(ServerCode))
                    return false;
                if (Companies.Any(c => !c.IsBlank))
                    return false;
                return true;
            }
        }

        public Dealer(ILogService? logger = null) : base(logger)
        {
            _isInitializing = true;
            CoreType = new Core.Types.Notebook.Dealer();

            InitCompanies();

            Companies ??= new ManagedObservableCollection<Company>(
                () => new Company(_logger) { Parent = this },
                null,
                _logger
            );

            _logger?.LogInfo("Dealer initialized.");
            _isInitializing = false;
        }

        public Dealer(IDealer dealer, ILogService? logger = null) : base(logger)
        {
            _isInitializing = true;
            CoreType = dealer;

            InitCompanies();

            Companies ??= new ManagedObservableCollection<Company>(
                () => new Company(_logger) { Parent = this },
                null,
                _logger
            );

            ServerCode = dealer.ServerCode;
            Name = dealer.Name;
            Notable = dealer.Notable;
            _logger?.LogInfo("Dealer loaded from core type.");
            _isInitializing = false;
            UpdateCore();
        }

        private void InitCompanies()
        {
            var companies = CoreType?.Companies.ToList()
                    .Select(coreCompany =>
                    {
                        var company = new Company(coreCompany, _logger)
                        {
                            CoreType = coreCompany,
                            Parent = this
                        };
                        company.PropertyChanged += OnCompanyPropertyChanged;
                        return company;
                    });

            Companies = new ManagedObservableCollection<Company>(
                () => new Company(_logger) { Parent = this },
                companies,
                _logger,
                (c) => c.PropertyChanged += OnCompanyPropertyChanged
            );
            Companies.PropertyChanged += OnCompaniesPropertyChanged;
            Companies.CollectionChanged += Companies_CollectionChanged;
            Companies.FirstOrDefault()?.Select();
        }

        private void OnCompaniesPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ManagedObservableCollection<Company>.SelectedItem))
            {
                OnPropertyChanged(nameof(SelectedCompany));
            }
        }

        private void OnCompanyPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsBlank));
        }

        private void Companies_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (Company c in e.NewItems)
                {
                    c.Parent = this;
                    c.PropertyChanged -= OnCompanyPropertyChanged;
                    c.PropertyChanged += OnCompanyPropertyChanged;
                }
            if (e.OldItems != null)
                foreach (Company c in e.OldItems)
                    c.PropertyChanged -= OnCompanyPropertyChanged;

            UpdateCore();
            Parent?.Parent?.NotifyCompanyNavigationChanged();
            _logger?.LogDebug("Companies collection changed.");
        }

        partial void OnNameChanged(string? value)
        {
            OnPropertyChanged(nameof(IsBlank));
            UpdateCore();
            using (LogContext.PushProperty("DealerId", Id))
            using (LogContext.PushProperty("Name", value))
            using (LogContext.PushProperty("ServerCode", ServerCode))
            using (LogContext.PushProperty("Companies", Companies.Count))
            {
                _logger?.LogInfo($"Dealer name changed: {value}");
            }
        }
        partial void OnServerCodeChanged(string? value)
        {
            OnPropertyChanged(nameof(IsBlank));
            UpdateCore();
            using (LogContext.PushProperty("DealerId", Id))
            using (LogContext.PushProperty("Name", Name))
            using (LogContext.PushProperty("ServerCode", value))
            using (LogContext.PushProperty("Companies", Companies.Count))
            {
                _logger?.LogInfo($"Dealer server code changed: {value}");
            }
        }

        partial void OnNotableChanged(bool value)
        {
            UpdateCore();
            using (LogContext.PushProperty("DealerId", Id))
            using (LogContext.PushProperty("Notable", value))
            {
                _logger?.LogInfo($"Dealer notable changed: {value}");
            }
        }

        internal void UpdateCore()
        {
            if (_isInitializing) return;

            if (CoreType == null && Parent?.CoreType != null)
                CoreType = Parent.CoreType.Dealers.FirstOrDefault(d => d.Id == Id);
            if (CoreType == null) return;
            CoreType.Name = Name ?? string.Empty;
            CoreType.ServerCode = ServerCode ?? string.Empty;
            CoreType.Notable = Notable;
            CoreType.Companies.Clear();
            CoreType.Companies.AddRange(Companies.Select(c => (Core.Types.Notebook.Company)c));
            CoreType.Companies.SelectedItem = Companies?.SelectedItem?.CoreType;
            Parent?.UpdateCore();
            _logger?.LogDebug("Dealer core updated.");
        }

        internal void RaiseChildPropertyChanged()
        {
            OnPropertyChanged(nameof(IsBlank));
        }

        public static implicit operator Core.Types.Notebook.Dealer(Dealer dealer)
        {
            if (dealer is null) return new Core.Types.Notebook.Dealer();
            return new Core.Types.Notebook.Dealer(dealer.Id)
            {
                Name = dealer.Name ?? string.Empty,
                ServerCode = dealer.ServerCode ?? string.Empty,
                Notable = dealer.Notable,
                Companies = [..dealer.Companies.Select(c => (Core.Types.Notebook.Company)c)]
            };
        }
    }
}
