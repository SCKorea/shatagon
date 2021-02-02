using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Forms;
using MetroFramework;
using MetroFramework.Forms;


namespace NSW.StarCitizen.Tools.Forms
{
    public partial class AuthForm : MetroForm
    {
        private readonly NSW.StarCitizen.Tools.Controllers.AuthController _controller;
        public AuthForm()
        {
            this.Font = new Font("맑은 고딕",12);
            InitializeComponent();
            AuthInfo.Location = new Point(this.Size.Width / 2 - AuthInfo.Size.Width / 2, this.Size.Width / 4);
        }



        private async void Applybtn_Click(object sender, EventArgs e)
        {
            var result = await _controller.try_auth(CodeInputBox.Text);
            if (result)
            {
                MetroMessageBox.Show(this, "", "인증완료", MessageBoxButtons.OK, MessageBoxIcon.Question, 100);
                this.Close();
            }
            else
            {
                MetroMessageBox.Show(this, "인증코드를\n확인해주세요", "", MessageBoxButtons.OK, MessageBoxIcon.Error, 150);
            }
        }
    }
}
