using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Flotilla
{
    /// <summary>
    /// The mod check of one copy against its game version: each mod that is on, whether it fits, and a step for the ones that do not
    /// (refresh a kept mod from the Workshop, or switch it off). Apply refreshes files at once and switches mods off in the list;
    /// the caller saves the list.
    /// </summary>
    public sealed class CheckForm : Form
    {
        readonly Fleet fleet;
        readonly Install install;
        readonly List<ModList.CheckRow> rows;
        readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, CheckBoxes = true };

        public bool Changed { get; private set; }

        public CheckForm(Fleet fleet, Install install, List<ModList.CheckRow> rows, string gameVersion, string intro)
        {
            this.fleet = fleet;
            this.install = install;
            this.rows = rows;
            Text = $"Mods and game version - \"{install.Name}\" - {Program.Title}";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(960, 400);
            MinimumSize = new Size(640, 320);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            list.Columns.Add("Mod", 190);
            list.Columns.Add("Where", 140);
            list.Columns.Add($"Fits game {gameVersion}?", 180);
            list.Columns.Add("Step (ticked = done on Apply)", 400);
            foreach (var row in rows)
            {
                var fit = row.Fits ? $"Yes: {row.Mod.MadeFor}" : $"No: made for {row.Mod.MadeFor}";
                var step = row.Step == ModList.Step.Refresh ? $"Refresh from the Workshop (that one fits: {row.WorkshopFor})"
                         : row.Step == ModList.Step.SwitchOff ? (row.Mod.IsKept ? "Switch off (the Workshop version does not fit either)"
                                                               : row.Mod.IsLocal ? "Switch off (no fitting version known)"
                                                               : "Switch off (no version for this game yet)")
                         : "Nothing to do";
                var item = new ListViewItem(new[] { row.Mod.Name, row.Mod.Where, fit, step }) { Tag = row, Checked = row.Step != ModList.Step.None, UseItemStyleForSubItems = false };
                item.SubItems[2].ForeColor = row.Fits ? Color.DarkGreen : Color.Firebrick;
                if (row.Step == ModList.Step.None) item.SubItems[3].ForeColor = SystemColors.GrayText;
                list.Items.Add(item);
            }
            list.ItemCheck += (s, e) => { if (((ModList.CheckRow)list.Items[e.Index].Tag).Step == ModList.Step.None) e.NewValue = CheckState.Unchecked; };

            var note = new Label { Dock = DockStyle.Top, Height = 54, Padding = new Padding(10, 8, 10, 0), Text = intro +
                "\nThe mods that are on in this copy, checked as the game checks them. Switching off applies to this copy only. Untick what you want to leave as it is." };
            var apply = new Button { Text = "Apply", Width = 100 };
            var leave = new Button { Text = "Leave as is", Width = 100, DialogResult = DialogResult.Cancel };
            apply.Click += (s, e) => Apply_();
            var row2 = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 6, 10, 0) };
            row2.Controls.Add(leave);
            row2.Controls.Add(apply);
            CancelButton = leave;
            var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 0) };
            pad.Controls.Add(list);
            Controls.Add(pad);
            Controls.Add(note);
            Controls.Add(row2);
        }

        void Apply_()
        {
            var ticked = list.Items.Cast<ListViewItem>().Where(i => i.Checked).Select(i => (ModList.CheckRow)i.Tag).Where(r => r.Step != ModList.Step.None).ToList();
            try
            {
                Cursor = Cursors.WaitCursor;
                ModList.Apply(fleet, install, ticked);
                Changed = ticked.Count > 0;
                DialogResult = DialogResult.OK;
            }
            catch (FlotillaException e) { Warn(e.Message); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Warn("Could not refresh the mod: " + e.Message); }
            finally { Cursor = Cursors.Default; }
        }

        void Warn(string text) => MessageBox.Show(this, text, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
