using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xaml;
using Markdig;
using Markdig.Wpf;
using SCTool_Redesigned.Utils;

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// mainNotes.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class mainNotes : Page
    {
        internal static mainNotes UI;

        public mainNotes(int idx)
        {
            UI = this;

            InitializeComponent();
            set_note(idx);
            GoogleAnalytics.Hit(App.Settings.UUID, "/main", "Program Main");
        }

        public void set_note(int idx)
        {

            switch (idx)
            {
                case 0: //patchnote
                    ShowReleasesNote();
                    break;

                case 1: //qna

                    ShowMarkdownDocument("QNA.md");
                    break;
                case 2: //credit

                    ShowMarkdownDocument("CREDIT.md");
                    break;
                default:
                    throw new ArgumentException("invalid note index " + idx.ToString());
            }
        }

        private void OpenHyperlink(object sender, ExecutedRoutedEventArgs e)
        {
            var link = e.Parameter.ToString();
            if (!link.StartsWith("http://") && !link.StartsWith("https://"))
            {
                link = "https://" + link;
            }

            try
            {
                Process.Start(link);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Properties.Resources.MSG_Decs_CannotOpenLink + "\n" + ex.Message, Properties.Resources.MSG_Title_CannotOpenLink);
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
}
