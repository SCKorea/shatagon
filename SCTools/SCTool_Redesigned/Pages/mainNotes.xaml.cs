using System;
using System.Collections.Generic;
using System.Linq;
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

namespace SCTool_Redesigned.Pages
{
    /// <summary>
    /// mainNotes.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class mainNotes : Page
    {
        private string _sampletxt1, _sampletxt2,_sampletxt3;
        public mainNotes()
        {
            _sampletxt1 = @"유구한 역사와 전통에 빛나는 우리 대한국민은 3·1운동으로 건립된 대한민국임시정부의 법통과 불의에 항거한 4·19민주이념을 계승하고, 조국의 민주개혁과 평화적 통일의 사명에 입각하여 정의·인도와 동포애로써 민족의 단결을 공고히 하고, 모든 사회적 폐습과 불의를 타파하며, 자율과 조화를 바탕으로 자유민주적 기본질서를 더욱 확고히 하여 정치·경제·사회·문화의 모든 영역에 있어서 각인의 기회를 균등히 하고, 능력을 최고도로 발휘하게 하며, 자유와 권리에 따르는 책임과 의무를 완수하게 하여, 안으로는 국민생활의 균등한 향상을 기하고 밖으로는 항구적인 세계평화와 인류공영에 이바지함으로써 우리들과 우리들의 자손의 안전과 자유와 행복을 영원히 확보할 것을 다짐하면서 1948년 7월 12일에 제정되고 8차에 걸쳐 개정된 헌법을 이제 국회의 의결을 거쳐 국민투표에 의하여 개정한다.

        제1장 총강

    조문체계도버튼

 제1조 ①대한민국은 민주공화국이다.

②대한민국의 주권은 국민에게 있고, 모든 권력은 국민으로부터 나온다.

    조문체계도버튼

 제2조 ①대한민국의 국민이 되는 요건은 법률로 정한다.

②국가는 법률이 정하는 바에 의하여 재외국민을 보호할 의무를 진다.

    조문체계도버튼

 제3조 대한민국의 영토는 한반도와 그 부속도서로 한다.

    조문체계도버튼

 제4조 대한민국은 통일을 지향하며, 자유민주적 기본질서에 입각한 평화적 통일 정책을 수립하고 이를 추진한다.

    조문체계도버튼

 제5조 ①대한민국은 국제평화의 유지에 노력하고 침략적 전쟁을 부인한다.

②국군은 국가의 안전보장과 국토방위의 신성한 의무를 수행함을 사명으로 하며, 그 정치적 중립성은 준수된다.

    조문체계도버튼

 제6조 ①헌법에 의하여 체결ㆍ공포된 조약과 일반적으로 승인된 국제법규는 국내법과 같은 효력을 가진다.

②외국인은 국제법과 조약이 정하는 바에 의하여 그 지위가 보장된다.

    조문체계도버튼

 제7조 ①공무원은 국민전체에 대한 봉사자이며, 국민에 대하여 책임을 진다.

②공무원의 신분과 정치적 중립성은 법률이 정하는 바에 의하여 보장된다.";
            _sampletxt2 = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
            _sampletxt3 = @"저에게 시간과 예산을 주신다면....














드...드리겠습니다!























필요없어!











아니에요... 많이 주세요 ㅠㅠㅠ";
            InitializeComponent();
            set_note(0);
        }
        private void set_note(int idx)
        {
            switch (idx)
            {
                case 0: //patchnote
                    Menu_patchnote.Foreground = (SolidColorBrush) App.Current.Resources["KeyPointBrush"];
                    Menu_credit.Foreground = (SolidColorBrush) App.Current.Resources["TextBrush"];
                    Menu_qna.Foreground = (SolidColorBrush) App.Current.Resources["TextBrush"];
                    NoteBlock.Text = _sampletxt1;
                    break;
                case 1: //qna
                    Menu_patchnote.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                    Menu_credit.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                    Menu_qna.Foreground = (SolidColorBrush)App.Current.Resources["KeyPointBrush"];
                    NoteBlock.Text = _sampletxt2;
                    break;
                case 2: //credit
                    Menu_patchnote.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                    Menu_credit.Foreground = (SolidColorBrush)App.Current.Resources["KeyPointBrush"];
                    Menu_qna.Foreground = (SolidColorBrush)App.Current.Resources["TextBrush"];
                    NoteBlock.Text = _sampletxt3;
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
    }
}
