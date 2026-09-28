using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SCTool_Redesigned.Localization;
using SCTool_Redesigned.Update;
using SCTool_Redesigned.Utils;

namespace SCTool_Redesigned.Pages
{
    public partial class selectVariant : Page
    {
        public selectVariant()
        {
            InitializeComponent();
            if (RepositoryManager.TargetInfo is CustomUpdateInfo info && VariantCatalog.IsLegacy(info))
            {
                RepositoryManager.SelectVariant("legacy");
                LegacyNotice.Visibility = Visibility.Visible;
                VariantSelectListBox.Visibility = Visibility.Collapsed;
                return;
            }

            VariantSelectListBox.ItemsSource = VariantCatalog.Options;
            VariantSelectListBox.SelectedItem = VariantCatalog.Options.First(option => option.Id == RepositoryManager.TargetVariant);
        }

        private void VariantSelectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VariantSelectListBox.SelectedItem is VariantOption option)
                RepositoryManager.SelectVariant(option.Id);
        }
    }
}
