using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Flotilla
{
    /// <summary>Name and kind of a new copy, with the space it needs.</summary>
    public sealed class CreateForm : Form
    {
        readonly Fleet fleet;
        readonly TextBox name = new TextBox { Width = 300 };
        readonly RadioButton clean = new RadioButton { Text = "Clean", AutoSize = true, Checked = true };
        readonly RadioButton setup = new RadioButton { Text = "Copy of my setup", AutoSize = true };
        readonly Label space = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Text = "Working out the size..." };
        readonly Label problem = new Label { AutoSize = true, ForeColor = Color.Firebrick };
        readonly Button ok = new Button { Text = "Create", DialogResult = DialogResult.OK, Width = 100 };
        long cleanBytes = -1, setupBytes = -1;

        public string CopyName => name.Text.Trim();
        public bool Clean => clean.Checked;

        public CreateForm(Fleet fleet)
        {
            this.fleet = fleet;
            Text = "New copy - " + Program.Title;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Padding = new Padding(14), Dock = DockStyle.Fill };
            layout.Controls.Add(new Label { Text = "Name", AutoSize = true });
            layout.Controls.Add(name);
            layout.Controls.Add(problem);
            layout.Controls.Add(clean);
            layout.Controls.Add(Note("Only the game's own files, as Steam installs them: no mod loader, no local mods, no missions,\n" +
                                     "fresh settings with no mods enabled. For bug reports to the developers."));
            layout.Controls.Add(setup);
            layout.Controls.Add(Note("Everything of your Steam install: the mod loader (BepInEx), local mods, missions, settings,\n" +
                                     "the enabled mods with their load order, and saves. A starting point for a big mod set."));
            layout.Controls.Add(Note("Workshop subscriptions belong to your Steam account, so every copy sees them.\n" +
                                     "Which ones are enabled, and in what order, is kept per copy."));
            layout.Controls.Add(space);
            var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 100 };
            row.Controls.Add(cancel);
            row.Controls.Add(ok);
            layout.Controls.Add(row);
            Controls.Add(layout);
            AcceptButton = ok;
            CancelButton = cancel;

            name.Text = Suggest();
            name.TextChanged += (s, e) => Check();
            clean.CheckedChanged += (s, e) => { if (clean.Checked && name.Text == "My setup") name.Text = Suggest(); Check(); };
            setup.CheckedChanged += (s, e) => { if (setup.Checked && name.Text == "Clean") name.Text = Suggest(); Check(); };
            Shown += async (s, e) =>
            {
                var sizes = await Task.Run(() => (fleet.PlanCopy(true).Bytes, fleet.PlanCopy(false).Bytes + fleet.PlanData().Bytes));
                cleanBytes = sizes.Item1;
                setupBytes = sizes.Item2;
                Check();
            };
            Check();
        }

        static Label Note(string text) => new Label { Text = text, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(20, 0, 0, 8) };

        string Suggest()
        {
            var basis = clean.Checked ? "Clean" : "My setup";
            for (int n = 1; ; n++)
            {
                var candidate = n == 1 ? basis : $"{basis} {n}";
                if (fleet.CheckName(candidate) == null) return candidate;
                if (n > 50) return basis;
            }
        }

        void Check()
        {
            var why = fleet.CheckName(name.Text);
            var need = clean.Checked ? cleanBytes : setupBytes;
            if (need >= 0)
            {
                long free = -1;
                try { free = Fleet.FreeBytes(fleet.FleetDir); } catch (Exception) { }
                space.Text = $"Needs about {Fleet.Gb(need)} in {fleet.FleetDir}" + (free >= 0 ? $" ({Fleet.Gb(free)} free)." : ".");
                if (why == null && free >= 0 && free < need + (1L << 30)) why = "Not enough free space on that drive. Change the folder in the main window.";
            }
            problem.Text = why ?? "";
            ok.Enabled = why == null && need >= 0;
        }
    }
}
