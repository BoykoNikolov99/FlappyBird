using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private int basePipeSpeed = 6;
        // gravity is the constant acceleration applied each tick (reduced for smoother fall)
        private int gravity = 2;
        // verticalSpeed is the current velocity of the bird (positive = down)
        private int verticalSpeed = 0;
        private int score = 0;
        private bool isGameOver = false;

        /// <summary>The score achieved when the game ended.</summary>
        public int FinalScore { get; private set; }

        // high score tracking (passed in from main menu)
        private int highScore = 0;
        /// <summary>The highest score across sessions (updated live).</summary>
        public int HighScore => highScore;
        private Label highScoreText;

        // runtime label shown on game over
        private System.Windows.Forms.Label gameOverText;
        // multiple pipe pairs
        private List<PictureBox> pipeTops;
        private List<PictureBox> pipeBottoms;
        // dungeon-added pipe pairs (temporary during a dungeon)
        private List<PictureBox> dungeonTops;
        private List<PictureBox> dungeonBottoms;
        private Random rnd = new Random();
        private int pipePairs = 5; // increased number of pipe pairs
        private int pipeWidth = 100;
        private int pipeGap = 120; // tighter gap for better difficulty
        private int pipeStartX = 800;
        private int pipeSpacing = 320; // even spacing between pipe pairs
        // appearance and runtime UI
        private Color pipeColor = Color.Green;
        private Image pipeImgLoaded = null;
        private Image pipeImgTopRotated = null;
        // runtime UI sliders removed per user request
        private System.Windows.Forms.Button pauseButton;
        // on-screen pause indicator
        private System.Windows.Forms.Label pauseIndicator;
        // smooth speed ramping
        private double currentPipeSpeed;
        // dungeon mode (clustered pipes) timing
        private bool inDungeon = false;
        private DateTime lastDungeonTrigger = DateTime.MinValue;
        private DateTime dungeonEndTime = DateTime.MinValue;
        private TimeSpan dungeonInterval = TimeSpan.FromSeconds(10);
        private TimeSpan dungeonDuration = TimeSpan.FromSeconds(6);
        private int dungeonSpacing = 120; // tighter spacing during dungeon
        private int originalPipeSpacing;
        // when true we wait to remove dungeon pipes until the player has crossed the last one
        private bool pendingDungeonCleanup = false;
        private int dungeonRegionStart = int.MaxValue;
        private int dungeonRegionEnd = int.MinValue;

        public Form1()
        {
            InitializeComponent();
        }

        public Form1(int pipeGapSetting, int baseSpeedSetting, int dungeonIntervalSetting, int currentHighScore = 0)
        {
            InitializeComponent();
            pipeGap = pipeGapSetting;
            basePipeSpeed = baseSpeedSetting;
            dungeonInterval = TimeSpan.FromSeconds(dungeonIntervalSetting);
            highScore = currentHighScore;
        }

        private void ApplyPipeAppearance()
        {
            if (pipeTops == null) return;

            // If a pipe image was loaded, use it by default
            bool useImage = (pipeImgLoaded != null);

            for (int i = 0; i < pipeTops.Count; i++)
            {
                var top = pipeTops[i];
                var bottom = pipeBottoms[i];

                if (useImage)
                {
                    // use the loaded image for bottom pipes and a cached rotated image for top pipes
                    bottom.Image = pipeImgLoaded;
                    bottom.SizeMode = PictureBoxSizeMode.StretchImage;
                    bottom.BackColor = Color.Transparent;

                    try
                    {
                        top.Image = pipeImgTopRotated ?? pipeImgLoaded;
                        top.SizeMode = PictureBoxSizeMode.StretchImage;
                        top.BackColor = Color.Transparent;
                    }
                    catch
                    {
                        top.Image = pipeImgLoaded;
                        top.SizeMode = PictureBoxSizeMode.StretchImage;
                        top.BackColor = Color.Transparent;
                    }
                }
                else
                {
                    // clear images and use solid color
                    top.Image = null;
                    bottom.Image = null;
                    top.BackColor = pipeColor;
                    bottom.BackColor = pipeColor;
                }
            }
        }

        private void RebuildPipes(int newPairs)
        {
            // remove extra dynamically created pipes
            if (pipeTops != null)
            {
                for (int i = pipeTops.Count - 1; i >= 0; i--)
                {
                    var ptop = pipeTops[i];
                    var pbot = pipeBottoms[i];
                    // skip designer-named ones "pipeTop"/"pipeBottom" when i==0
                    if (i == 0)
                        continue;
                    this.Controls.Remove(ptop);
                    this.Controls.Remove(pbot);
                }
            }

            pipeTops = new List<PictureBox>();
            pipeBottoms = new List<PictureBox>();

            for (int i = 0; i < newPairs; i++)
            {
                int posX = pipeStartX + i * pipeSpacing;
                if (i == 0)
                {
                    // reuse designer controls
                    pipeTop.Width = pipeWidth;
                    pipeBottom.Width = pipeWidth;
                    pipeTop.Left = posX;
                    pipeBottom.Left = posX;
                    int topMax = Math.Max(41, this.ClientSize.Height - pipeGap - 80);
                    int topH = rnd.Next(40, topMax);
                    pipeTop.Height = topH;
                    pipeBottom.Top = topH + pipeGap;
                    pipeBottom.Height = this.ClientSize.Height - pipeBottom.Top;
                    pipeTops.Add(pipeTop);
                    pipeBottoms.Add(pipeBottom);
                }
                else
                {
                    var top = new PictureBox();
                    var bottom = new PictureBox();
                    top.Width = pipeWidth;
                    bottom.Width = pipeWidth;
                    top.Left = posX;
                    bottom.Left = posX;
                    int topMax = Math.Max(41, this.ClientSize.Height - pipeGap - 80);
                    int topH = rnd.Next(40, topMax);
                    top.Height = topH;
                    bottom.Top = topH + pipeGap;
                    bottom.Height = this.ClientSize.Height - bottom.Top;
                    top.Name = "pipeTop" + i;
                    bottom.Name = "pipeBottom" + i;
                    this.Controls.Add(top);
                    this.Controls.Add(bottom);
                    top.SendToBack();
                    bottom.SendToBack();
                    pipeTops.Add(top);
                    pipeBottoms.Add(bottom);
                }
            }

            // apply appearance to newly created pipes
            ApplyPipeAppearance();

            try
            {
                Debug.WriteLine($"RebuildPipes completed: newPairs={newPairs}, pipeTops={pipeTops.Count}");
                for (int i = 0; i < pipeTops.Count; i++)
                {
                    var t = pipeTops[i];
                    var b = pipeBottoms[i];
                    Debug.WriteLine($" Rebuilt #{i}: top={t.Name}, left={t.Left}, topH={t.Height}, bottomTop={b.Top}");
                }
            }
            catch { }
        }

        private void StartDungeon()
        {
            // don't start a new dungeon if one is already active, or if there are
            // existing dungeon pipes that haven't been cleaned up yet. This avoids
            // overlapping multiple dungeon clusters which caused the large region
            // and visual glitches.
            if (inDungeon) return;
            if (pendingDungeonCleanup) {
                Debug.WriteLine("StartDungeon skipped: pending dungeon cleanup in progress");
                return;
            }
            if (dungeonTops != null && dungeonTops.Count > 0)
            {
                Debug.WriteLine("StartDungeon skipped: existing dungeon pipes still present");
                return;
            }
            inDungeon = true;
            dungeonEndTime = DateTime.Now + dungeonDuration;
            // tighten spacing for dungeon, but ensure it's not too tight horizontally
            int effectiveDungeonSpacing = Math.Max(dungeonSpacing, pipeWidth + 40);
            // enforce a minimum horizontal spacing between dungeon pipes so the
            // bird can physically travel between them: at least three times the
            // bird width (or fall back to effectiveDungeonSpacing).
            int minDungeonSpacing = effectiveDungeonSpacing;
            if (bird != null)
            {
                try { minDungeonSpacing = Math.Max(minDungeonSpacing, bird.Width * 3); } catch { }
            }
            pipeSpacing = effectiveDungeonSpacing;
            // place new dungeon pipes after the current rightmost pipe so existing pipes continue normally
            if (dungeonTops == null) dungeonTops = new List<PictureBox>();
            if (dungeonBottoms == null) dungeonBottoms = new List<PictureBox>();

            int rightmost = pipeTops.Max(p => p.Left);
            Debug.WriteLine($"StartDungeon pre-spawn dump: rightmost={rightmost}, pipeTops={pipeTops.Count}, dungeonTops={(dungeonTops==null?0:dungeonTops.Count)}");
            try
            {
                for (int pi = 0; pi < pipeTops.Count; pi++)
                {
                    var p = pipeTops[pi];
                    Debug.WriteLine($"  Normal #{pi}: {p.Name} left={p.Left} topH={p.Height} bottomTop={pipeBottoms[pi].Top}");
                }
            }
            catch { }

            // ensure the effective gap is at least big enough for the bird to pass
            // account for bird size and any pipe image caps so the visual gap matches collision
            int imageCapMargin = (pipeImgLoaded != null) ? 14 : 8;
            int minGapForBird = (bird != null) ? bird.Height + 20 + imageCapMargin : 40;
            int effectiveGap = Math.Max(pipeGap, minGapForBird);

            Debug.WriteLine($"StartDungeon spacing: effectiveDungeonSpacing={effectiveDungeonSpacing}, effectiveGap={effectiveGap}");

            Debug.WriteLine($"StartDungeon at {DateTime.Now:HH:mm:ss.fff}: rightmost={rightmost}, pipePairs={pipePairs}, pipeSpacing={pipeSpacing}, effectiveGap={effectiveGap}");

            // Extra debugging: show current dungeon region and sample of upcoming pipe X positions
            Debug.WriteLine($"Current dungeonRegionStart={dungeonRegionStart}, dungeonRegionEnd={dungeonRegionEnd}");

            // reduce density and spawn further right to avoid overlapping existing pipes
            int spawnCount = Math.Max(1, (int)Math.Ceiling(pipePairs * 0.6));
            int spawnStart = rightmost + minDungeonSpacing * 4; // push dungeon start further to the right
            Debug.WriteLine($"Dungeon spawn: spawnCount={spawnCount}, spawnStart={spawnStart}, effectiveDungeonSpacing={effectiveDungeonSpacing}, minDungeonSpacing={minDungeonSpacing}");

            for (int i = 0; i < spawnCount; i++)
            {
                var top = new PictureBox();
                var bottom = new PictureBox();

                top.BackColor = pipeColor;
                bottom.BackColor = pipeColor;

                top.Width = pipeWidth;
                bottom.Width = pipeWidth;

                int newLeft = spawnStart + i * minDungeonSpacing;

                // Ensure dungeon pipes do not spawn too close to any existing
                // normal pipes or previously created dungeon pipes. If they do,
                // push them right by minDungeonSpacing until there's no conflict.
                try
                {
                    bool adjusted = true;
                    while (adjusted)
                    {
                        adjusted = false;
                        // check against normal pipes
                        if (pipeTops != null && pipeTops.Count > 0)
                        {
                            foreach (var np in pipeTops)
                            {
                                if (Math.Abs(newLeft - np.Left) < minDungeonSpacing)
                                {
                                    newLeft = np.Left + minDungeonSpacing;
                                    adjusted = true;
                                    break;
                                }
                            }
                        }
                        // check against previously placed dungeon pipes
                        if (!adjusted && dungeonTops != null && dungeonTops.Count > 0)
                        {
                            foreach (var dp in dungeonTops)
                            {
                                if (Math.Abs(newLeft - dp.Left) < minDungeonSpacing)
                                {
                                    newLeft = dp.Left + minDungeonSpacing;
                                    adjusted = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
                top.Left = newLeft;
                bottom.Left = newLeft;

                // Constrained vertical placement: each dungeon pipe's gap
                // center must be within maxDungeonShift of the previous pipe's
                // gap center so the bird can physically fly between them.
                int mid = this.ClientSize.Height / 2;
                int topH;
                int maxDungeonShift = Math.Max(50, pipeGap / 2); // max vertical change between consecutive gaps
                int minTopH = 30;
                int maxTopH = Math.Max(31, this.ClientSize.Height - effectiveGap - 40);

                // Determine the previous gap center: use the last dungeon pipe
                // we just created, or the nearest normal pipe if this is the first.
                double prevDungeonGapCenter = mid; // default to screen center
                if (i > 0 && dungeonTops.Count > 0)
                {
                    var prevDT = dungeonTops[dungeonTops.Count - 1];
                    var prevDB = dungeonBottoms[dungeonBottoms.Count - 1];
                    prevDungeonGapCenter = (prevDT.Height + prevDB.Top) / 2.0;
                }
                else
                {
                    // align first dungeon pipe with nearest normal pipe
                    var nearestNormal = pipeTops.OrderBy(p => Math.Abs(p.Left - newLeft)).FirstOrDefault();
                    if (nearestNormal != null)
                    {
                        int ni = pipeTops.IndexOf(nearestNormal);
                        if (ni >= 0 && ni < pipeBottoms.Count)
                            prevDungeonGapCenter = (nearestNormal.Height + pipeBottoms[ni].Top) / 2.0;
                    }
                }

                // pick a random gap center within the allowed shift range
                int gapCenterMin = (int)Math.Max(minTopH + effectiveGap / 2, prevDungeonGapCenter - maxDungeonShift);
                int gapCenterMax = (int)Math.Min(this.ClientSize.Height - 40 - effectiveGap / 2, prevDungeonGapCenter + maxDungeonShift);
                if (gapCenterMin > gapCenterMax) gapCenterMin = gapCenterMax = mid;
                int gapCenter = rnd.Next(gapCenterMin, gapCenterMax + 1);
                topH = gapCenter - effectiveGap / 2;
                topH = Math.Max(minTopH, Math.Min(topH, maxTopH));

                Debug.WriteLine($"Dungeon pipe #{i} gap: prevCenter={prevDungeonGapCenter:F0}, gapCenterRange=[{gapCenterMin},{gapCenterMax}], chosen={gapCenter}, topH={topH}");

                top.Height = topH;
                bottom.Top = topH + effectiveGap;
                bottom.Height = this.ClientSize.Height - bottom.Top;

                top.Name = "dungeonTop" + i + "_" + Guid.NewGuid().ToString("N");
                bottom.Name = "dungeonBottom" + i + "_" + Guid.NewGuid().ToString("N");

                this.Controls.Add(top);
                this.Controls.Add(bottom);
                top.SendToBack();
                bottom.SendToBack();

                // track dungeon pipes separately so they don't interfere with normal pipe respawn logic
                dungeonTops.Add(top);
                dungeonBottoms.Add(bottom);

                Debug.WriteLine($"Created dungeon pipe #{i}: top={top.Name}, left={top.Left}, height={top.Height}, bottomTop={bottom.Top}, bottomHeight={bottom.Height}");
            }
            // Apply appearance (images/colors) to the newly created pipes
            ApplyPipeAppearance();

            // record the horizontal region occupied by the dungeon pipes so normal
            // pipes can avoid spawning into this region and causing visual overlap
            if (dungeonTops != null && dungeonTops.Count > 0)
            {
                dungeonRegionStart = dungeonTops.Min(p => p.Left);
                dungeonRegionEnd = dungeonTops.Max(p => p.Left + p.Width);
                Debug.WriteLine($"Dungeon region: start={dungeonRegionStart}, end={dungeonRegionEnd}");
            }

            // Ensure bird and UI remain visible above pipes
            if (bird != null) bird.BringToFront();
            if (scoreText != null) scoreText.BringToFront();
            if (gameOverText != null) gameOverText.BringToFront();
            Debug.WriteLine($"Dungeon pipes total: {dungeonTops.Count}");

            // apply appearance to dungeon pipes as well
            bool useImage = (pipeImgLoaded != null);
            if (dungeonTops != null && dungeonTops.Count > 0)
            {
                for (int i = 0; i < dungeonTops.Count; i++)
                {
                    var top = dungeonTops[i];
                    var bottom = dungeonBottoms[i];
                    if (useImage)
                    {
                        bottom.Image = pipeImgLoaded;
                        bottom.SizeMode = PictureBoxSizeMode.StretchImage;
                        bottom.BackColor = Color.Transparent;
                        try
                        {
                            top.Image = pipeImgTopRotated ?? pipeImgLoaded;
                            top.SizeMode = PictureBoxSizeMode.StretchImage;
                            top.BackColor = Color.Transparent;
                        }
                        catch
                        {
                            top.Image = pipeImgLoaded;
                            top.SizeMode = PictureBoxSizeMode.StretchImage;
                            top.BackColor = Color.Transparent;
                        }
                    }
                    else
                    {
                        top.Image = null;
                        bottom.Image = null;
                        top.BackColor = pipeColor;
                        bottom.BackColor = pipeColor;
                    }
                }
            }
        }

        private void EndDungeon()
        {
            if (!inDungeon) return;
            inDungeon = false;
            // restore normal spacing
            pipeSpacing = originalPipeSpacing;

            Debug.WriteLine($"EndDungeon at {DateTime.Now:HH:mm:ss.fff}: dungeonTops={dungeonTops?.Count ?? 0}, dungeonBottoms={dungeonBottoms?.Count ?? 0}");

            // Instead of forcibly removing dungeon pipes immediately when the dungeon ends
            // (which can make them disappear before the player reaches them), delay final
            // removal until the player has actually crossed the last dungeon pipe.
            // Keep any on-screen or upcoming dungeon pipes; movement logic will still
            // remove pipes that go off-screen early.
            if (dungeonTops != null && dungeonTops.Count > 0)
            {
                for (int i = dungeonTops.Count - 1; i >= 0; i--)
                {
                    var top = dungeonTops[i];
                    var bottom = (i < dungeonBottoms.Count) ? dungeonBottoms[i] : null;
                    if (top == null) continue;

                    if (top.Left < -pipeWidth)
                    {
                        Debug.WriteLine($"Removing off-screen dungeon top: {top.Name}, left={top.Left}");
                        this.Controls.Remove(top);
                        if (bottom != null) this.Controls.Remove(bottom);
                        try { top.Dispose(); } catch { }
                        if (bottom != null) try { bottom.Dispose(); } catch { }
                        dungeonTops.RemoveAt(i);
                        if (bottom != null && i < dungeonBottoms.Count) dungeonBottoms.RemoveAt(i);
                    }
                    else
                    {
                        Debug.WriteLine($"Keeping on-screen/upcoming dungeon top: {top.Name}, left={top.Left}");
                    }
                }
            }

            // set a flag so we only fully remove any remaining dungeon pipes after the
            // player (bird) has passed the last dungeon pipe. The movement loop will
            // detect when that happens and finish cleanup.
            pendingDungeonCleanup = true;
            Debug.WriteLine("EndDungeon complete - remaining dungeon pipes will be kept until player crosses the last one");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // ensure the form receives key events even when child controls have focus
            this.KeyPreview = true;
            IconHelper.SetFormIcon(this);
            // Ensure the bird PictureBox is visible and on top.
            // Reset size/location so it starts visible (higher on screen) and won't immediately fall off.
            bird.Visible = true;
            bird.BackColor = Color.Yellow;
            bird.Size = new Size(40, 28);
            // Start the bird higher so gravity doesn't push it off-screen before the user can react.
            bird.Location = new Point(56, 200);
            bird.BringToFront();

            // Try loading flappy-bird.png from the application folder first, then fall
            // back to bird.png. If neither exists the yellow box remains visible.
            string[] candidateNames = new[] { "flappy-bird.png", "bird.png" };
            foreach (var name in candidateNames)
            {
                string imagePath = Path.Combine(Application.StartupPath, name);
                if (!File.Exists(imagePath))
                    continue;

                try
                {
                    var img = Image.FromFile(imagePath);
                    bird.Image = img;
                    bird.SizeMode = PictureBoxSizeMode.StretchImage;
                    // Use a fixed display size for the bird so large source images don't
                    // make the PictureBox huge and cause immediate collisions.
                    bird.Size = new Size(45, 32);
                    bird.BackColor = Color.Transparent;
                    bird.BringToFront();
                    break; // loaded an image, stop searching
                }
                catch
                {
                    // ignore load errors and try next candidate
                }
            }

            // Create the game over label but keep it hidden until needed
            gameOverText = new Label();
            gameOverText.AutoSize = false;
            gameOverText.Size = new Size(400, 60);
            gameOverText.TextAlign = ContentAlignment.MiddleCenter;
            gameOverText.Font = new Font("Microsoft Sans Serif", 18F, FontStyle.Bold, GraphicsUnit.Point, ((byte)(0)));
            gameOverText.ForeColor = Color.Red;
            gameOverText.BackColor = Color.Transparent;
            gameOverText.Location = new Point((this.ClientSize.Width - gameOverText.Width) / 2, (this.ClientSize.Height - gameOverText.Height) / 2);
            gameOverText.Text = "";
            gameOverText.Visible = false;

            // Create in-game high score label next to the score
            highScoreText = new Label();
            highScoreText.AutoSize = true;
            highScoreText.Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Bold, GraphicsUnit.Point, ((byte)(0)));
            highScoreText.ForeColor = Color.Gold;
            highScoreText.BackColor = Color.Transparent;
            highScoreText.Text = "Best: " + highScore;
            highScoreText.Location = new Point(scoreText.Right + 20, scoreText.Top + 5);
            this.Controls.Add(highScoreText);
            highScoreText.BringToFront();
            gameOverText.BringToFront();
            this.Controls.Add(gameOverText);

            // create a pause button so user can pause the game to take screenshots
            pauseButton = new Button();
            pauseButton.Size = new Size(80, 30);
            pauseButton.Location = new Point(this.ClientSize.Width - pauseButton.Width - 10, 10);
            pauseButton.Text = "Pause";
            pauseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pauseButton.Click += PauseButton_Click;
            this.Controls.Add(pauseButton);
            pauseButton.BringToFront();

            // create an on-screen pause indicator for easy screenshots
            pauseIndicator = new Label();
            pauseIndicator.Size = new Size(220, 80);
            pauseIndicator.Font = new Font("Microsoft Sans Serif", 20F, FontStyle.Bold, GraphicsUnit.Point);
            pauseIndicator.Text = "PAUSED";
            pauseIndicator.TextAlign = ContentAlignment.MiddleCenter;
            pauseIndicator.ForeColor = Color.White;
            pauseIndicator.BackColor = Color.FromArgb(160, 0, 0, 0);
            pauseIndicator.Visible = false;
            pauseIndicator.Location = new Point((this.ClientSize.Width - pauseIndicator.Width) / 2, (this.ClientSize.Height - pauseIndicator.Height) / 2);
            pauseIndicator.Anchor = AnchorStyles.None;
            this.Controls.Add(pauseIndicator);
            pauseIndicator.BringToFront();

            // Initialize multiple pipes (include the designer pipes as the first pair)
            pipeTops = new List<PictureBox>();
            pipeBottoms = new List<PictureBox>();
            // prepare lists for dungeon-added pipes
            dungeonTops = new List<PictureBox>();
            dungeonBottoms = new List<PictureBox>();

            for (int i = 0; i < pipePairs; i++)
            {
                int posX = pipeStartX + i * pipeSpacing;

                if (i == 0)
                {
                    // reuse existing designer controls
                    pipeTop.Width = pipeWidth;
                    pipeBottom.Width = pipeWidth;
                    pipeTop.Left = posX;
                    pipeBottom.Left = posX;
                    // set randomized heights
                    int topMax = Math.Max(41, this.ClientSize.Height - pipeGap - 80);
                    int topH = rnd.Next(40, topMax);
                    pipeTop.Height = topH;
                    pipeBottom.Top = topH + pipeGap;
                    pipeBottom.Height = this.ClientSize.Height - pipeBottom.Top;

                    pipeTops.Add(pipeTop);
                    pipeBottoms.Add(pipeBottom);
                }
                else
                {
                    // create new PictureBoxes for extra pipes
                    var top = new PictureBox();
                    var bottom = new PictureBox();

                    top.BackColor = Color.Green;
                    bottom.BackColor = Color.Green;

                    top.Width = pipeWidth;
                    bottom.Width = pipeWidth;

                    top.Left = posX;
                    bottom.Left = posX;

                    int topH = rnd.Next(40, this.ClientSize.Height - pipeGap - 80);
                    top.Height = topH;
                    bottom.Top = topH + pipeGap;
                    bottom.Height = this.ClientSize.Height - bottom.Top;

                    top.Name = "pipeTop" + i;
                    bottom.Name = "pipeBottom" + i;

                    this.Controls.Add(top);
                    this.Controls.Add(bottom);

                    // ensure pipes are behind bird and score
                    top.SendToBack();
                    bottom.SendToBack();

                    pipeTops.Add(top);
                    pipeBottoms.Add(bottom);
                }
            }
            // Try loading a pipe image to use for visual variation
            Image pipeImg = null;
            string pipePath = Path.Combine(Application.StartupPath, "pipe.png");
            if (File.Exists(pipePath))
            {
                try
                {
                    pipeImg = Image.FromFile(pipePath);
                }
                catch
                {
                    pipeImg = null;
                }
            }

            // store loaded pipe image for later use
            if (pipeImg != null)
            {
                pipeImgLoaded = pipeImg;
                try
                {
                    // create a single rotated copy for top pipes and reuse it to avoid per-control cloning
                    pipeImgTopRotated = (Image)pipeImgLoaded.Clone();
                    pipeImgTopRotated.RotateFlip(RotateFlipType.Rotate180FlipNone);
                }
                catch
                {
                    pipeImgTopRotated = pipeImgLoaded;
                }
            }

            // Apply initial appearance to pipes (image if loaded and enabled, else color)
            ApplyPipeAppearance();

            // enable double buffering for smoother rendering
            this.DoubleBuffered = true;

            // Try loading a background image from the application folder
            string bgPath = Path.Combine(Application.StartupPath, "background.png");
            if (File.Exists(bgPath))
            {
                try
                {
                    var bgImg = Image.FromFile(bgPath);
                    this.BackgroundImage = bgImg;
                    this.BackgroundImageLayout = ImageLayout.Stretch;
                }
                catch
                {
                    // ignore background load errors
                }
            }

            // initialize current speed for smooth ramping
            currentPipeSpeed = basePipeSpeed;
            // save original spacing
            originalPipeSpacing = pipeSpacing;
            // initialize dungeon trigger time so first dungeon happens after interval
            lastDungeonTrigger = DateTime.Now;
        }

        private void bird_Click(object sender, EventArgs e)
        {

        }

        private void gameTimer_Tick(object sender, EventArgs e)
        {
            // compute target pipe speed based on score
            double targetSpeed = basePipeSpeed + (score / 5.0);
            targetSpeed = Math.Min(targetSpeed, basePipeSpeed + 12);
            // smooth ramping (lerp) towards targetSpeed
            currentPipeSpeed = Lerp(currentPipeSpeed, targetSpeed, 0.02);
            int useSpeed = (int)Math.Round(currentPipeSpeed);
            // Dungeon trigger handling: every dungeonInterval seconds start a clustered region
            var now = DateTime.Now;
            if (!inDungeon && (now - lastDungeonTrigger) >= dungeonInterval)
            {
                StartDungeon();
                lastDungeonTrigger = now;
            }

            if (inDungeon && now >= dungeonEndTime)
            {
                EndDungeon();
            }

            gameTimer_Tick(sender, e, useSpeed);
        }

        private double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private void gameTimer_Tick(object sender, EventArgs e, int pipeSpeed1)
        {
            Debug.WriteLine($"Tick: time={DateTime.Now:HH:mm:ss.fff}, pipeSpeed={pipeSpeed1}, inDungeon={inDungeon}, pendingDungeonCleanup={pendingDungeonCleanup}, pipeTops={pipeTops?.Count ?? 0}, dungeonTops={dungeonTops?.Count ?? 0}");

            // Dump positions of normal pipes for debugging
            try
            {
                if (pipeTops != null)
                {
                    for (int pi = 0; pi < pipeTops.Count; pi++)
                    {
                        var p = pipeTops[pi];
                        Debug.WriteLine($"NormalPipe #{pi}: name={p.Name}, left={p.Left}, topH={p.Height}, bottomTop={(pipeBottoms.Count>pi?pipeBottoms[pi].Top:-1)}");
                    }
                }
                if (dungeonTops != null)
                {
                    for (int di = 0; di < dungeonTops.Count; di++)
                    {
                        var d = dungeonTops[di];
                        Debug.WriteLine($"DungeonPipe #{di}: name={d.Name}, left={d.Left}, topH={d.Height}, bottomTop={(dungeonBottoms.Count>di?dungeonBottoms[di].Top:-1)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Tick dump error: " + ex);
            }
            if (isGameOver)
                return;

            // if pipe lists are empty, nothing to do
            if (pipeTops == null || pipeTops.Count == 0) return;

            // apply gravity to vertical speed and move bird
            verticalSpeed += gravity;
            // cap vertical speed to avoid excessive fall velocity
            verticalSpeed = Math.Min(verticalSpeed, 10);
            bird.Top += verticalSpeed;

            // move all pipes and handle respawn/score
            // maintain a respawn cursor so multiple pipes that need respawning
            // in the same tick get placed at increasing positions and don't collide
            // Start the respawn cursor at the rightmost normal pipe.  Also include
            // dungeon pipes (if present) so we don't accidentally place a normal
            // pipe to the left of an active dungeon pipe and create crowding.
            int respawnCursor = pipeTops.Max(p => p.Left);
            if (dungeonTops != null && dungeonTops.Count > 0)
            {
                try
                {
                    int dungeonRight = dungeonTops.Max(d => d.Left + d.Width);
                    respawnCursor = Math.Max(respawnCursor, dungeonRight);
                }
                catch { }
            }
            for (int i = 0; i < pipeTops.Count; i++)
            {
                var top = pipeTops[i];
                var bottom = pipeBottoms[i];

                Debug.WriteLine($"Moving normal pipe #{i}: name={top.Name}, beforeLeft={top.Left}");
                top.Left -= pipeSpeed1;
                bottom.Left -= pipeSpeed1;
                Debug.WriteLine($"Moved normal pipe #{i}: name={top.Name}, afterLeft={top.Left}");

                if (top.Left < -pipeWidth)
                {
                    Debug.WriteLine($"Respawn triggered for {top.Name}: currentLeft={top.Left}, respawnCursor={respawnCursor}");
                    // respawn behavior
                    // propose new position after the respawn cursor
                    int proposedLeft = respawnCursor + pipeSpacing;
                    Debug.WriteLine($"Initial proposedLeft for respawn of {top.Name} = {proposedLeft} (respawnCursor={respawnCursor}, pipeSpacing={pipeSpacing})");

                    // enforce a minimum horizontal spacing between spawned normal pipes
                    // to ensure the bird has room to travel between them. The minimum
                    // spacing is three times the bird's width (as requested) but at
                    // least the configured pipeSpacing.
                    int minNormalSpacing = pipeSpacing;
                    if (bird != null)
                    {
                        try { minNormalSpacing = Math.Max(minNormalSpacing, bird.Width * 3); } catch { }
                    }
                    Debug.WriteLine($"minNormalSpacing={minNormalSpacing}, pipeSpacing={pipeSpacing}, birdWidth={(bird!=null?bird.Width:-1)}");

                    // if there are dungeon pipes present, ensure the proposedLeft does
                    // not overlap any dungeon pipe. Prefer placing before the first
                    // overlapping dungeon pipe if there's space; otherwise push the
                    // normal pipe to the right of the last overlapping dungeon pipe.
                    int buffer = 12; // extra spacing to avoid cap overlap
                    Debug.WriteLine($"Checking overlaps with dungeon pipes: dungeonCount={dungeonTops?.Count ?? 0}, dungeonRegion=[{dungeonRegionStart},{dungeonRegionEnd}]");
                    if (dungeonTops != null && dungeonTops.Count > 0)
                    {
                        // iterate to resolve overlaps with multiple dungeon pipes
                        for (int attempt = 0; attempt < 8; attempt++)
                        {
                            bool changed = false;
                            foreach (var d in dungeonTops)
                            {
                                int dStart = d.Left - buffer;
                                int dEnd = d.Left + d.Width + buffer;
                                bool overlaps = (proposedLeft + pipeWidth) > dStart && proposedLeft < dEnd;
                                if (overlaps)
                                {
                                    // try placing before this dungeon pipe
                                    int placeBefore = dStart - pipeWidth;
                                    if (placeBefore > respawnCursor + 20)
                                    {
                                        proposedLeft = placeBefore;
                                        changed = true;
                                        break; // placed before, re-evaluate against others
                                    }
                                    else
                                    {
                                        // no room before; push past this dungeon pipe
                                        int placeAfter = dEnd + pipeSpacing;
                                        if (placeAfter > proposedLeft)
                                        {
                                            proposedLeft = placeAfter;
                                            changed = true;
                                            // continue checking against other dungeon pipes
                                        }
                                    }
                                }
                            }
                            Debug.WriteLine($"Overlap attempt {attempt}: proposedLeft={proposedLeft}, changed={changed}");
                            if (!changed) break;
                        }
                    }
                    Debug.WriteLine($"After dungeon adjustments proposedLeft={proposedLeft}");
                    // dump nearby normal & dungeon pipes to help diagnose overlaps
                    try
                    {
                        var nearbyNormals = pipeTops.Where(p => Math.Abs(p.Left - proposedLeft) < minNormalSpacing * 4)
                                                     .Select(p => $"{p.Name}:{p.Left}").ToArray();
                        var nearbyDungeons = (dungeonTops ?? new List<PictureBox>()).Where(d => Math.Abs(d.Left - proposedLeft) < minNormalSpacing * 6)
                                                     .Select(d => $"{d.Name}:{d.Left}").ToArray();
                        Debug.WriteLine($"Nearby normals: {string.Join(",", nearbyNormals)}");
                        Debug.WriteLine($"Nearby dungeons: {string.Join(",", nearbyDungeons)}");
                    }
                    catch { }

                    // If the proposed position would fall inside the horizontal
                    // region occupied by the dungeon pipes (or too close to them),
                    // push it to the right edge of that region. This prevents
                    // normal pipes from being placed inside/adjacent to dungeon
                    // clusters which can create impossible vertical sequences.
                    if (dungeonTops != null && dungeonTops.Count > 0)
                    {
                        // if overlapping dungeon region, move to after the dungeon
                        if (proposedLeft + pipeWidth > dungeonRegionStart && proposedLeft < dungeonRegionEnd)
                        {
                            proposedLeft = dungeonRegionEnd + pipeSpacing + 8; // small safe buffer
                        }

                        // if an EndDungeon is pending cleanup, be conservative and
                        // ensure normals spawn after the recorded dungeon region
                        if (pendingDungeonCleanup && proposedLeft < dungeonRegionEnd + pipeSpacing + 8)
                        {
                            proposedLeft = dungeonRegionEnd + pipeSpacing + 8;
                        }
                    }

                    // ensure we respect the minimum normal spacing relative to the
                    // respawnCursor as a final step after any dungeon adjustments
                    if (proposedLeft < respawnCursor + minNormalSpacing)
                    {
                        proposedLeft = respawnCursor + minNormalSpacing;
                    }

                    // Additionally ensure the proposedLeft doesn't collide with any
                    // other existing normal pipes or dungeon pipes. If it does, push
                    // it right until it no longer conflicts.
                    try
                    {
                        int maxAttempts = 16;
                        while (maxAttempts-- > 0)
                        {
                            bool conflicted = false;
                            if (pipeTops != null)
                            {
                                foreach (var p in pipeTops)
                                {
                                    if (p == top) continue; // skip the one we're respawning
                                    if (Math.Abs(proposedLeft - p.Left) < minNormalSpacing)
                                    {
                                        proposedLeft = p.Left + minNormalSpacing;
                                        conflicted = true;
                                        break;
                                    }
                                }
                            }
                            if (!conflicted && dungeonTops != null)
                            {
                                foreach (var d in dungeonTops)
                                {
                                    if (Math.Abs(proposedLeft - d.Left) < minNormalSpacing)
                                    {
                                        proposedLeft = d.Left + minNormalSpacing;
                                        conflicted = true;
                                        break;
                                    }
                                }
                            }
                            if (!conflicted) break;
                        }
                    }
                    catch { }

                    // Final diagnostics before assignment
                    Debug.WriteLine($"Final proposedLeft for {top.Name} = {proposedLeft} (respawnCursor before assign={respawnCursor})");
                    try
                    {
                        Debug.WriteLine($"DungeonRegion=[{dungeonRegionStart},{dungeonRegionEnd}], pendingDungeonCleanup={pendingDungeonCleanup}");
                    }
                    catch { }

                    // advance the cursor to avoid placing subsequent respawns at the same spot
                    // and assign the final left to the pipe controls only after all
                    // proposedLeft adjustments have been made.
                    respawnCursor = proposedLeft;
                    top.Left = proposedLeft;
                    bottom.Left = proposedLeft;

                    Debug.WriteLine($"Assigned new left for {top.Name}: left={top.Left}");

                    int maxTopLimit = Math.Max(41, this.ClientSize.Height - pipeGap - 80);
                    int topHNormal;

                    // Find the nearest pipe (normal OR dungeon) that the bird
                    // will encounter just before this respawned pipe. Constrain
                    // this pipe's gap center relative to that predecessor so we
                    // never create an impossible vertical jump.
                    int maxShift = Math.Max(50, pipeGap / 2);
                    double prevGapCenter = this.ClientSize.Height / 2.0;
                    string prevSource = "(default mid)";

                    // collect all pipes to the left of proposedLeft (the ones the
                    // bird flies through before reaching this pipe)
                    PictureBox bestPrev = null;
                    int bestPrevLeft = int.MinValue;
                    // check normal pipes
                    if (pipeTops != null)
                    {
                        foreach (var p in pipeTops)
                        {
                            if (p == top) continue;
                            if (p.Left < proposedLeft && p.Left > bestPrevLeft)
                            {
                                bestPrev = p;
                                bestPrevLeft = p.Left;
                            }
                        }
                    }
                    // check dungeon pipes
                    if (dungeonTops != null)
                    {
                        for (int di = 0; di < dungeonTops.Count; di++)
                        {
                            var d = dungeonTops[di];
                            if (d.Left < proposedLeft && d.Left > bestPrevLeft)
                            {
                                bestPrev = d;
                                bestPrevLeft = d.Left;
                            }
                        }
                    }

                    if (bestPrev != null)
                    {
                        // determine which list it belongs to, to get the bottom
                        int bpIdx = pipeTops.IndexOf(bestPrev);
                        if (bpIdx >= 0 && bpIdx < pipeBottoms.Count)
                        {
                            prevGapCenter = (bestPrev.Height + pipeBottoms[bpIdx].Top) / 2.0;
                            prevSource = $"normal {bestPrev.Name}";
                        }
                        else if (dungeonTops != null)
                        {
                            int dIdx = dungeonTops.IndexOf(bestPrev);
                            if (dIdx >= 0 && dIdx < dungeonBottoms.Count)
                            {
                                prevGapCenter = (bestPrev.Height + dungeonBottoms[dIdx].Top) / 2.0;
                                prevSource = $"dungeon {bestPrev.Name}";
                            }
                        }
                    }
                    Debug.WriteLine($"Respawn gap ref: prevSource={prevSource}, prevGapCenter={prevGapCenter:F0}, maxShift={maxShift}");

                    // pick a random gap center within the allowed shift range
                    int gcMin = (int)Math.Max(30 + pipeGap / 2, prevGapCenter - maxShift);
                    int gcMax = (int)Math.Min(this.ClientSize.Height - 40 - pipeGap / 2, prevGapCenter + maxShift);
                    if (gcMin > gcMax) { gcMin = gcMax = this.ClientSize.Height / 2; }
                    int newGapCenter = rnd.Next(gcMin, gcMax + 1);
                    topHNormal = Math.Max(30, Math.Min(newGapCenter - pipeGap / 2, maxTopLimit));
                    Debug.WriteLine($"Respawn gap result: gcRange=[{gcMin},{gcMax}], chosen={newGapCenter}, topHNormal={topHNormal}");

                    top.Height = topHNormal;
                    bottom.Top = topHNormal + pipeGap;
                    bottom.Height = this.ClientSize.Height - bottom.Top;

                    Debug.WriteLine($"Final respawn {top.Name}: left={top.Left}, topH={top.Height}, bottomTop={bottom.Top}");

                    score++;
                }
            }

            // move dungeon pipes (separate from normal pipes)
            if (dungeonTops != null)
            {
                for (int i = 0; i < dungeonTops.Count; i++)
                {
                    var top = dungeonTops[i];
                    var bottom = dungeonBottoms[i];
                    Debug.WriteLine($"Moving dungeon pipe #{i}: name={top.Name}, beforeLeft={top.Left}");
                    top.Left -= pipeSpeed1;
                    bottom.Left -= pipeSpeed1;
                    Debug.WriteLine($"Moved dungeon pipe #{i}: name={top.Name}, afterLeft={top.Left}");

                    // if a dungeon pipe moves off-screen, remove it (cleanup early)
                    if (top.Left < -pipeWidth)
                    {
                        Debug.WriteLine($"Dungeon pipe off-screen, removing: {top.Name}, left={top.Left}");
                        this.Controls.Remove(top);
                        this.Controls.Remove(bottom);
                        try { top.Dispose(); } catch { }
                        try { bottom.Dispose(); } catch { }
                        dungeonTops.RemoveAt(i);
                        dungeonBottoms.RemoveAt(i);
                        i--;
                        continue;
                    }
                }
                // recompute the dungeon horizontal region based on current pipe positions
                if (dungeonTops.Count > 0)
                {
                    try
                    {
                        dungeonRegionStart = dungeonTops.Min(p => p.Left);
                        dungeonRegionEnd = dungeonTops.Max(p => p.Left + p.Width);
                    Debug.WriteLine($"Recomputed dungeon region: start={dungeonRegionStart}, end={dungeonRegionEnd}");
                    }
                    catch
                    {
                        dungeonRegionStart = int.MaxValue;
                        dungeonRegionEnd = int.MinValue;
                    }
                }
                else
                {
                    // no dungeon pipes present
                    dungeonRegionStart = int.MaxValue;
                    dungeonRegionEnd = int.MinValue;
                }
                // If we've signaled that dungeon cleanup is pending (EndDungeon called),
                // only finish removing remaining dungeon pipes once the player has
                // crossed the last dungeon pipe. Crossing is detected when the rightmost
                // dungeon pipe's right edge is left of the bird's left coordinate.
                if (pendingDungeonCleanup && dungeonTops.Count > 0)
                {
                    int rightmostDungeonEdge = dungeonTops.Max(p => p.Left + p.Width);
                Debug.WriteLine($"PendingDungeonCleanup check: rightmostDungeonEdge={rightmostDungeonEdge}, bird.Left={bird?.Left ?? -1}");
                    if (rightmostDungeonEdge < bird.Left)
                    {
                        Debug.WriteLine($"Player crossed last dungeon pipe (edge={rightmostDungeonEdge}), cleaning up remaining dungeon pipes.");
                        // remove all remaining dungeon pipes now
                        for (int i = dungeonTops.Count - 1; i >= 0; i--)
                        {
                            var top = dungeonTops[i];
                            var bottom = (i < dungeonBottoms.Count) ? dungeonBottoms[i] : null;
                            if (top != null)
                            {
                                this.Controls.Remove(top);
                                try { top.Dispose(); } catch { }
                            }
                            if (bottom != null)
                            {
                                this.Controls.Remove(bottom);
                                try { bottom.Dispose(); } catch { }
                            }
                        }
                        dungeonTops.Clear();
                        dungeonBottoms.Clear();
                        pendingDungeonCleanup = false;
                            // reset recorded dungeon region
                            dungeonRegionStart = int.MaxValue;
                            dungeonRegionEnd = int.MinValue;
                    }
                }
            }

            scoreText.Text = "Score: " + score;

            // update high score live during gameplay
            if (score > highScore)
                highScore = score;
            if (highScoreText != null)
                highScoreText.Text = "Best: " + highScore;

            // Collision detection using shrunken hitboxes to match visible sprites
            var birdRect = bird.Bounds;
            birdRect.Inflate(-8, -6); // shrink bird hitbox to forgive transparent edges but still detect real overlaps

            // check each normal pipe pair
            for (int i = 0; i < pipeTops.Count; i++)
            {
                var top = pipeTops[i];
                var bottom = pipeBottoms[i];

                var topRect = top.Bounds;
                var bottomRect = bottom.Bounds;
                topRect.Inflate(-6, -4);
                bottomRect.Inflate(-6, -4);

                if (birdRect.IntersectsWith(topRect) || birdRect.IntersectsWith(bottomRect))
                {
                    GameOver();
                    break;
                }
            }

            // check dungeon pipes as well
            if (dungeonTops != null)
            {
                for (int i = 0; i < dungeonTops.Count; i++)
                {
                    var top = dungeonTops[i];
                    var bottom = dungeonBottoms[i];

                    var topRect = top.Bounds;
                    var bottomRect = bottom.Bounds;
                    topRect.Inflate(-6, -4);
                    bottomRect.Inflate(-6, -4);

                    if (birdRect.IntersectsWith(topRect) || birdRect.IntersectsWith(bottomRect))
                    {
                        GameOver();
                        break;
                    }
                }
            }

            // treat out-of-bounds with a small tolerance at the bottom
            if (bird.Top < 0 || bird.Bottom > this.ClientSize.Height - 5)
            {
                GameOver();
            }
        }

        // button to return to main menu on game over
        private Button btnMainMenu;

        private void GameOver()
        {
            isGameOver = true;
            FinalScore = score;
            gameTimer.Stop();
            gameOverText.Text = "Game Over! Press R to restart";
            // center the label in case window size changed
            gameOverText.Location = new Point((this.ClientSize.Width - gameOverText.Width) / 2, (this.ClientSize.Height - gameOverText.Height) / 2);
            gameOverText.Visible = true;
            gameOverText.BringToFront();

            // show main menu button
            if (btnMainMenu == null)
            {
                btnMainMenu = new Button();
                btnMainMenu.Text = "Main Menu";
                btnMainMenu.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
                btnMainMenu.Size = new Size(140, 38);
                btnMainMenu.FlatStyle = FlatStyle.Flat;
                btnMainMenu.FlatAppearance.BorderColor = Color.White;
                btnMainMenu.FlatAppearance.BorderSize = 2;
                btnMainMenu.BackColor = Color.FromArgb(200, 34, 139, 34);
                btnMainMenu.ForeColor = Color.White;
                btnMainMenu.Cursor = Cursors.Hand;
                btnMainMenu.Click += (s, args) => { this.Close(); };
                this.Controls.Add(btnMainMenu);
            }
            btnMainMenu.Location = new Point(
                (this.ClientSize.Width - btnMainMenu.Width) / 2,
                gameOverText.Bottom + 10);
            btnMainMenu.Visible = true;
            btnMainMenu.BringToFront();
        }

        private void RestartGame()
        {
            // Reset core state
            isGameOver = false;
            score = 0;
            scoreText.Text = "Score: " + score;

            // hide main menu button if visible
            if (btnMainMenu != null) btnMainMenu.Visible = false;

            // Stop timer while we reconstruct scene to avoid race with game loop
            try { if (gameTimer != null) gameTimer.Stop(); } catch { }

            // Ensure pipe spacing restored to original value before rebuilding
            pipeSpacing = originalPipeSpacing;

            // Remove and dispose any leftover dungeon pipes that may have been left
            // around when the player died. This prevents stale dungeon pipes from
            // influencing normal pipe respawn logic after restart.
            if (dungeonTops != null)
            {
                Debug.WriteLine($"RestartGame: cleaning up {dungeonTops.Count} dungeon pipes");
                for (int i = dungeonTops.Count - 1; i >= 0; i--)
                {
                    var t = dungeonTops[i];
                    var b = (i < dungeonBottoms.Count) ? dungeonBottoms[i] : null;
                    if (t != null)
                    {
                        Debug.WriteLine($" RestartGame removing dungeon top: {t.Name}, left={t.Left}");
                        this.Controls.Remove(t);
                        try { t.Dispose(); } catch { }
                    }
                    if (b != null)
                    {
                        Debug.WriteLine($" RestartGame removing dungeon bottom: {b.Name}, top={b.Top}");
                        this.Controls.Remove(b);
                        try { b.Dispose(); } catch { }
                    }
                }
                dungeonTops.Clear();
                dungeonBottoms.Clear();
            }
            pendingDungeonCleanup = false;
            inDungeon = false;
            dungeonRegionStart = int.MaxValue;
            dungeonRegionEnd = int.MinValue;

            // Rebuild/reset normal pipes so they start fresh and won't reference
            // any previous on-screen positions that could create impossible gaps.
            if (pipeTops == null || pipeBottoms == null || pipeTops.Count != pipePairs)
            {
                RebuildPipes(pipePairs);
            }
            else
            {
                for (int i = 0; i < pipeTops.Count; i++)
                {
                    int posX = pipeStartX + i * pipeSpacing;
                    var top = pipeTops[i];
                    var bottom = pipeBottoms[i];
                    top.Left = posX;
                    bottom.Left = posX;
                    int topH = rnd.Next(40, Math.Max(60, this.ClientSize.Height - pipeGap - 80));
                    top.Height = topH;
                    bottom.Top = topH + pipeGap;
                    bottom.Height = this.ClientSize.Height - bottom.Top;
                }
            }

            // Reset bird and physics
            bird.Location = new Point(56, 200);
            verticalSpeed = 0;

            // Reset runtime speed and dungeon trigger timer so dungeons don't
            // immediately spawn at restart
            currentPipeSpeed = basePipeSpeed;
            lastDungeonTrigger = DateTime.Now;

            gameOverText.Visible = false;

            // Start the game loop
            try { if (gameTimer != null) gameTimer.Start(); } catch { }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            // Only handle specific keys explicitly. Do not treat other keys as pause.
            if (e.KeyCode == Keys.P && e.Modifiers == Keys.None)
            {
                // toggle pause via keyboard
                PauseButton_Click(pauseButton, EventArgs.Empty);
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Space && !isGameOver)
            {
                // give the bird an upward impulse
                verticalSpeed = -12;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.R && isGameOver)
            {
                RestartGame();
                e.SuppressKeyPress = true;
                return;
            }
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            // KeyUp handler (currently unused but required by designer)
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            // Toggle pause/resume by stopping/starting the game timer
            try
            {
                if (gameTimer == null) return;

                if (gameTimer.Enabled)
                {
                    gameTimer.Stop();
                    pauseButton.Text = "Resume";
                    if (pauseIndicator != null) pauseIndicator.Visible = true;
                    Debug.WriteLine($"Game paused at {DateTime.Now:HH:mm:ss.fff}");
                }
                else
                {
                    // do not resume if game is over
                    if (isGameOver)
                        return;

                    gameTimer.Start();
                    pauseButton.Text = "Pause";
                    if (pauseIndicator != null) pauseIndicator.Visible = false;
                    Debug.WriteLine($"Game resumed at {DateTime.Now:HH:mm:ss.fff}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PauseButton_Click error: {ex}");
            }
        }
    }
}