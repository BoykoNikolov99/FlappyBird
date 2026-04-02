using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public class MainMenuForm : Form
    {
        private Button btnNewGame;
        private Button btnOptions;
        private Button btnAbout;
        private Button btnExit;
        private Label titleLabel;
        private Label highScoreLabel;
        private int highScore = 0;

        // settings that can be changed in Options
        public int PipeGap { get; set; } = 120;
        public int BasePipeSpeed { get; set; } = 6;
        public int DungeonIntervalSeconds { get; set; } = 6;

        public MainMenuForm()
        {
            highScore = Properties.Settings.Default.HighScore;
            InitializeMainMenu();
        }

        private void InitializeMainMenu()
        {
            this.Text = "Flappy Bird";
            this.ClientSize = new Size(800, 450);
            IconHelper.SetFormIcon(this);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;

            // try loading background image
            string bgPath = Path.Combine(Application.StartupPath, "background.png");
            if (File.Exists(bgPath))
            {
                try
                {
                    this.BackgroundImage = Image.FromFile(bgPath);
                    this.BackgroundImageLayout = ImageLayout.Stretch;
                }
                catch { }
            }
            else
            {
                this.BackColor = Color.SkyBlue;
            }

            // semi-transparent overlay panel for menu readability
            var overlay = new Panel();
            overlay.Size = new Size(300, 360);
            overlay.Location = new Point((this.ClientSize.Width - overlay.Width) / 2, (this.ClientSize.Height - overlay.Height) / 2);
            overlay.BackColor = Color.FromArgb(160, 0, 0, 0);
            this.Controls.Add(overlay);

            // title
            titleLabel = new Label();
            titleLabel.Text = "Flappy Bird";
            titleLabel.Font = new Font("Microsoft Sans Serif", 24F, FontStyle.Bold);
            titleLabel.ForeColor = Color.Yellow;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.AutoSize = true;
            overlay.Controls.Add(titleLabel);
            // center title after adding to get auto-sized width
            titleLabel.Location = new Point((overlay.Width - titleLabel.PreferredWidth) / 2, 20);

            // high score display
            highScoreLabel = new Label();
            highScoreLabel.Text = "High Score: " + highScore;
            highScoreLabel.Font = new Font("Microsoft Sans Serif", 13F, FontStyle.Bold);
            highScoreLabel.ForeColor = Color.White;
            highScoreLabel.BackColor = Color.Transparent;
            highScoreLabel.AutoSize = true;
            overlay.Controls.Add(highScoreLabel);
            highScoreLabel.Location = new Point((overlay.Width - highScoreLabel.PreferredWidth) / 2, 60);

            int buttonWidth = 200;
            int buttonHeight = 45;
            int startY = 100;
            int spacing = 58;
            int buttonX = (overlay.Width - buttonWidth) / 2;

            // New Game button
            btnNewGame = CreateMenuButton("New Game", buttonX, startY, buttonWidth, buttonHeight);
            btnNewGame.Click += BtnNewGame_Click;
            overlay.Controls.Add(btnNewGame);

            // Options button
            btnOptions = CreateMenuButton("Options", buttonX, startY + spacing, buttonWidth, buttonHeight);
            btnOptions.Click += BtnOptions_Click;
            overlay.Controls.Add(btnOptions);

            // About Us button
            btnAbout = CreateMenuButton("About Us", buttonX, startY + spacing * 2, buttonWidth, buttonHeight);
            btnAbout.Click += BtnAbout_Click;
            overlay.Controls.Add(btnAbout);

            // Exit button
            btnExit = CreateMenuButton("Exit", buttonX, startY + spacing * 3, buttonWidth, buttonHeight);
            btnExit.Click += BtnExit_Click;
            overlay.Controls.Add(btnExit);
        }

        private Button CreateMenuButton(string text, int x, int y, int width, int height)
        {
            var btn = new Button();
            btn.Text = text;
            btn.Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Bold);
            btn.Size = new Size(width, height);
            btn.Location = new Point(x, y);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = Color.White;
            btn.FlatAppearance.BorderSize = 2;
            btn.BackColor = Color.FromArgb(200, 34, 139, 34); // forest green
            btn.ForeColor = Color.White;
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private void BtnNewGame_Click(object sender, EventArgs e)
        {
            this.Hide();
            var gameForm = new Form1(PipeGap, BasePipeSpeed, DungeonIntervalSeconds, highScore);
            gameForm.FormClosed += (s, args) =>
            {
                if (gameForm.HighScore > highScore)
                {
                    highScore = gameForm.HighScore;
                    Properties.Settings.Default.HighScore = highScore;
                    Properties.Settings.Default.Save();
                    highScoreLabel.Text = "High Score: " + highScore;
                    highScoreLabel.Location = new Point(
                        (highScoreLabel.Parent.Width - highScoreLabel.PreferredWidth) / 2,
                        highScoreLabel.Location.Y);
                }
                this.Show();
            };
            gameForm.Show();
        }

        private void BtnOptions_Click(object sender, EventArgs e)
        {
            using (var optionsForm = new OptionsForm(PipeGap, BasePipeSpeed, DungeonIntervalSeconds))
            {
                if (optionsForm.ShowDialog(this) == DialogResult.OK)
                {
                    PipeGap = optionsForm.PipeGap;
                    BasePipeSpeed = optionsForm.BasePipeSpeed;
                    DungeonIntervalSeconds = optionsForm.DungeonIntervalSeconds;
                }
            }
        }

        private void BtnAbout_Click(object sender, EventArgs e)
        {
            using (var aboutForm = new AboutForm())
            {
                aboutForm.ShowDialog(this);
            }
        }

        private void BtnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
