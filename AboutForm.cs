using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeAbout();
        }

        private void InitializeAbout()
        {
            this.Text = "About Us";
            this.ClientSize = new Size(400, 320);
            IconHelper.SetFormIcon(this);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);

            var titleLabel = new Label();
            titleLabel.Text = "Flappy Bird";
            titleLabel.Font = new Font("Microsoft Sans Serif", 22F, FontStyle.Bold);
            titleLabel.ForeColor = Color.Yellow;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.AutoSize = true;
            this.Controls.Add(titleLabel);
            titleLabel.Location = new Point((this.ClientSize.Width - titleLabel.PreferredWidth) / 2, 20);

            var versionLabel = new Label();
            versionLabel.Text = "Version v1.0.3";
            versionLabel.Font = new Font("Microsoft Sans Serif", 11F);
            versionLabel.ForeColor = Color.LightGray;
            versionLabel.BackColor = Color.Transparent;
            versionLabel.AutoSize = true;
            this.Controls.Add(versionLabel);
            versionLabel.Location = new Point((this.ClientSize.Width - versionLabel.PreferredWidth) / 2, 65);

            var creatorLabel = new Label();
            creatorLabel.Text = "Created by Boyko Nikolov";
            creatorLabel.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            creatorLabel.ForeColor = Color.LightGreen;
            creatorLabel.BackColor = Color.Transparent;
            creatorLabel.AutoSize = true;
            this.Controls.Add(creatorLabel);
            creatorLabel.Location = new Point((this.ClientSize.Width - creatorLabel.PreferredWidth) / 2, 95);

            var descLabel = new Label();
            descLabel.Text = "A Flappy Bird clone built with\nWindows Forms and .NET Framework.\n\nNavigate through pipes, survive dungeons,\nand beat your high score!";
            descLabel.Font = new Font("Microsoft Sans Serif", 10F);
            descLabel.ForeColor = Color.White;
            descLabel.BackColor = Color.Transparent;
            descLabel.TextAlign = ContentAlignment.MiddleCenter;
            descLabel.AutoSize = false;
            descLabel.Size = new Size(360, 100);
            descLabel.Location = new Point((this.ClientSize.Width - descLabel.Width) / 2, 130);
            this.Controls.Add(descLabel);

            var btnClose = new Button();
            btnClose.Text = "Close";
            btnClose.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            btnClose.Size = new Size(120, 38);
            btnClose.Location = new Point((this.ClientSize.Width - btnClose.Width) / 2, 255);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.BackColor = Color.FromArgb(200, 34, 139, 34);
            btnClose.ForeColor = Color.White;
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }
    }
}
