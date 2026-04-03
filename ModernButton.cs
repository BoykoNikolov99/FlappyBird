using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    /// <summary>
    /// A fully owner-drawn rounded button that manually composites the
    /// parent background (form image + ancestor overlays) so rounded
    /// corners blend perfectly – even over semi-transparent panels.
    /// All base-class painting is suppressed via WndProc intercepts.
    /// </summary>
    public class ModernButton : Control
    {
        private int cornerRadius = 18;
        private Color gradientTop = Color.FromArgb(255, 76, 175, 80);
        private Color gradientBottom = Color.FromArgb(255, 46, 125, 50);
        private Color hoverTop = Color.FromArgb(255, 102, 195, 106);
        private Color hoverBottom = Color.FromArgb(255, 56, 142, 60);
        private Color pressedTop = Color.FromArgb(255, 40, 110, 44);
        private Color pressedBottom = Color.FromArgb(255, 27, 94, 32);
        private bool isHovered;
        private bool isPressed;

        // cached background bitmap – regenerated when position / size changes
        private Bitmap bgCache;
        private Point bgCachePos;
        private Size bgCacheSize;

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public int CornerRadius
        {
            get => cornerRadius;
            set { cornerRadius = value; Invalidate(); }
        }

        public Color GradientTop
        {
            get => gradientTop;
            set { gradientTop = value; Invalidate(); }
        }

        public Color GradientBottom
        {
            get => gradientBottom;
            set { gradientBottom = value; Invalidate(); }
        }

        public Color HoverGradientTop
        {
            get => hoverTop;
            set { hoverTop = value; Invalidate(); }
        }

        public Color HoverGradientBottom
        {
            get => hoverBottom;
            set { hoverBottom = value; Invalidate(); }
        }

        public Color PressedGradientTop
        {
            get => pressedTop;
            set { pressedTop = value; Invalidate(); }
        }

        public Color PressedGradientBottom
        {
            get => pressedBottom;
            set { pressedBottom = value; Invalidate(); }
        }

        // ───── helpers ─────

        private GraphicsPath GetRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Finds the nearest Form ancestor (which may be a non-top-level
        /// child form embedded via TopLevel=false).
        /// </summary>
        private Form FindNearestForm()
        {
            Control c = Parent;
            while (c != null)
            {
                if (c is Form f) return f;
                c = c.Parent;
            }
            return null;
        }

        /// <summary>
        /// Builds (and caches) a bitmap of the composited background that
        /// sits behind this control: the form's BackgroundImage region
        /// plus each ancestor's BackColor layered on top.
        /// </summary>
        private Bitmap GetCompositeBackground()
        {
            // Return cache when position & size are unchanged
            Point pos = GetPositionOnForm();
            if (bgCache != null && bgCachePos == pos && bgCacheSize == Size)
                return bgCache;

            bgCachePos = pos;
            bgCacheSize = Size;
            bgCache?.Dispose();
            bgCache = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(bgCache))
            {
                g.CompositingMode = CompositingMode.SourceOver;
                Form form = FindNearestForm();

                // 1) Form background (image or solid colour)
                if (form?.BackgroundImage != null)
                {
                    Size cs = form.ClientSize;
                    if (cs.Width > 0 && cs.Height > 0)
                    {
                        float sx = (float)form.BackgroundImage.Width / cs.Width;
                        float sy = (float)form.BackgroundImage.Height / cs.Height;
                        g.DrawImage(form.BackgroundImage,
                            new RectangleF(0, 0, Width, Height),
                            new RectangleF(pos.X * sx, pos.Y * sy, Width * sx, Height * sy),
                            GraphicsUnit.Pixel);
                    }
                }
                else if (form != null)
                {
                    using (var brush = new SolidBrush(form.BackColor))
                        g.FillRectangle(brush, 0, 0, Width, Height);
                }
                else
                {
                    g.Clear(SystemColors.Control);
                    return bgCache;
                }

                // 2) Layer each ancestor's BackColor and BackgroundImage
                //    (handles semi-transparent overlays and panels with screenshots)
                var ancestors = new List<Control>();
                Control c = Parent;
                while (c != null && c != form)
                {
                    ancestors.Add(c);
                    c = c.Parent;
                }
                ancestors.Reverse();
                foreach (var ancestor in ancestors)
                {
                    if (ancestor.BackColor.A > 0)
                    {
                        using (var brush = new SolidBrush(ancestor.BackColor))
                            g.FillRectangle(brush, 0, 0, Width, Height);
                    }

                    // If the ancestor has a BackgroundImage (e.g. the game-over
                    // overlay with a captured screenshot), composite it so the
                    // button corners show the correct background.
                    if (ancestor.BackgroundImage != null)
                    {
                        try
                        {
                            Point btnInAncestor = Point.Empty;
                            Control walk = this;
                            while (walk != null && walk != ancestor)
                            {
                                btnInAncestor.X += walk.Left;
                                btnInAncestor.Y += walk.Top;
                                walk = walk.Parent;
                            }
                            var bgImg = ancestor.BackgroundImage;
                            if (ancestor.BackgroundImageLayout == ImageLayout.None)
                            {
                                g.DrawImage(bgImg,
                                    new RectangleF(0, 0, Width, Height),
                                    new RectangleF(btnInAncestor.X, btnInAncestor.Y, Width, Height),
                                    GraphicsUnit.Pixel);
                            }
                            else
                            {
                                // Stretch / Zoom: scale source coordinates
                                Size acs = ancestor.ClientSize;
                                if (acs.Width > 0 && acs.Height > 0)
                                {
                                    float asx = (float)bgImg.Width / acs.Width;
                                    float asy = (float)bgImg.Height / acs.Height;
                                    g.DrawImage(bgImg,
                                        new RectangleF(0, 0, Width, Height),
                                        new RectangleF(btnInAncestor.X * asx, btnInAncestor.Y * asy,
                                                       Width * asx, Height * asy),
                                        GraphicsUnit.Pixel);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            return bgCache;
        }

        private Point GetPositionOnForm()
        {
            Form form = FindNearestForm();
            if (form == null || !IsHandleCreated) return Point.Empty;
            try
            {
                Point screen = PointToScreen(Point.Empty);
                return form.PointToClient(screen);
            }
            catch { return Point.Empty; }
        }

        // Invalidate cache when the control moves or resizes
        protected override void OnLocationChanged(EventArgs e)
        {
            InvalidateBgCache();
            base.OnLocationChanged(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            InvalidateBgCache();
            base.OnSizeChanged(e);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            InvalidateBgCache();
            base.OnParentChanged(e);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            InvalidateBgCache();
            base.OnVisibleChanged(e);
        }

        private void InvalidateBgCache()
        {
            bgCache?.Dispose();
            bgCache = null;
            Invalidate();
        }

        // ───── painting ─────

        private const int WM_ERASEBKGND = 0x0014;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;   // suppress erase
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // intentionally empty – OnPaint handles everything
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // 1) Paint composited background (corners will show through)
            var bg = GetCompositeBackground();
            if (bg != null)
                g.DrawImageUnscaled(bg, 0, 0);

            // 2) Rounded gradient button face
            var rect = new RectangleF(1, 1, Width - 2, Height - 2);
            float r = Math.Min(cornerRadius, Math.Min(rect.Width, rect.Height) / 2);

            Color top, bottom;
            if (isPressed) { top = pressedTop; bottom = pressedBottom; }
            else if (isHovered) { top = hoverTop; bottom = hoverBottom; }
            else { top = gradientTop; bottom = gradientBottom; }

            using (var path = GetRoundedRect(rect, r))
            {
                using (var brush = new LinearGradientBrush(
                    new RectangleF(rect.X, rect.Y - 1, rect.Width, rect.Height + 2),
                    top, bottom, 90f))
                {
                    g.FillPath(brush, path);
                }

                // subtle inner highlight for depth
                var highlightRect = new RectangleF(rect.X + 2, rect.Y + 1, rect.Width - 4, rect.Height / 2);
                float hr = Math.Min(cornerRadius - 1, Math.Min(highlightRect.Width, highlightRect.Height) / 2);
                if (hr > 0 && highlightRect.Width > 0 && highlightRect.Height > 0)
                {
                    using (var hPath = GetRoundedRect(highlightRect, hr))
                    using (var hBrush = new LinearGradientBrush(
                        highlightRect, Color.FromArgb(40, 255, 255, 255), Color.Transparent, 90f))
                    {
                        g.FillPath(hBrush, hPath);
                    }
                }

                // text with drop shadow
                g.SetClip(path);
                var textRect = new RectangleF(0, 0, Width, Height);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    using (var sBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
                    {
                        var sr = textRect;
                        sr.Offset(0, 1.5f);
                        g.DrawString(Text, Font, sBrush, sr, sf);
                    }
                    using (var textBrush = new SolidBrush(ForeColor))
                    {
                        g.DrawString(Text, Font, textBrush, textRect, sf);
                    }
                }
                g.ResetClip();
            }

            // focus indicator
            if (Focused && ShowFocusCues)
            {
                var focusRect = new RectangleF(3, 3, Width - 6, Height - 6);
                float fr = Math.Max(r - 2, 1);
                using (var fPath = GetRoundedRect(focusRect, fr))
                using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
                {
                    pen.DashStyle = DashStyle.Dot;
                    g.DrawPath(pen, fPath);
                }
            }
        }

        // ───── interaction ─────

        protected override void OnMouseEnter(EventArgs e)
        {
            isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            isHovered = false;
            isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPressed = true;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            isPressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                isPressed = true;
                Invalidate();
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                isPressed = false;
                Invalidate();
                OnClick(EventArgs.Empty);
            }
            base.OnKeyUp(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                bgCache?.Dispose();
                bgCache = null;
            }
            base.Dispose(disposing);
        }
    }
}
