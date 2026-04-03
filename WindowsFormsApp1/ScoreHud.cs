using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    /// <summary>
    /// Lightweight owner-drawn control that renders score and best-score text
    /// with a drop-shadow.  Uses WS_EX_TRANSPARENT so the OS paints sibling
    /// controls (pipes, bird, background) first, then this control paints
    /// only the text on top.  No double-buffering on this control (the parent
    /// form's WS_EX_COMPOSITED handles flicker-free compositing).
    /// </summary>
    internal sealed class ScoreHud : Control
    {
        private Font scoreFont;
        private Font bestFont;
        private string scoreStr = "";
        private string bestStr = "";

        public ScoreHud()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint, true);
            // Explicitly disable double-buffer – it conflicts with
            // WS_EX_TRANSPARENT + parent WS_EX_COMPOSITED.
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                return cp;
            }
        }

        public Font ScoreFont
        {
            get => scoreFont;
            set { scoreFont = value; Invalidate(); }
        }

        public Font BestFont
        {
            get => bestFont;
            set { bestFont = value; Invalidate(); }
        }

        public string ScoreStr
        {
            get => scoreStr;
            set
            {
                if (scoreStr == value) return;
                scoreStr = value;
                Invalidate();
            }
        }

        public string BestStr
        {
            get => bestStr;
            set
            {
                if (bestStr == value) return;
                bestStr = value;
                Invalidate();
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Skip background painting entirely — the parent form's
            // WS_EX_COMPOSITED compositing handles the background.
            // Calling base would invoke the standard transparent-BackColor
            // logic which paints the parent's BackgroundImage region,
            // making pipes behind this control invisible. By doing nothing
            // the composited buffer already contains the correct pixels.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (scoreFont == null) return;

            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.SmoothingMode = SmoothingMode.HighQuality;

            float sx = 13;
            float sy = 6;

            // Score text with drop shadow
            using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                g.DrawString(scoreStr, scoreFont, shadow, sx + 1, sy + 1);
            using (var brush = new SolidBrush(Color.White))
                g.DrawString(scoreStr, scoreFont, brush, sx, sy);

            if (bestFont != null && !string.IsNullOrEmpty(bestStr))
            {
                var scoreSz = g.MeasureString(scoreStr, scoreFont);
                float bx = sx + scoreSz.Width + 20;
                float by = sy + 5;

                using (var shadow = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                    g.DrawString(bestStr, bestFont, shadow, bx + 1, by + 1);
                using (var brush = new SolidBrush(Color.Gold))
                    g.DrawString(bestStr, bestFont, brush, bx, by);
            }
        }
    }
}
