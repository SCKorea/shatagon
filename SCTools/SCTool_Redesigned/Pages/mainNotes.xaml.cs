using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
using System.Windows.Threading;
using System.Xaml;
using Markdig;
using Markdig.Wpf;
using NSW.StarCitizen.Tools.Lib.Update;
using SCTool_Redesigned.Utils;
using SCTool_Redesigned.Settings;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// mainNotes.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class mainNotes : Page
    {
        internal static mainNotes UI;

        public mainNotes()
        {
            UI = this;

            InitializeComponent();
            set_note(0);
        }

        private void set_note(int idx)
        {

            switch (idx)
            {
                case 0: //patchnote
                    Menu_patchnote.Foreground = (SolidColorBrush)App.Current.Resources["KeyPointBrush"];
                    Menu_credit.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                    Menu_qna.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                    ShowReleasesNote();
                    break;

                case 1: //qna
                    Menu_patchnote.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                    Menu_credit.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                    Menu_qna.Foreground = (SolidColorBrush)App.Current.Resources["KeyPointBrush"];
                    ShowMarkdownDocument("QNA.md");
                    break;
                case 2: //credit
                    Menu_patchnote.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                    Menu_credit.Foreground = (SolidColorBrush)App.Current.Resources["KeyPointBrush"];
                    Menu_qna.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                    ShowMarkdownDocument("CREDIT.md");
                    break;
                default:
                    throw new ArgumentException("invalid note index " + idx.ToString());
            }
        }
        private void Menu_patchnote_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            set_note(0);
        }
        private void Menu_qna_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            set_note(1);
        }
        private void Menu_credit_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            set_note(2);
        }

        private void OpenHyperlink(object sender, ExecutedRoutedEventArgs e)
        {
            Process.Start(e.Parameter.ToString());
        }

        private void ShowReleasesNote()
        {
            ShowFlowDocument("releases", RepositoryManager.GetLatestReleaseNote());
        }

        private Dictionary<string, FlowDocument> _cache = new Dictionary<string, FlowDocument>();

        private void ShowMarkdownDocument(string filename)
        {
            string markdown;

            if (_cache.ContainsKey(filename))
            {
                UI.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
                {
                    NoteBlock.Document = _cache[filename];
                }));

                return;
            }

            ShowFlowDocument(filename, Properties.Resources.UI_Desc_BringingMarkdown);

            Task.Run(() =>
            {
                LocalizationSource localization = RepositoryManager.GetLocalizationSource();
                string gitUri = "";

                if (localization.Type.Equals(UpdateRepositoryType.GitHub))
                {
                    gitUri = "https://raw.githubusercontent.com/";
                }

                string markdownUri = $"{gitUri}{localization.Repository}/master/{filename}";

                if (localization.Repository.Contains("sc_ko"))
                {
                    // The sc_ko repository is private and uses its own api server.
                    markdownUri = "https://sc.galaxyhub.kr/api/v1/translate/document/?name=" + filename;
                }

                using (var web = new WebClient())
                {
                    try
                    {
                        web.Encoding = Encoding.UTF8;
                        markdown = web.DownloadString(markdownUri);
                    }
                    catch (WebException webe)
                    {
                        if (((HttpWebResponse)webe.Response).StatusCode == HttpStatusCode.NotFound)
                        {
                            markdown = Properties.Resources.UI_Desc_NotFoundMarkdown;
                        }
                        else
                        {
                            markdown = Properties.Resources.UI_Desc_UnableMarkdown;
                        }
                    }
                }

                ShowFlowDocument(filename, markdown);
                
            });
        }

        private void ShowFlowDocument(string name, string markdown)
        {
            UI.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                var xaml = Markdig.Wpf.Markdown.ToXaml(markdown, new MarkdownPipelineBuilder().UseSupportedExtensions().Build());

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml)))
                {
                    using (var reader = new XamlXmlReader(stream, new MyXamlSchemaContext()))
                    {
                        if (System.Windows.Markup.XamlReader.Load(reader) is FlowDocument document)
                        {
                            if (_cache.ContainsKey(name))
                            {
                                _cache.Remove(name);
                            }

                            _cache.Add(name, document);

                            UI.NoteBlock.Document = document;
                        }
                    }
                }
            }));
        }
    }

    class MyXamlSchemaContext : XamlSchemaContext
    {
        public override bool TryGetCompatibleXamlNamespace(string xamlNamespace, out string compatibleNamespace)
        {
            if (xamlNamespace.Equals("clr-namespace:Markdig.Wpf", StringComparison.Ordinal))
            {
                //compatibleNamespace = $"clr-namespace:Markdig.Wpf;assembly={Assembly.GetAssembly(typeof(Markdig.Wpf.Styles)).FullName}";
                compatibleNamespace = "";
                return true;
            }
            return base.TryGetCompatibleXamlNamespace(xamlNamespace, out compatibleNamespace);
        }
    }
}
