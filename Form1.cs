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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;

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
        // Bird hovers until the player's first Space press
        private bool waitingForFirstInput = true;

        /// <summary>The score achieved when the game ended.</summary>
        public int FinalScore { get; private set; }

        // high score tracking (passed in from main menu)
        private int highScore = 0;
        /// <summary>The highest score across sessions (updated live).</summary>
        public int HighScore => highScore;

        // Owner-drawn score HUD control that renders on top of pipes
        private ScoreHud scoreHud;

        // Game over image loaded once and painted directly onto the overlay bitmap
        private Image gameOverImg;
        // multiple pipe pairs
        private List<PictureBox> pipeTops;
        private List<PictureBox> pipeBottoms;
        // Tracks which normal pipe pairs the bird has already scored through
        private HashSet<PictureBox> scoredPipes = new HashSet<PictureBox>();
        // Tracks which dungeon pipe pairs the bird has already scored through
        private HashSet<PictureBox> scoredDungeonPipes = new HashSet<PictureBox>();
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
        private ModernButton pauseButton;
        // on-screen pause indicator
        private System.Windows.Forms.Label pauseIndicator;
        // smooth speed ramping
        private double currentPipeSpeed;
        // dungeon mode (clustered pipes) timing
        private bool inDungeon = false;
        private DateTime lastDungeonTrigger = DateTime.MinValue;
        private DateTime dungeonEndTime = DateTime.MinValue;
        private TimeSpan dungeonInterval = TimeSpan.FromSeconds(6);
        private TimeSpan dungeonDuration = TimeSpan.FromSeconds(6);
        private int dungeonSpacing = 120; // tighter spacing during dungeon
        private int originalPipeSpacing;
        // when true we wait to remove dungeon pipes until the player has crossed the last one
        private bool pendingDungeonCleanup = false;
        private int dungeonRegionStart = int.MaxValue;
        private int dungeonRegionEnd = int.MinValue;

        // resolution scaling
        private int resWidth = 800;
        private int resHeight = 450;
        private double scale = 1.0;
        private int flapImpulse = -12;
        private int maxFallSpeed = 10;

        // Sound effect file paths
        private string sfxFlap;
        private string sfxScore;
        private string sfxFail;

        // Background-thread sound engine: a hidden Form on its own thread
        // provides the Windows message pump that MCI requires.
        private Form soundForm;
        private volatile bool soundReady;
        // Cooldown to skip redundant rapid flap sounds (held Space)
        private long lastFlapTicks;

        // High-resolution game loop (~60 Hz) replacing WinForms Timer (~50 Hz max)
        private const double TargetFrameMs = 1000.0 / 60.0;   // ~16.67ms per frame
        private const double BaseFrameMs = 33.0;               // original design tick rate used to keep physics speed consistent
        private Stopwatch gameLoopWatch;
        private bool gameLoopRunning;
        private double frameDt = 1.0; // delta-time ratio: elapsed / BaseFrameMs
        // Floating-point accumulators for sub-pixel precision
        private double birdY;
        private double verticalSpeedF;
        private double pipeMoveFrac;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMsg
        {
            public IntPtr hWnd;
            public uint msg;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public System.Drawing.Point pt;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PeekMessage(out NativeMsg lpMsg, IntPtr hWnd,
            uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, bool wParam, int lParam);
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uMilliseconds);
        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr callback);

        public Form1()
        {
            InitializeComponent();
            InitSounds();
        }

        public Form1(int pipeGapSetting, int baseSpeedSetting, int dungeonIntervalSetting, int currentHighScore = 0, int resolutionWidth = 800, int resolutionHeight = 450)
        {
            InitializeComponent();
            InitSounds();
            pipeGap = pipeGapSetting;
            basePipeSpeed = baseSpeedSetting;
            dungeonInterval = TimeSpan.FromSeconds(dungeonIntervalSetting);
            highScore = currentHighScore;
            resWidth = resolutionWidth;
            resHeight = resolutionHeight;

            // Apply resolution immediately after InitializeComponent so AutoScaleMode
            // does not override our ClientSize during the Load event.
            this.AutoScaleMode = AutoScaleMode.None;
            this.ClientSize = new Size(resWidth, resHeight);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
        }

        /// <summary>
        /// Clips the bird PictureBox to a Region containing only the opaque
        /// pixels of its image. This eliminates the rectangular transparent
        /// background that WinForms would otherwise fill with the parent's
        /// background, causing visible artifacts when overlapping pipes.
        /// </summary>
        private void ApplyBirdRegion()
        {
            if (bird.Image == null || bird.Width == 0 || bird.Height == 0) return;

            // Render the image at the PictureBox display size
            var bmp = new Bitmap(bird.Width, bird.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.DrawImage(bird.Image, 0, 0, bird.Width, bird.Height);
            }

            // Build a region from horizontal runs of opaque pixels (fast for small bitmaps)
            var rgn = new Region();
            rgn.MakeEmpty();
            for (int y = 0; y < bmp.Height; y++)
            {
                int? runStart = null;
                for (int x = 0; x < bmp.Width; x++)
                {
                    if (bmp.GetPixel(x, y).A > 0)
                    {
                        if (runStart == null) runStart = x;
                    }
                    else
                    {
                        if (runStart != null)
                        {
                            rgn.Union(new Rectangle(runStart.Value, y, x - runStart.Value, 1));
                            runStart = null;
                        }
                    }
                }
                if (runStart != null)
                    rgn.Union(new Rectangle(runStart.Value, y, bmp.Width - runStart.Value, 1));
            }

            bird.Region = rgn;
            bmp.Dispose();
        }

        // Enable WS_EX_COMPOSITED to double-buffer all child controls and eliminate
        // flicker/stuttering when many PictureBoxes move simultaneously.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        private void ApplyPipeAppearance()
        {
            if (pipeTops == null) return;

            bool useCustomPaint = (pipeImgLoaded != null && pipeImgTopRotated != null);

            for (int i = 0; i < pipeTops.Count; i++)
            {
                var top = pipeTops[i];
                var bottom = pipeBottoms[i];
                SetupPipePaint(top, isTopPipe: true, useCustomPaint);
                SetupPipePaint(bottom, isTopPipe: false, useCustomPaint);
            }
        }

        private void SetupPipePaint(PictureBox pipe, bool isTopPipe, bool useCustomPaint)
        {
            // Remove any previous Paint handler to avoid stacking
            pipe.Paint -= PipeTop_Paint;
            pipe.Paint -= PipeBottom_Paint;
            pipe.Image = null;

            if (useCustomPaint)
            {
                pipe.BackColor = Color.Transparent;
                if (isTopPipe)
                    pipe.Paint += PipeTop_Paint;
                else
                    pipe.Paint += PipeBottom_Paint;
            }
            else
            {
                pipe.BackColor = pipeColor;
            }
        }

        private void PipeTop_Paint(object sender, PaintEventArgs e)
        {
            // Top pipe: cap at bottom (facing the gap), body extending upward.
            // Uses pipeImgTopRotated (180° flipped) so the cap is at the image bottom.
            if (pipeImgTopRotated == null) return;
            var pb = (PictureBox)sender;
            var g = e.Graphics;
            int w = pb.Width;
            int h = pb.Height;

            int imgW = pipeImgTopRotated.Width;
            int imgH = pipeImgTopRotated.Height;
            // Scale the image height to match the PictureBox width (preserve aspect ratio)
            int scaledH = (int)((double)imgH / imgW * w);

            if (h <= scaledH)
            {
                // PictureBox shorter than scaled image: draw the bottom portion
                // (which contains the cap) so the cap is always fully visible.
                int srcY = imgH - (int)((double)h / scaledH * imgH);
                g.DrawImage(pipeImgTopRotated, new Rectangle(0, 0, w, h),
                            new Rectangle(0, srcY, imgW, imgH - srcY), GraphicsUnit.Pixel);
            }
            else
            {
                // PictureBox taller: draw full image at the bottom, extend body upward
                int yOffset = h - scaledH;
                // Stretch the top few rows of the flipped image (body) to fill above
                if (yOffset > 0)
                {
                    g.DrawImage(pipeImgTopRotated, new Rectangle(0, 0, w, yOffset),
                                new Rectangle(0, 0, imgW, 2), GraphicsUnit.Pixel);
                }
                g.DrawImage(pipeImgTopRotated, new Rectangle(0, yOffset, w, scaledH));
            }
        }

        private void PipeBottom_Paint(object sender, PaintEventArgs e)
        {
            // Bottom pipe: cap at top (facing the gap), body extending downward.
            // Uses pipeImgLoaded (original) so the cap is at the image top.
            if (pipeImgLoaded == null) return;
            var pb = (PictureBox)sender;
            var g = e.Graphics;
            int w = pb.Width;
            int h = pb.Height;

            int imgW = pipeImgLoaded.Width;
            int imgH = pipeImgLoaded.Height;
            // Scale the image height to match the PictureBox width (preserve aspect ratio)
            int scaledH = (int)((double)imgH / imgW * w);

            if (h <= scaledH)
            {
                // PictureBox shorter than scaled image: draw from the top
                // (cap and as much body as fits).
                int srcH = (int)((double)h / scaledH * imgH);
                g.DrawImage(pipeImgLoaded, new Rectangle(0, 0, w, h),
                            new Rectangle(0, 0, imgW, srcH), GraphicsUnit.Pixel);
            }
            else
            {
                // PictureBox taller: draw full image at the top, extend body downward
                g.DrawImage(pipeImgLoaded, new Rectangle(0, 0, w, scaledH));
                // Stretch the bottom few rows of the image (body) to fill below
                int remaining = h - scaledH;
                if (remaining > 0)
                {
                    g.DrawImage(pipeImgLoaded, new Rectangle(0, scaledH, w, remaining),
                                new Rectangle(0, imgH - 2, imgW, 2), GraphicsUnit.Pixel);
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
                    pipeTop.Top = -20;
                    pipeTop.Height = topH + 20;
                    pipeBottom.Top = topH + pipeGap;
                    pipeBottom.Height = this.ClientSize.Height - pipeBottom.Top + 20;
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
                    top.Top = -20;
                    top.Height = topH + 20;
                    bottom.Top = topH + pipeGap;
                    bottom.Height = this.ClientSize.Height - bottom.Top + 20;
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
        }

        private void StartDungeon()
        {
            // don't start a new dungeon if one is already active, or if there are
            // existing dungeon pipes that haven't been cleaned up yet. This avoids
            // overlapping multiple dungeon clusters which caused the large region
            // and visual glitches.
            if (inDungeon) return;
            if (pendingDungeonCleanup) {
                return;
            }
            if (dungeonTops != null && dungeonTops.Count > 0)
            {
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

            // ensure the effective gap is at least big enough for the bird to pass
            // account for bird size and any pipe image caps so the visual gap matches collision
            int imageCapMargin = (pipeImgLoaded != null) ? 14 : 8;
            int minGapForBird = (bird != null) ? bird.Height + 20 + imageCapMargin : 40;
            int effectiveGap = Math.Max(pipeGap, minGapForBird);

            // reduce density and spawn further right to avoid overlapping existing pipes
            int spawnCount = Math.Max(1, (int)Math.Ceiling(pipePairs * 0.6));
            int spawnStart = rightmost + minDungeonSpacing * 4;

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
                    prevDungeonGapCenter = (prevDT.Bottom + prevDB.Top) / 2.0;
                }
                else
                {
                    // align first dungeon pipe with nearest normal pipe
                    var nearestNormal = pipeTops.OrderBy(p => Math.Abs(p.Left - newLeft)).FirstOrDefault();
                    if (nearestNormal != null)
                    {
                        int ni = pipeTops.IndexOf(nearestNormal);
                        if (ni >= 0 && ni < pipeBottoms.Count)
                            prevDungeonGapCenter = (nearestNormal.Bottom + pipeBottoms[ni].Top) / 2.0;
                    }
                }

                // pick a random gap center within the allowed shift range
                int gapCenterMin = (int)Math.Max(minTopH + effectiveGap / 2, prevDungeonGapCenter - maxDungeonShift);
                int gapCenterMax = (int)Math.Min(this.ClientSize.Height - 40 - effectiveGap / 2, prevDungeonGapCenter + maxDungeonShift);
                if (gapCenterMin > gapCenterMax) gapCenterMin = gapCenterMax = mid;
                int gapCenter = rnd.Next(gapCenterMin, gapCenterMax + 1);
                topH = gapCenter - effectiveGap / 2;
                topH = Math.Max(minTopH, Math.Min(topH, maxTopH));

                top.Top = -20;
                top.Height = topH + 20;
                bottom.Top = topH + effectiveGap;
                bottom.Height = this.ClientSize.Height - bottom.Top + 20;

                top.Name = "dungeonTop" + i + "_" + Guid.NewGuid().ToString("N");
                bottom.Name = "dungeonBottom" + i + "_" + Guid.NewGuid().ToString("N");

                this.Controls.Add(top);
                this.Controls.Add(bottom);
                top.SendToBack();
                bottom.SendToBack();

                // track dungeon pipes separately so they don't interfere with normal pipe respawn logic
                dungeonTops.Add(top);
                dungeonBottoms.Add(bottom);
            }
            // Apply appearance (images/colors) to the newly created pipes
            ApplyPipeAppearance();

            // record the horizontal region occupied by the dungeon pipes so normal
            // pipes can avoid spawning into this region and causing visual overlap
            if (dungeonTops != null && dungeonTops.Count > 0)
            {
                dungeonRegionStart = dungeonTops.Min(p => p.Left);
                dungeonRegionEnd = dungeonTops.Max(p => p.Left + p.Width);
            }

            // Ensure bird and HUD remain visible above pipes
            if (bird != null) bird.BringToFront();
            if (scoreHud != null) scoreHud.BringToFront();

            // apply appearance to dungeon pipes
            bool useCustomPaint = (pipeImgLoaded != null && pipeImgTopRotated != null);
            if (dungeonTops != null && dungeonTops.Count > 0)
            {
                for (int i = 0; i < dungeonTops.Count; i++)
                {
                    SetupPipePaint(dungeonTops[i], isTopPipe: true, useCustomPaint);
                    SetupPipePaint(dungeonBottoms[i], isTopPipe: false, useCustomPaint);
                }
            }
        }

        private void EndDungeon()
        {
            if (!inDungeon) return;
            inDungeon = false;
            // restore normal spacing
            pipeSpacing = originalPipeSpacing;

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
                        this.Controls.Remove(top);
                        if (bottom != null) this.Controls.Remove(bottom);
                        try { top.Dispose(); } catch { }
                        if (bottom != null) try { bottom.Dispose(); } catch { }
                        dungeonTops.RemoveAt(i);
                        if (bottom != null && i < dungeonBottoms.Count) dungeonBottoms.RemoveAt(i);
                    }
                }
            }

            pendingDungeonCleanup = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // ensure the form receives key events even when child controls have focus
            this.KeyPreview = true;
            IconHelper.SetFormIcon(this);

            // Apply resolution and compute scale factor (base design = 800×450)
            scale = resWidth / 800.0;

            // Scale game constants to match resolution
            pipeWidth = (int)(100 * scale);
            pipeStartX = resWidth;
            pipeSpacing = (int)(320 * scale);
            dungeonSpacing = (int)(120 * scale);
            pipeGap = (int)(pipeGap * scale);
            gravity = Math.Max(1, (int)Math.Round(2 * scale));
            flapImpulse = -(int)Math.Round(12 * scale);
            maxFallSpeed = (int)Math.Round(10 * scale);
            basePipeSpeed = (int)Math.Round(basePipeSpeed * scale);
            // Ensure the bird PictureBox is visible and on top.
            // Reset size/location so it starts visible (higher on screen) and won't immediately fall off.
            bird.Visible = true;
            bird.BackColor = Color.Yellow;
            bird.Size = new Size((int)(40 * scale), (int)(28 * scale));
            // Start the bird higher so gravity doesn't push it off-screen before the user can react.
            bird.Location = new Point((int)(56 * scale), (int)(200 * scale));
            bird.BringToFront();

            // Try loading bird images in priority order; first match wins.
            // If none exist the yellow box remains visible.
            string[] candidateNames = new[] { "flappy-bird.png", "bird.png" };
            foreach (var name in candidateNames)
            {
                string imagePath = AssetPath.Image(name);
                if (!File.Exists(imagePath))
                    continue;

                try
                {
                    var img = Image.FromFile(imagePath);
                    bird.Image = img;
                    bird.SizeMode = PictureBoxSizeMode.StretchImage;
                    // Use a fixed display size for the bird so large source images don't
                    // make the PictureBox huge and cause immediate collisions.
                    bird.Size = new Size((int)(45 * scale), (int)(32 * scale));
                    bird.BackColor = Color.Transparent;
                    ApplyBirdRegion();
                    bird.BringToFront();
                    break; // loaded an image, stop searching
                }
                catch
                {
                    // ignore load errors and try next candidate
                }
            }

            // Load game over image (painted directly onto the overlay bitmap, not as a child control)
            string gameOverPath = AssetPath.Image("Game-Over.png");
            if (File.Exists(gameOverPath))
            {
                try { gameOverImg = Image.FromFile(gameOverPath); } catch { }
            }

            // Overlay panel used during game over – shows a snapshot of the game
            // state with a light dark tint so the player can see where the bird
            // collided while keeping the Game Over UI readable.
            // The Game Over image and restart text are painted directly onto the
            // bitmap to avoid WinForms child-control transparency artifacts.
            gameOverOverlay = new Panel();
            gameOverOverlay.BackColor = Color.Transparent;
            gameOverOverlay.Visible = false;
            this.Controls.Add(gameOverOverlay);

            // Hide the designer Label control – score/best are now rendered by
            // the owner-drawn ScoreHud control which has a truly transparent
            // background and sits on top of pipes in the Z-order.
            scoreText.Visible = false;

            // Create the owner-drawn score HUD
            scoreHud = new ScoreHud();
            scoreHud.ScoreFont = new Font("Microsoft Sans Serif", (float)(20.25 * scale), FontStyle.Regular);
            scoreHud.BestFont = new Font("Microsoft Sans Serif", (float)(14 * scale), FontStyle.Bold);
            scoreHud.ScoreStr = "Score: 0";
            scoreHud.BestStr = "Best: " + highScore;
            scoreHud.Location = Point.Empty;
            scoreHud.Size = new Size(this.ClientSize.Width, (int)(50 * scale));
            this.Controls.Add(scoreHud);
            scoreHud.BringToFront();

            // create an on-screen pause indicator for easy screenshots
            pauseIndicator = new Label();
            pauseIndicator.Size = new Size((int)(220 * scale), (int)(80 * scale));
            pauseIndicator.Font = new Font("Microsoft Sans Serif", (float)(20 * scale), FontStyle.Bold, GraphicsUnit.Point);
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
                    pipeTop.Top = -20;
                    pipeTop.Height = topH + 20;
                    pipeBottom.Top = topH + pipeGap;
                    pipeBottom.Height = this.ClientSize.Height - pipeBottom.Top + 20;

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
                    top.Top = -20;
                    top.Height = topH + 20;
                    bottom.Top = topH + pipeGap;
                    bottom.Height = this.ClientSize.Height - bottom.Top + 20;

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
            string pipePath = AssetPath.Image("pipe.png");
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

            // store loaded pipe image and create a flipped copy for top pipes
            if (pipeImg != null)
            {
                pipeImgLoaded = pipeImg;
                try
                {
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
            string bgPath = AssetPath.Image("background.png");
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

            // Disable the low-resolution WinForms timer; use Application.Idle loop
            gameTimer.Enabled = false;
            gameTimer.Stop();

            // Initialize floating-point accumulators
            birdY = bird.Top;
            verticalSpeedF = 0;
            pipeMoveFrac = 0;

            // Request 1ms timer resolution so Thread.Sleep(1) is accurate
            timeBeginPeriod(1);

            // Start the high-resolution game loop
            gameLoopWatch = Stopwatch.StartNew();
            gameLoopRunning = true;
            Application.Idle += GameLoop_Idle;
        }

        private void bird_Click(object sender, EventArgs e)
        {

        }

        private void GameLoop_Idle(object sender, EventArgs e)
        {
            NativeMsg msg;
            while (!PeekMessage(out msg, IntPtr.Zero, 0, 0, 0))
            {
                if (!gameLoopRunning || isGameOver)
                    return;

                double elapsedMs = gameLoopWatch.Elapsed.TotalMilliseconds;
                if (elapsedMs >= TargetFrameMs)
                {
                    gameLoopWatch.Restart();
                    frameDt = Math.Min(elapsedMs / BaseFrameMs, 2.0);
                    gameTimer_Tick(this, EventArgs.Empty);
                }
                else
                {
                    // Yield CPU briefly while waiting for the next frame
                    System.Threading.Thread.Sleep(1);
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            gameLoopRunning = false;
            Application.Idle -= GameLoop_Idle;
            timeEndPeriod(1);
            DisposeSounds();
            base.OnFormClosed(e);
        }

        private void gameTimer_Tick(object sender, EventArgs e)
        {
            // compute target pipe speed based on score
            double targetSpeed = basePipeSpeed + (score / 5.0);
            targetSpeed = Math.Min(targetSpeed, basePipeSpeed + 12);
            // smooth ramping (lerp) - scale factor by delta time
            currentPipeSpeed = Lerp(currentPipeSpeed, targetSpeed, 0.02 * frameDt);
            double useSpeed = currentPipeSpeed;
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

            gameTickInner(useSpeed);
        }

        private double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private void gameTickInner(double pipeSpeed)
        {
            if (isGameOver)
                return;

            // if pipe lists are empty, nothing to do
            if (pipeTops == null || pipeTops.Count == 0) return;

            // While waiting for first input, keep rendering but don't run physics
            if (waitingForFirstInput)
                return;

            this.SuspendLayout();

            // apply gravity with delta-time scaling and move bird using float accumulator
            verticalSpeedF += gravity * frameDt;
            // cap vertical speed to avoid excessive fall velocity
            verticalSpeedF = Math.Min(verticalSpeedF, maxFallSpeed);
            birdY += verticalSpeedF * frameDt;
            bird.Top = (int)Math.Round(birdY);

            // Compute integer pipe pixels to move this tick via sub-pixel accumulation
            pipeMoveFrac += pipeSpeed * frameDt;
            int pipeSpeed1 = (int)pipeMoveFrac;
            pipeMoveFrac -= pipeSpeed1;

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

                top.Left -= pipeSpeed1;
                bottom.Left -= pipeSpeed1;

                if (top.Left < -pipeWidth)
                {
                    // respawn behavior
                    // propose new position after the respawn cursor
                    int proposedLeft = respawnCursor + pipeSpacing;

                    // enforce a minimum horizontal spacing between spawned normal pipes
                    int minNormalSpacing = pipeSpacing;
                    if (bird != null)
                    {
                        try { minNormalSpacing = Math.Max(minNormalSpacing, bird.Width * 3); } catch { }
                    }

                    // if there are dungeon pipes present, ensure the proposedLeft does
                    // not overlap any dungeon pipe. Prefer placing before the first
                    // overlapping dungeon pipe if there's space; otherwise push the
                    // normal pipe to the right of the last overlapping dungeon pipe.
                    int buffer = 12; // extra spacing to avoid cap overlap
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
                            if (!changed) break;
                        }
                    }

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

                    // advance the cursor to avoid placing subsequent respawns at the same spot
                    respawnCursor = proposedLeft;
                    top.Left = proposedLeft;
                    bottom.Left = proposedLeft;

                    int maxTopLimit = Math.Max(41, this.ClientSize.Height - pipeGap - 80);
                    int topHNormal;

                    // Find the nearest pipe (normal OR dungeon) that the bird
                    // will encounter just before this respawned pipe. Constrain
                    // this pipe's gap center relative to that predecessor so we
                    // never create an impossible vertical jump.
                    int maxShift = Math.Max(50, pipeGap / 2);
                    double prevGapCenter = this.ClientSize.Height / 2.0;

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
                            prevGapCenter = (bestPrev.Bottom + pipeBottoms[bpIdx].Top) / 2.0;
                        }
                        else if (dungeonTops != null)
                        {
                            int dIdx = dungeonTops.IndexOf(bestPrev);
                            if (dIdx >= 0 && dIdx < dungeonBottoms.Count)
                            {
                                prevGapCenter = (bestPrev.Bottom + dungeonBottoms[dIdx].Top) / 2.0;
                            }
                        }
                    }

                    // pick a random gap center within the allowed shift range
                    int gcMin = (int)Math.Max(30 + pipeGap / 2, prevGapCenter - maxShift);
                    int gcMax = (int)Math.Min(this.ClientSize.Height - 40 - pipeGap / 2, prevGapCenter + maxShift);
                    if (gcMin > gcMax) { gcMin = gcMax = this.ClientSize.Height / 2; }
                    int newGapCenter = rnd.Next(gcMin, gcMax + 1);
                    topHNormal = Math.Max(30, Math.Min(newGapCenter - pipeGap / 2, maxTopLimit));

                    top.Top = -20;
                    top.Height = topHNormal + 20;
                    bottom.Top = topHNormal + pipeGap;
                    // Extend bottom pipe past the form edge to hide stretched image cap artifact
                    bottom.Height = this.ClientSize.Height - bottom.Top + 20;

                    // Pipe respawned — allow it to be scored again
                    scoredPipes.Remove(top);
                }
            }

            // move dungeon pipes (separate from normal pipes)
            if (dungeonTops != null)
            {
                for (int i = 0; i < dungeonTops.Count; i++)
                {
                    var top = dungeonTops[i];
                    var bottom = dungeonBottoms[i];
                    top.Left -= pipeSpeed1;
                    bottom.Left -= pipeSpeed1;

                    // if a dungeon pipe moves off-screen, remove it (cleanup early)
                    if (top.Left < -pipeWidth)
                    {
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
                    if (rightmostDungeonEdge < bird.Left)
                    {
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
                        scoredDungeonPipes.Clear();
                        pendingDungeonCleanup = false;
                            // reset recorded dungeon region
                            dungeonRegionStart = int.MaxValue;
                            dungeonRegionEnd = int.MinValue;
                    }
                }
            }

            // update high score live during gameplay
            if (score > highScore)
                highScore = score;
            if (scoreHud != null)
            {
                scoreHud.ScoreStr = "Score: " + score;
                scoreHud.BestStr = "Best: " + highScore;
            }

            // Score when the bird's center crosses the center of each pipe pair
            int birdCenterX = bird.Left + bird.Width / 2;
            for (int i = 0; i < pipeTops.Count; i++)
            {
                var top = pipeTops[i];
                if (scoredPipes.Contains(top)) continue;
                int pipeCenterX = top.Left + top.Width / 2;
                if (birdCenterX >= pipeCenterX)
                {
                    score++;
                    scoredPipes.Add(top);
                    PlaySound("score");
                }
            }

            // Score dungeon pipes the same way
            if (dungeonTops != null)
            {
                for (int i = 0; i < dungeonTops.Count; i++)
                {
                    var top = dungeonTops[i];
                    if (scoredDungeonPipes.Contains(top)) continue;
                    int pipeCenterX = top.Left + top.Width / 2;
                    if (birdCenterX >= pipeCenterX)
                    {
                        score++;
                        scoredDungeonPipes.Add(top);
                        PlaySound("score");
                    }
                }
            }

            // Collision detection using shrunken hitboxes to match visible sprites
            var birdRect = bird.Bounds;
            birdRect.Inflate((int)(-8 * scale), (int)(-6 * scale));

            // check each normal pipe pair
            for (int i = 0; i < pipeTops.Count; i++)
            {
                var top = pipeTops[i];
                var bottom = pipeBottoms[i];

                var topRect = top.Bounds;
                var bottomRect = bottom.Bounds;
                topRect.Inflate((int)(-6 * scale), (int)(-4 * scale));
                bottomRect.Inflate((int)(-6 * scale), (int)(-4 * scale));

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

                    var topRect2 = top.Bounds;
                    var bottomRect2 = bottom.Bounds;
                    topRect2.Inflate((int)(-6 * scale), (int)(-4 * scale));
                    bottomRect2.Inflate((int)(-6 * scale), (int)(-4 * scale));

                    if (birdRect.IntersectsWith(topRect2) || birdRect.IntersectsWith(bottomRect2))
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

            this.ResumeLayout(false);
            this.Invalidate(true);
            this.Update();
        }

        // button to return to main menu on game over
        private ModernButton btnMainMenu;
        private Panel gameOverOverlay;

        private void GameOver()
        {
            isGameOver = true;
            FinalScore = score;
            gameLoopRunning = false;
            PlaySound("fail");

            // Capture the current game state so the player can see where
            // the bird collided, then paint the Game Over image and restart
            // text directly onto the bitmap. This avoids WinForms child-
            // control transparency artifacts (opaque rectangular backgrounds).
            gameOverOverlay.Size = this.ClientSize;
            gameOverOverlay.Location = Point.Empty;
            int centerY = this.ClientSize.Height / 2;
            int textBottomY = centerY + (int)(5 * scale) + (int)(60 * scale);
            try
            {
                // Hide the HUD control so DrawToBitmap captures a clean scene;
                // we paint score/best directly onto the bitmap below.
                if (scoreHud != null) scoreHud.Visible = false;

                var snap = new Bitmap(this.ClientSize.Width, this.ClientSize.Height);
                this.DrawToBitmap(snap, new Rectangle(Point.Empty, this.ClientSize));

                using (var g = Graphics.FromImage(snap))
                {
                    // Dark tint for contrast
                    using (var brush = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
                        g.FillRectangle(brush, 0, 0, snap.Width, snap.Height);

                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    // Paint score and high score cleanly onto the bitmap
                    Font sFont = scoreHud?.ScoreFont;
                    Font bFont = scoreHud?.BestFont;
                    string sStr = scoreHud?.ScoreStr ?? ("Score: " + score);
                    string bStr = scoreHud?.BestStr ?? ("Best: " + highScore);
                    if (sFont != null)
                    {
                        float sx = 13 * (float)scale;
                        float sy = 10 * (float)scale;
                        using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                            g.DrawString(sStr, sFont, shadow, sx + 1, sy + 1);
                        using (var sBrush = new SolidBrush(Color.White))
                            g.DrawString(sStr, sFont, sBrush, sx, sy);

                        var scoreSz = g.MeasureString(sStr, sFont);
                        float bx = sx + scoreSz.Width + 20;
                        float by = sy + 5;
                        if (bFont != null)
                        {
                            using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                                g.DrawString(bStr, bFont, shadow, bx + 1, by + 1);
                            using (var hsBrush = new SolidBrush(Color.Gold))
                                g.DrawString(bStr, bFont, hsBrush, bx, by);
                        }
                    }

                    // Paint Game Over image
                    if (gameOverImg != null)
                    {
                        int imgW = (int)(300 * scale);
                        int imgH = (int)(120 * scale);
                        int imgX = (this.ClientSize.Width - imgW) / 2;
                        int imgY = centerY - imgH - (int)(5 * scale);
                        g.DrawImage(gameOverImg, imgX, imgY, imgW, imgH);
                    }

                    // Paint "Press R to restart" text
                    using (var font = new Font("Microsoft Sans Serif", (float)(18 * scale), FontStyle.Bold))
                    using (var textBrush = new SolidBrush(Color.Red))
                    {
                        var textSize = g.MeasureString("Press R to restart", font);
                        float textX = (this.ClientSize.Width - textSize.Width) / 2f;
                        float textY = centerY + (int)(5 * scale);
                        g.DrawString("Press R to restart", font, textBrush, textX, textY);
                        textBottomY = (int)(textY + textSize.Height);
                    }
                }
                var old = gameOverOverlay.BackgroundImage;
                gameOverOverlay.BackgroundImage = snap;
                gameOverOverlay.BackgroundImageLayout = ImageLayout.None;
                old?.Dispose();
            }
            catch { }
            gameOverOverlay.Visible = true;
            gameOverOverlay.BringToFront();

            // show main menu button (only interactive control needed on overlay)
            if (btnMainMenu == null)
            {
                btnMainMenu = new ModernButton();
                btnMainMenu.Text = "Main Menu";
                btnMainMenu.Font = new Font("Segoe UI", (float)(11 * scale), FontStyle.Bold);
                btnMainMenu.Size = new Size((int)(160 * scale), (int)(42 * scale));
                btnMainMenu.Click += (s, args) => { this.Close(); };
                gameOverOverlay.Controls.Add(btnMainMenu);
            }
            btnMainMenu.Location = new Point(
                (this.ClientSize.Width - btnMainMenu.Width) / 2,
                textBottomY + 10);
            btnMainMenu.Visible = true;
            btnMainMenu.BringToFront();
        }

        private void RestartGame()
        {
            // Reset core state
            isGameOver = false;
            score = 0;
            scoredPipes.Clear();
            if (scoreHud != null) scoreHud.ScoreStr = "Score: 0";

            // Restore score HUD visibility (hidden during game over snapshot)
            if (scoreHud != null) scoreHud.Visible = true;

            // hide main menu button if visible
            if (btnMainMenu != null) btnMainMenu.Visible = false;
            if (gameOverOverlay != null)
            {
                gameOverOverlay.Visible = false;
                var oldBg = gameOverOverlay.BackgroundImage;
                gameOverOverlay.BackgroundImage = null;
                oldBg?.Dispose();
            }

            // Stop timer while we reconstruct scene to avoid race with game loop
            gameLoopRunning = false;

            // Ensure pipe spacing restored to original value before rebuilding
            pipeSpacing = originalPipeSpacing;

            // Remove and dispose any leftover dungeon pipes that may have been left
            // around when the player died. This prevents stale dungeon pipes from
            // influencing normal pipe respawn logic after restart.
            if (dungeonTops != null)
            {
                for (int i = dungeonTops.Count - 1; i >= 0; i--)
                {
                    var t = dungeonTops[i];
                    var b = (i < dungeonBottoms.Count) ? dungeonBottoms[i] : null;
                    if (t != null)
                    {
                        this.Controls.Remove(t);
                        try { t.Dispose(); } catch { }
                    }
                    if (b != null)
                    {
                        this.Controls.Remove(b);
                        try { b.Dispose(); } catch { }
                    }
                }
                dungeonTops.Clear();
                dungeonBottoms.Clear();
            }
            scoredDungeonPipes.Clear();
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
                    top.Top = -20;
                    top.Height = topH + 20;
                    bottom.Top = topH + pipeGap;
                    bottom.Height = this.ClientSize.Height - bottom.Top + 20;
                }
            }

            // Reset bird and physics
            bird.Location = new Point((int)(56 * scale), (int)(200 * scale));
            birdY = bird.Top;
            verticalSpeedF = 0;
            pipeMoveFrac = 0;
            waitingForFirstInput = true;

            // Reset runtime speed and dungeon trigger timer so dungeons don't
            // immediately spawn at restart
            currentPipeSpeed = basePipeSpeed;
            lastDungeonTrigger = DateTime.Now;

            // Start the high-res game loop
            gameLoopWatch.Restart();
            gameLoopRunning = true;
        }

        // ---- Sound helpers (background-thread MCI with message pump) ----

        private void InitSounds()
        {
            sfxFlap = AssetPath.Sound("Bird-fly.mp3");
            sfxScore = AssetPath.Sound("Point-pipe.mp3");
            sfxFail = AssetPath.Sound("Bird-fail.mp3");

            var ready = new ManualResetEventSlim(false);
            var t = new Thread(() =>
            {
                // Create a hidden form to get a message pump on this thread
                soundForm = new Form
                {
                    ShowInTaskbar = false,
                    FormBorderStyle = FormBorderStyle.None,
                    Size = new Size(1, 1),
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                    Opacity = 0
                };
                soundForm.Load += (s, e) =>
                {
                    // Open MCI aliases on this thread (MCI is thread-affine)
                    if (File.Exists(sfxFlap))
                        mciSendString($"open \"{sfxFlap}\" type mpegvideo alias flap", null, 0, IntPtr.Zero);
                    if (File.Exists(sfxScore))
                        mciSendString($"open \"{sfxScore}\" type mpegvideo alias score", null, 0, IntPtr.Zero);
                    if (File.Exists(sfxFail))
                        mciSendString($"open \"{sfxFail}\" type mpegvideo alias fail", null, 0, IntPtr.Zero);
                    soundReady = true;
                    ready.Set();
                };
                Application.Run(soundForm); // pumps messages until soundForm is closed
            })
            {
                IsBackground = true,
                Name = "SFX"
            };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            ready.Wait(2000); // wait up to 2s for aliases to open
            ready.Dispose();
        }

        private void PlaySound(string alias)
        {
            if (!soundReady || soundForm == null || soundForm.IsDisposed) return;

            // Flap cooldown: skip if last flap was < 80ms ago to prevent queue flooding
            if (alias == "flap")
            {
                long now = Stopwatch.GetTimestamp();
                long prev = Interlocked.Exchange(ref lastFlapTicks, now);
                double elapsedMs = (now - prev) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs < 80) return;
            }

            // Marshal to the sound thread (non-blocking from UI thread)
            try
            {
                soundForm.BeginInvoke((Action)(() =>
                {
                    mciSendString($"seek {alias} to start", null, 0, IntPtr.Zero);
                    mciSendString($"play {alias}", null, 0, IntPtr.Zero);
                }));
            }
            catch (InvalidOperationException) { } // form disposed
        }

        private void DisposeSounds()
        {
            soundReady = false;
            if (soundForm != null && !soundForm.IsDisposed)
            {
                try
                {
                    soundForm.Invoke((Action)(() =>
                    {
                        mciSendString("close flap", null, 0, IntPtr.Zero);
                        mciSendString("close score", null, 0, IntPtr.Zero);
                        mciSendString("close fail", null, 0, IntPtr.Zero);
                        soundForm.Close();
                    }));
                }
                catch { }
            }
        }

        // ---- Key input ----

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            ProcessGameKeyDown(e);
        }

        /// <summary>
        /// Processes game key input. Called directly by Form1_KeyDown and also
        /// by the parent form when Form1 is embedded as a child control.
        /// </summary>
        public void ProcessGameKeyDown(KeyEventArgs e)
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
                // First Space press starts the game
                if (waitingForFirstInput)
                {
                    waitingForFirstInput = false;
                    gameLoopWatch.Restart(); // reset timer so first frame isn't huge
                }
                // give the bird an upward impulse
                verticalSpeedF = flapImpulse;
                PlaySound("flap");
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
            // Suppress KeyUp for game keys so they don't reach child controls.
            // Without this, ModernButton.OnKeyUp fires OnClick when Space is
            // released while the pause button has focus, causing an auto-pause.
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.P || e.KeyCode == Keys.R)
            {
                e.Handled = true;
            }
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            // Toggle pause/resume by stopping/starting the high-res game loop
            try
            {
                if (gameLoopRunning)
                {
                    gameLoopRunning = false;
                    if (pauseIndicator != null) pauseIndicator.Visible = true;
                    Debug.WriteLine($"Game paused at {DateTime.Now:HH:mm:ss.fff}");
                }
                else
                {
                    // do not resume if game is over
                    if (isGameOver)
                        return;

                    gameLoopWatch.Restart();
                    gameLoopRunning = true;
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