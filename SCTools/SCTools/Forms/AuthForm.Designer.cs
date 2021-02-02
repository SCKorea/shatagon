
namespace NSW.StarCitizen.Tools.Forms
{
    partial class AuthForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.AuthInfo = new MetroFramework.Controls.MetroLabel();
            this.CodeInputBox = new MetroFramework.Controls.MetroTextBox();
            this.Applybtn = new MetroFramework.Controls.MetroButton();
            this.exitbtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // AuthInfo
            // 
            this.AuthInfo.AutoSize = true;
            this.AuthInfo.BackColor = System.Drawing.Color.Transparent;
            this.AuthInfo.FontSize = MetroFramework.MetroLabelSize.Tall;
            this.AuthInfo.FontWeight = MetroFramework.MetroLabelWeight.Bold;
            this.AuthInfo.ForeColor = System.Drawing.Color.White;
            this.AuthInfo.Location = new System.Drawing.Point(39, 30);
            this.AuthInfo.Name = "AuthInfo";
            this.AuthInfo.Size = new System.Drawing.Size(120, 50);
            this.AuthInfo.TabIndex = 0;
            this.AuthInfo.Text = "인증코드를\r\n입력해주세요";
            this.AuthInfo.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.AuthInfo.Theme = MetroFramework.MetroThemeStyle.Dark;
            // 
            // CodeInputBox
            // 
            // 
            // 
            // 
            this.CodeInputBox.CustomButton.BackColor = System.Drawing.Color.LightGray;
            this.CodeInputBox.CustomButton.FlatAppearance.BorderColor = System.Drawing.Color.Gray;
            this.CodeInputBox.CustomButton.FlatAppearance.BorderSize = 5;
            this.CodeInputBox.CustomButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.CodeInputBox.CustomButton.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.CodeInputBox.CustomButton.Image = null;
            this.CodeInputBox.CustomButton.Location = new System.Drawing.Point(92, 2);
            this.CodeInputBox.CustomButton.Name = "";
            this.CodeInputBox.CustomButton.Size = new System.Drawing.Size(25, 25);
            this.CodeInputBox.CustomButton.Style = MetroFramework.MetroColorStyle.White;
            this.CodeInputBox.CustomButton.TabIndex = 1;
            this.CodeInputBox.CustomButton.Theme = MetroFramework.MetroThemeStyle.Light;
            this.CodeInputBox.CustomButton.UseCustomBackColor = true;
            this.CodeInputBox.CustomButton.UseSelectable = true;
            this.CodeInputBox.CustomButton.UseVisualStyleBackColor = false;
            this.CodeInputBox.CustomButton.Visible = false;
            this.CodeInputBox.FontSize = MetroFramework.MetroTextBoxSize.Medium;
            this.CodeInputBox.Lines = new string[0];
            this.CodeInputBox.Location = new System.Drawing.Point(40, 150);
            this.CodeInputBox.MaxLength = 8;
            this.CodeInputBox.Name = "CodeInputBox";
            this.CodeInputBox.PasswordChar = '●';
            this.CodeInputBox.PromptText = "_ _ _ _ _ _ _";
            this.CodeInputBox.ScrollBars = System.Windows.Forms.ScrollBars.None;
            this.CodeInputBox.SelectedText = "";
            this.CodeInputBox.SelectionLength = 0;
            this.CodeInputBox.SelectionStart = 0;
            this.CodeInputBox.ShortcutsEnabled = true;
            this.CodeInputBox.Size = new System.Drawing.Size(120, 30);
            this.CodeInputBox.Style = MetroFramework.MetroColorStyle.Purple;
            this.CodeInputBox.TabIndex = 2;
            this.CodeInputBox.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.CodeInputBox.Theme = MetroFramework.MetroThemeStyle.Dark;
            this.CodeInputBox.UseCustomBackColor = true;
            this.CodeInputBox.UseCustomForeColor = true;
            this.CodeInputBox.UseSelectable = true;
            this.CodeInputBox.UseStyleColors = true;
            this.CodeInputBox.UseSystemPasswordChar = true;
            this.CodeInputBox.WaterMark = "_ _ _ _ _ _ _";
            this.CodeInputBox.WaterMarkColor = System.Drawing.Color.FromArgb(((int)(((byte)(109)))), ((int)(((byte)(109)))), ((int)(((byte)(109)))));
            this.CodeInputBox.WaterMarkFont = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            // 
            // Applybtn
            // 
            this.Applybtn.FontSize = MetroFramework.MetroButtonSize.Medium;
            this.Applybtn.Highlight = true;
            this.Applybtn.Location = new System.Drawing.Point(58, 224);
            this.Applybtn.Name = "Applybtn";
            this.Applybtn.Size = new System.Drawing.Size(83, 32);
            this.Applybtn.Style = MetroFramework.MetroColorStyle.Purple;
            this.Applybtn.TabIndex = 3;
            this.Applybtn.Text = "코드 입력";
            this.Applybtn.Theme = MetroFramework.MetroThemeStyle.Dark;
            this.Applybtn.UseSelectable = true;
            this.Applybtn.Click += new System.EventHandler(this.Applybtn_Click);
            // 
            // exitbtn
            // 
            this.exitbtn.BackColor = System.Drawing.Color.Transparent;
            this.exitbtn.FlatAppearance.BorderColor = System.Drawing.Color.Black;
            this.exitbtn.FlatAppearance.BorderSize = 0;
            this.exitbtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.exitbtn.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.exitbtn.Location = new System.Drawing.Point(175, 5);
            this.exitbtn.Margin = new System.Windows.Forms.Padding(0);
            this.exitbtn.Name = "exitbtn";
            this.exitbtn.Size = new System.Drawing.Size(25, 25);
            this.exitbtn.TabIndex = 4;
            this.exitbtn.TabStop = false;
            this.exitbtn.Text = "╳";
            this.exitbtn.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.exitbtn.UseVisualStyleBackColor = false;
            this.exitbtn.Click += new System.EventHandler(this.exitbtn_Click);
            // 
            // AuthForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(200, 300);
            this.ControlBox = false;
            this.Controls.Add(this.exitbtn);
            this.Controls.Add(this.Applybtn);
            this.Controls.Add(this.CodeInputBox);
            this.Controls.Add(this.AuthInfo);
            this.DisplayHeader = false;
            this.ForeColor = System.Drawing.Color.White;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AuthForm";
            this.Padding = new System.Windows.Forms.Padding(20, 30, 20, 20);
            this.Resizable = false;
            this.ShadowType = MetroFramework.Forms.MetroFormShadowType.DropShadow;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Style = MetroFramework.MetroColorStyle.Purple;
            this.Text = "인증";
            this.TextAlign = MetroFramework.Forms.MetroFormTextAlign.Center;
            this.Theme = MetroFramework.MetroThemeStyle.Dark;
            this.TopMost = true;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private MetroFramework.Controls.MetroLabel AuthInfo;
        private MetroFramework.Controls.MetroTextBox CodeInputBox;
        private MetroFramework.Controls.MetroButton Applybtn;
        private System.Windows.Forms.Button exitbtn;
    }
}