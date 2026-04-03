using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public class MainMenuForm : Form
    {
        private ModernButton btnNewGame;
        private ModernButton btnOptions;
        private ModernButton btnAbout;
        private ModernButton btnExit;
        private PictureBox logoPictureBox;
        private Label highScoreLabel;
        private int highScore = 0;
        private Panel overlay;
        private Form1 activeGame;

        // Persistent high score file in a stable location
        private static readonly string SaveDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlappyBird");
        private static readonly string SaveFile = Path.Combine(SaveDir, "highscore.txt");

        // settings that can be changed in Options
        public int PipeGap { get; set; } = 120;
        public int BasePipeSpeed { get; set; } = 6;
        public int DungeonIntervalSeconds { get; set; } = 6;
        public Size GameResolution { get; set; } = new Size(800, 450);

        public MainMenuForm()
        {
            highScore = LoadHighScore();
            InitializeMainMenu();
        }

        private void InitializeMainMenu()
        {
            this.Text = "Flappy Bird";
            this.ClientSize = GameResolution;
            IconHelper.SetFormIcon(this);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;

            // try loading background image
            string bgPath = AssetPath.Image("background.png");
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
            overlay = new Panel();
            overlay.Size = new Size(300, 380);
            overlay.Location = new Point((this.ClientSize.Width - overlay.Width) / 2, (this.ClientSize.Height - overlay.Height) / 2);
            overlay.BackColor = Color.FromArgb(160, 0, 0, 0);
            this.Controls.Add(overlay);

            // logo at the top of the menu
            logoPictureBox = new PictureBox();
            logoPictureBox.Size = new Size(260, 80);
            logoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            logoPictureBox.BackColor = Color.Transparent;
            logoPictureBox.Location = new Point((overlay.Width - logoPictureBox.Width) / 2, 12);
            string logoPath = AssetPath.Image("mainmenu-logo.png");
            if (File.Exists(logoPath))
            {
                try { logoPictureBox.Image = Image.FromFile(logoPath); } catch { }
            }
            overlay.Controls.Add(logoPictureBox);

            // high score display
            highScoreLabel = new Label();
            highScoreLabel.Text = "High Score: " + highScore;
            highScoreLabel.Font = new Font("Microsoft Sans Serif", 13F, FontStyle.Bold);
            highScoreLabel.ForeColor = Color.White;
            highScoreLabel.BackColor = Color.Transparent;
            highScoreLabel.AutoSize = true;
            overlay.Controls.Add(highScoreLabel);
            highScoreLabel.Location = new Point((overlay.Width - highScoreLabel.PreferredWidth) / 2, 100);

            int buttonWidth = 200;
            int buttonHeight = 45;
            int startY = 135;
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

            // Version watermark at bottom-right of the form
            var versionLabel = new Label();
            versionLabel.Text = "Flappy Bird version v1.0.3.1";
            versionLabel.Font = new Font("Microsoft Sans Serif", 8F);
            versionLabel.ForeColor = Color.FromArgb(180, 255, 255, 255);
            versionLabel.BackColor = Color.Transparent;
            versionLabel.AutoSize = true;
            this.Controls.Add(versionLabel);
            versionLabel.Location = new Point(
                this.ClientSize.Width - versionLabel.PreferredWidth - 8,
                this.ClientSize.Height - versionLabel.PreferredHeight - 6);
            versionLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            versionLabel.BringToFront();
        }

        private void ApplyResolution()
        {
            this.ClientSize = GameResolution;
            this.CenterToScreen();
            if (overlay != null)
            {
                overlay.Location = new Point(
                    (this.ClientSize.Width - overlay.Width) / 2,
                    (this.ClientSize.Height - overlay.Height) / 2);
            }
        }

        private ModernButton CreateMenuButton(string text, int x, int y, int width, int height)
        {
            var btn = new ModernButton();
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            btn.Size = new Size(width, height);
            btn.Location = new Point(x, y);
            return btn;
        }

        private void BtnNewGame_Click(object sender, EventArgs e)
        {
            // Hide menu overlay
            overlay.Visible = false;

            // Create Form1 and embed it as a child control in this window
            activeGame = new Form1(PipeGap, BasePipeSpeed, DungeonIntervalSeconds, highScore, GameResolution.Width, GameResolution.Height);
            activeGame.TopLevel = false;
            activeGame.FormBorderStyle = FormBorderStyle.None;
            activeGame.Dock = DockStyle.Fill;

            activeGame.FormClosed += (s, args) =>
            {
                var closedGame = (Form1)s;

                // Ignore if this handler belongs to a stale game instance
                if (closedGame != activeGame)
                {
                    this.Controls.Remove(closedGame);
                    closedGame.Dispose();
                    return;
                }

                if (closedGame.HighScore > highScore)
                {
                    highScore = closedGame.HighScore;
                    SaveHighScore(highScore);
                    highScoreLabel.Text = "High Score: " + highScore;
                    highScoreLabel.Location = new Point(
                        (highScoreLabel.Parent.Width - highScoreLabel.PreferredWidth) / 2,
                        highScoreLabel.Location.Y);
                }

                // Remove game and show menu again
                this.Controls.Remove(activeGame);
                activeGame.Dispose();
                activeGame = null;
                this.KeyPreview = false;
                overlay.Visible = true;
            };

            this.Controls.Add(activeGame);
            activeGame.BringToFront();
            activeGame.Show();
            activeGame.Focus();

            // Forward key events from the top-level form to the embedded game
            this.KeyPreview = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (activeGame != null && activeGame.Visible)
            {
                // Forward the key event to Form1's KeyDown handler
                activeGame.ProcessGameKeyDown(e);
                if (e.SuppressKeyPress) return;
            }
            base.OnKeyDown(e);
        }

        private void BtnOptions_Click(object sender, EventArgs e)
        {
            using (var optionsForm = new OptionsForm(PipeGap, BasePipeSpeed, DungeonIntervalSeconds, GameResolution))
            {
                if (optionsForm.ShowDialog(this) == DialogResult.OK)
                {
                    PipeGap = optionsForm.PipeGap;
                    BasePipeSpeed = optionsForm.BasePipeSpeed;
                    DungeonIntervalSeconds = optionsForm.DungeonIntervalSeconds;
                    GameResolution = optionsForm.Resolution;
                    ApplyResolution();
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

        private static int LoadHighScore()
        {
            try
            {
                if (File.Exists(SaveFile))
                {
                    string text = File.ReadAllText(SaveFile).Trim();
                    if (int.TryParse(text, out int value))
                        return value;
                }
            }
            catch { }
            return 0;
        }

        private static void SaveHighScore(int score)
        {
            try
            {
                Directory.CreateDirectory(SaveDir);
                File.WriteAllText(SaveFile, score.ToString());
            }
            catch { }
        }
    }
}
