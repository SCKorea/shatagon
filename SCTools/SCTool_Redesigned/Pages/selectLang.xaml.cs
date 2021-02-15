using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SCTool_Redesigned.Settings;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// selectLang.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class selectLang : Page
    {
        Dictionary<string, string> UiLangList { get; set; }

        public selectLang()
        {
            UiLangList = GetSupportedUiLanguages();

            InitializeComponent();

            LangListBox.ItemsSource = UiLangList;
            LangListBox.SelectedValue = Properties.Resources.Culture.Name;
        }

        private static Dictionary<string, string> GetSupportedUiLanguages()
        {
            var languages = new Dictionary<string, string> {
                { "en-US", "English" },
                { "ko-KR", "한국어" }
            };

            var neutralCultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
                .Where(c => Directory.Exists(c.TwoLetterISOLanguageName));

            foreach (var neutralCulture in neutralCultures)
            {
                var culture = CultureInfo.CreateSpecificCulture(neutralCulture.Name);
                if (!languages.ContainsKey(culture.Name))
                {
                    languages.Add(culture.Name, neutralCulture.NativeName);
                }
            }

            return languages;
        }

        private void applyBtn_Click(object sender, RoutedEventArgs e)
        {
            ((Windows.MainWindow) Application.Current.MainWindow).Phase++;
        }

        private void LangListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LangListBox.SelectedValue is string language)
            {
                Properties.Resources.Culture = new CultureInfo(language);

                Thread.CurrentThread.CurrentCulture = Properties.Resources.Culture;
                Thread.CurrentThread.CurrentUICulture = Properties.Resources.Culture;

                applyBtn.Content = Properties.Resources.ResourceManager.GetString("UI_Button_Next", Properties.Resources.Culture);

                App.Settings.Language = language;
                App.SaveAppSettings();
            }
        }
    }
}
