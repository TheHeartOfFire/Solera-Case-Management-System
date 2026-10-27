using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.Interfaces;
using AMFormsCST.Desktop.Types;
using CommunityToolkit.Mvvm.ComponentModel;
using static AMFormsCST.Desktop.Interfaces.IManagedObservableCollectionItem;

namespace AMFormsCST.Desktop.BaseClasses
{
    public abstract class ManagedObservableCollectionItem(ILogService? logger = null) : ObservableObject, IManagedObservableCollectionItem
    {
        public abstract bool IsBlank { get; }
        public abstract Guid Id { get; }
        public CollectionMemberState State { get; set; } = CollectionMemberState.NotACollectionMember;

        public event EventHandler<GuidEventArgs>? Selected;
        private IManagedObservableCollection? _containingCollection;
        protected readonly ILogService? _logger = logger;
        private EventHandler<GuidEventArgs>? _selectionChangedHandler;

        public void OnAddedToCollection(IManagedObservableCollection containingCollection)
        {
            if (State != CollectionMemberState.NotACollectionMember)
            {
                _logger?.LogError($"Item {Id} is already a member of a collection.", null);
                throw new InvalidOperationException("Item is already a member of a collection.");
            }
            State = CollectionMemberState.NotSelected;
            _containingCollection = containingCollection ?? throw new ArgumentNullException(nameof(containingCollection));
            _selectionChangedHandler = (s, e) => Select(e.Value);
            _containingCollection.SelectionChanged += _selectionChangedHandler;
            _logger?.LogInfo($"Item {Id} added to collection.");
        }

        public void OnRemovedFromCollection()
        {
            if (State == CollectionMemberState.NotACollectionMember)
            {
                _logger?.LogError($"Item {Id} is not a member of a collection.", null);
                throw new InvalidOperationException("Item is not a member of a collection.");
            }
            State = CollectionMemberState.NotACollectionMember;

            if (_containingCollection is null)
            {
                _logger?.LogFatal($"Internal error: _containingCollection is null for item {Id} while State indicates membership.");
                throw new InvalidOperationException("Internal error: _containingCollection is null while State indicates membership.");
            }

            _containingCollection.SelectionChanged -= _selectionChangedHandler;
            _containingCollection = null;
            _logger?.LogInfo($"Item {Id} removed from collection.");
        }

        public void Select()
        {
            if (State == CollectionMemberState.NotACollectionMember)
            {
                _logger?.LogError($"Item {Id} is not a member of a collection.", null);
                throw new InvalidOperationException("Item is not a member of a collection.");
            }

            Selected?.Invoke(this, new GuidEventArgs(Id));
        }

        public void Select(Guid? guid = null)
        {
            if (State == CollectionMemberState.NotACollectionMember)
            {
                _logger?.LogError($"Item {Id} is not a member of a collection.", null);
                throw new InvalidOperationException("Item is not a member of a collection.");
            }

            if (guid != Id)
            {
                if (State != CollectionMemberState.NotSelected)
                {
                    State = CollectionMemberState.NotSelected;
                    OnPropertyChanged(nameof(State));
                }
                _logger?.LogDebug($"Item {Id} deselected (guid: {guid}).");
                return;
            }

            if (guid == Id)
            {
                State = CollectionMemberState.Selected;
                OnPropertyChanged(nameof(State));
                _logger?.LogInfo($"Item {Id} set to selected (guid: {guid}).");
            }
        }

        private protected void SelectionComplete() { }
    }
}
