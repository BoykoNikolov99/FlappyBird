using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public class OptionsForm : Form
    {
        private TrackBar trackPipeGap;
        private TrackBar trackSpeed;
        private TrackBar trackDungeon;
        private Label lblGapValue;
        private Label lblSpeedValue;
        private Label lblDungeonValue;
        private Button btnSave;
        private Button btnCancel;

        public int PipeGap { get; private set; }
        public int BasePipeSpeed { get; private set; }
        public int DungeonIntervalSeconds { get; private set; }

        public OptionsForm(int pipeGap, int basePipeSpeed, int dungeonInterval)
        {
            PipeGap = pipeGap;
            BasePipeSpeed = basePipeSpeed;
            DungeonIntervalSeconds = dungeonInterval;
            InitializeOptions();
        }

        private void InitializeOptions()
        {
            this.Text = "Options";
            this.ClientSize = new Size(420, 340);
            IconHelper.SetFormIcon(this);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(30, 30, 30);

            int labelX = 30;
            int trackX = 30;
            int trackWidth = 260;
            int valueX = 310;
            int rowHeight = 80;
            int startY = 20;

            // --- Pipe Gap ---
            var lblGap = CreateLabel("Pipe Gap (Difficulty):", labelX, startY);
            this.Controls.Add(lblGap);

            trackPipeGap = new TrackBar();
            trackPipeGap.Minimum = 80;
            trackPipeGap.Maximum = 200;
            trackPipeGap.Value = PipeGap;
            trackPipeGap.TickFrequency = 10;
            trackPipeGap.Location = new Point(trackX, startY + 25);
            trackPipeGap.Size = new Size(trackWidth, 30);
            trackPipeGap.ValueChanged += (s, e) => { lblGapValue.Text = trackPipeGap.Value.ToString(); };
            this.Controls.Add(trackPipeGap);

            lblGapValue = CreateValueLabel(trackPipeGap.Value.ToString(), valueX, startY + 25);
            this.Controls.Add(lblGapValue);

            // --- Base Speed ---
            var lblSpeed = CreateLabel("Base Pipe Speed:", labelX, startY + rowHeight);
            this.Controls.Add(lblSpeed);

            trackSpeed = new TrackBar();
            trackSpeed.Minimum = 3;
            trackSpeed.Maximum = 12;
            trackSpeed.Value = BasePipeSpeed;
            trackSpeed.TickFrequency = 1;
            trackSpeed.Location = new Point(trackX, startY + rowHeight + 25);
            trackSpeed.Size = new Size(trackWidth, 30);
            trackSpeed.ValueChanged += (s, e) => { lblSpeedValue.Text = trackSpeed.Value.ToString(); };
            this.Controls.Add(trackSpeed);

            lblSpeedValue = CreateValueLabel(trackSpeed.Value.ToString(), valueX, startY + rowHeight + 25);
            this.Controls.Add(lblSpeedValue);

            // --- Dungeon Interval ---
            var lblDungeon = CreateLabel("Dungeon Interval (seconds):", labelX, startY + rowHeight * 2);
            this.Controls.Add(lblDungeon);

            trackDungeon = new TrackBar();
            trackDungeon.Minimum = 5;
            trackDungeon.Maximum = 30;
            trackDungeon.Value = DungeonIntervalSeconds;
            trackDungeon.TickFrequency = 5;
            trackDungeon.Location = new Point(trackX, startY + rowHeight * 2 + 25);
            trackDungeon.Size = new Size(trackWidth, 30);
            trackDungeon.ValueChanged += (s, e) => { lblDungeonValue.Text = trackDungeon.Value.ToString(); };
            this.Controls.Add(trackDungeon);

            lblDungeonValue = CreateValueLabel(trackDungeon.Value.ToString(), valueX, startY + rowHeight * 2 + 25);
            this.Controls.Add(lblDungeonValue);

            // --- Buttons ---
            btnSave = new Button();
            btnSave.Text = "Save";
            btnSave.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            btnSave.Size = new Size(100, 38);
            btnSave.Location = new Point(80, startY + rowHeight * 3 + 10);
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.BackColor = Color.FromArgb(200, 34, 139, 34);
            btnSave.ForeColor = Color.White;
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold);
            btnCancel.Size = new Size(100, 38);
            btnCancel.Location = new Point(240, startY + rowHeight * 3 + 10);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.BackColor = Color.FromArgb(200, 180, 30, 30);
            btnCancel.ForeColor = Color.White;
            btnCancel.Click += BtnCancel_Click;
            this.Controls.Add(btnCancel);
        }

        private Label CreateLabel(string text, int x, int y)
        {
            var lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            lbl.ForeColor = Color.White;
            lbl.BackColor = Color.Transparent;
            lbl.AutoSize = true;
            lbl.Location = new Point(x, y);
            return lbl;
        }

        private Label CreateValueLabel(string text, int x, int y)
        {
            var lbl = new Label();
            lbl.Text = text;
            lbl.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
            lbl.ForeColor = Color.Yellow;
            lbl.BackColor = Color.Transparent;
            lbl.AutoSize = true;
            lbl.Location = new Point(x, y + 5);
            return lbl;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            PipeGap = trackPipeGap.Value;
            BasePipeSpeed = trackSpeed.Value;
            DungeonIntervalSeconds = trackDungeon.Value;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
