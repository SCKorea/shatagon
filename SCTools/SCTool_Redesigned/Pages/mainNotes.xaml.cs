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
using System.Text.RegularExpressions;

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
            set_link(App.Settings.GameLanguage);

            GoogleAnalytics.Hit(App.Settings.UUID, "/main", "Program Main");
        }

        private void set_link(string language)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
            {
                Community_link1.Source = Base64ToImage(Properties.Resources.UI_Button_Image_Community_1).Source;
                Community_link1.ToolTip = Properties.Resources.UI_Button_Tooltip_Community_1;
                Community_link2.Source = Base64ToImage(Properties.Resources.UI_Button_Image_Community_2).Source;
                Community_link2.ToolTip = Properties.Resources.UI_Button_Tooltip_Community_2;
            }));
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
            var link = e.Parameter.ToString();
            if(!link.StartsWith("http://") && !link.StartsWith("https://"))
            {
                link = "https://" + link;
            }
            Console.WriteLine(link);

            try
            {
                Process.Start(link);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.MSG_Decs_CannotOpenLink+"\n"+ex.Message, Properties.Resources.MSG_Title_CannotOpenLink);
            }
        }

        private void ShowReleasesNote()
        {
            ShowFlowDocument("releases", RepositoryManager.GetReleaseNote(true));
        }

        private Dictionary<string, FlowDocument> _cache = new Dictionary<string, FlowDocument>();

        private void ShowMarkdownDocument(string filename)
        {
            if (_cache.ContainsKey(filename))
            {
                UI.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate
                {
                    NoteBlock.Document = _cache[filename];
                }));

                return;
            }

            ShowFlowDocument(filename, Properties.Resources.UI_Desc_BringingMarkdown);

            Task.Run(() => ShowFlowDocument(filename, RepositoryManager.GetMarkdownDocument(filename)));
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

        private void Open_Community_1(object sender, MouseButtonEventArgs e) => Process.Start(Properties.Resources.UI_Button_Link_Community_1);

        private void Open_Community_2(object sender, MouseButtonEventArgs e) => Process.Start(Properties.Resources.UI_Button_Link_Community_2);

        private Image Base64ToImage(string image)
        {
            Image img = new Image();
            byte[] binaryData = Convert.FromBase64String(image);
            BitmapImage bi = new BitmapImage();

            bi.BeginInit();
            using (bi.StreamSource = new MemoryStream(binaryData))
            {
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                
                img.Source = bi;
            }

            return img;
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
