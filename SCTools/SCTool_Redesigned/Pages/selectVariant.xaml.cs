using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SCTool_Redesigned.Localization;
using SCTool_Redesigned.Update;
using SCTool_Redesigned.Utils;
using SCTool_Redesigned.Windows;

namespace SCTool_Redesigned.Pages
{
    public partial class selectVariant : Page
    {
        private CancellationTokenSource? _loadCancellation;
        private int _requestNumber;
        private bool _ready;
        private bool _changing;
        private bool _singleSelection;
        private readonly List<FeatureRow> _rows = [];

        public selectVariant() => InitializeComponent();

        private static string Text(string name) => Properties.Resources.ResourceManager.GetString(name, Properties.Resources.Culture) ?? name;

        private async void Page_Loaded(object sender, RoutedEventArgs e) => await LoadCatalogAsync();
        private async void RetryBtn_Click(object sender, RoutedEventArgs e) => await LoadCatalogAsync();

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _requestNumber++;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            _ready = false;
        }

        private async Task LoadCatalogAsync()
        {
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = new CancellationTokenSource();
            var token = _loadCancellation.Token;
            int request = ++_requestNumber;
            var info = RepositoryManager.TargetInfo;
            var repository = RepositoryManager.TargetRepository;
            _ready = false;
            _rows.Clear();
            FeatureItems.ItemsSource = null;
            NoticeText.Text = "";
            StatusText.Text = Text("UI_Desc_FeatureLoading");
            RetryBtn.Visibility = Visibility.Collapsed;
            MainWindow.UI.SetFeatureSelectionAvailable(false);
            try
            {
                if (info is not CustomUpdateInfo customInfo || repository == null)
                    throw new InvalidOperationException("Release metadata is missing");
                var release = await repository.LoadFeaturesAsync(customInfo, token);
                token.ThrowIfCancellationRequested();
                if (request != _requestNumber || !IsLoaded || !ReferenceEquals(info, RepositoryManager.TargetInfo) ||
                    !ReferenceEquals(repository, RepositoryManager.TargetRepository) || MainWindow.UI.Phase != 7) return;
                var warnings = RepositoryManager.SetFeatureRelease(release);
                NoticeText.Text = string.Join("\n", release.Notices.Concat(warnings));
                _singleSelection = release.Format == LocalizationReleaseFormat.LegacyPacks;
                if (release.Catalog != null)
                {
                    foreach (var option in release.Catalog.Options)
                        _rows.Add(new FeatureRow(option, RepositoryManager.TargetFeatures.Contains(option.Id, StringComparer.Ordinal)));
                }
                FeatureItems.ItemsSource = _rows;
                StatusText.Text = release.Format == LocalizationReleaseFormat.Features ? Text("UI_Desc_FeatureBaseOnly") : "";
                _ready = true;
                MainWindow.UI.SetFeatureSelectionAvailable(RepositoryManager.FeatureSelectionReady);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                if (request != _requestNumber || !IsLoaded || token.IsCancellationRequested) return;
                App.Logger.Error(exception, "Unable to load the selected release's feature catalog");
                StatusText.Text = Text("UI_Desc_FeatureLoadFailed");
                RetryBtn.Visibility = Visibility.Visible;
                MainWindow.UI.SetFeatureSelectionAvailable(false);
            }
        }

        private void Feature_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!_ready || _changing || sender is not CheckBox { Tag: FeatureRow row }) return;
            _changing = true;
            try
            {
                row.IsSelected = ((CheckBox)sender).IsChecked == true;
                if (_singleSelection)
                {
                    if (row.IsSelected)
                        foreach (var other in _rows.Where(other => other != row)) other.IsSelected = false;
                    else if (!_rows.Any(other => other.IsSelected)) row.IsSelected = true;
                }
                RepositoryManager.SelectFeatures(_rows.Where(option => option.IsSelected).Select(option => option.Id));
                MainWindow.UI.SetFeatureSelectionAvailable(RepositoryManager.FeatureSelectionReady);
            }
            finally { _changing = false; }
        }
    }

    internal sealed class FeatureRow : INotifyPropertyChanged
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        private bool _selected;
        public bool IsSelected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        public FeatureRow(VariantOption option, bool selected)
        {
            Id = option.Id;
            Name = option.Name;
            Description = option.Description;
            _selected = selected;
        }
    }
}
