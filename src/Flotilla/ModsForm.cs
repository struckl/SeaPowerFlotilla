using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Flotilla
{
    /// <summary>Which mods one copy uses, in what order, and which mods only this copy has.</summary>
    public sealed class ModsForm : Form
    {
        readonly Fleet fleet;
        readonly Install install;
        readonly List<ModEntry> mods;
        readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, CheckBoxes = true };
        readonly Button up = Btn("Move up"), down = Btn("Move down"), keep = Btn("Keep in this copy"), back = Btn("Use Workshop again"),
            addFolder = Btn("Add folder..."), addZip = Btn("Add zip..."), remove = Btn("Remove...");

        public ModsForm(Fleet fleet, Install install, List<ModEntry> mods)
        {
            this.fleet = fleet;
            this.install = install;
            this.mods = mods;
            Text = $"Mods of \"{install.Name}\" - {Program.Title}";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(880, 500);
            MinimumSize = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;

            list.Columns.Add("Mod (ticked = on, in load order)", 270);
            list.Columns.Add("Where", 220);
            list.Columns.Add("Made for", 70);
            list.Columns.Add("Folder", 140);
            list.SelectedIndexChanged += (s, e) => UpdateButtons();

            var side = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 150, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10, 8, 10, 0), WrapContents = false };
            side.Controls.AddRange(new Control[] { up, down, keep, back, addFolder, addZip, remove });
            keep.Click += (s, e) => Keep_();
            back.Click += (s, e) => Back_();
            up.Click += (s, e) => Move_(-1);
            down.Click += (s, e) => Move_(1);
            addFolder.Click += (s, e) => AddFolder();
            addZip.Click += (s, e) => AddZip();
            remove.Click += (s, e) => Remove_();

            var note = new Label
            {
                Dock = DockStyle.Top, Height = 74, Padding = new Padding(10, 8, 10, 0), Text =
                    $"Game version of this copy: {install.Build}. Everything here applies to this copy only." +
                    "\nKeep in this copy: this copy gets its own copy of a Workshop mod. Steam updates and unsubscribing no longer change it here." +
                    (Game.HasLoader(install.Dir) ? "" : "\nThis copy has no mod loader: mods with scripts (DLLs) will not run in it, data mods will."),
            };
            var ok = new Button { Text = "Save", Width = 100, DialogResult = DialogResult.None };
            var cancel = new Button { Text = "Cancel", Width = 100, DialogResult = DialogResult.Cancel };
            ok.Click += (s, e) => Save();
            var row = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 6, 10, 0) };
            row.Controls.Add(cancel);
            row.Controls.Add(ok);
            CancelButton = cancel;

            Controls.Add(list);
            Controls.Add(side);
            Controls.Add(note);
            Controls.Add(row);
            Refill(0);
        }

        static Button Btn(string text) => new Button { Text = text, Width = 128, Height = 30, Margin = new Padding(0, 0, 0, 6) };

        List<ModEntry> Shown => mods.Where(m => !m.Missing).ToList();

        void Keep()
        {
            foreach (ListViewItem item in list.Items) ((ModEntry)item.Tag).Enabled = item.Checked;
        }

        void Refill(int select)
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var mod in Shown)
                list.Items.Add(new ListViewItem(new[] { mod.Name, mod.Where, mod.MadeFor, mod.Folder }) { Tag = mod, Checked = mod.Enabled });
            if (list.Items.Count > 0) list.Items[Math.Max(0, Math.Min(select, list.Items.Count - 1))].Selected = true;
            list.EndUpdate();
            UpdateButtons();
        }

        ModEntry Selected => list.SelectedItems.Count == 0 ? null : (ModEntry)list.SelectedItems[0].Tag;

        void UpdateButtons()
        {
            int index = list.SelectedIndices.Count == 0 ? -1 : list.SelectedIndices[0];
            up.Enabled = index > 0;
            down.Enabled = index >= 0 && index < list.Items.Count - 1;
            keep.Enabled = Selected != null && !Selected.IsLocal;
            back.Enabled = Selected?.IsKept == true;
            remove.Enabled = Selected?.IsLocal == true;
        }

        void Move_(int by)
        {
            var mod = Selected;
            if (mod == null) return;
            Keep();
            var shown = Shown;
            int index = shown.IndexOf(mod);
            var other = shown[index + by];
            int a = mods.IndexOf(mod), b = mods.IndexOf(other);
            mods[a] = other;
            mods[b] = mod;
            Refill(index + by);
        }

        void AddFolder()
        {
            using (var pick = new FolderBrowserDialog { Description = "The mod's folder (the one with _info.ini in it)" })
                if (pick.ShowDialog(this) == DialogResult.OK) AddFrom(pick.SelectedPath);
        }

        void AddZip()
        {
            using (var pick = new OpenFileDialog { Filter = "Zip files|*.zip", Title = "A mod as a zip file" })
                if (pick.ShowDialog(this) == DialogResult.OK) AddFrom(pick.FileName);
        }

        void AddFrom(string source)
        {
            try
            {
                Keep();
                var folder = ModList.Add(fleet, install, source);
                var fresh = ModList.Read(fleet, install).First(m => m.IsLocal && Flotilla.Layout.Same(m.Folder, folder));
                fresh.Enabled = true;
                mods.Add(fresh);
                Refill(Shown.Count - 1);
            }
            catch (FlotillaException e) { Warn(e.Message); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException) { Warn("Could not add the mod: " + e.Message); }
        }

        void Remove_()
        {
            var mod = Selected;
            if (mod == null || !mod.IsLocal) return;
            var answer = MessageBox.Show(this, $"Delete the mod folder \"{mod.Folder}\" from \"{install.Name}\"? This cannot be undone.", Program.Title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
            try
            {
                Keep();
                int index = list.SelectedIndices[0];
                ModList.Remove(fleet, install, mod);
                mods.Remove(mod);
                Refill(index);
            }
            catch (FlotillaException e) { Warn(e.Message); }
        }

        void Keep_()
        {
            var item = Selected;
            if (item == null || item.IsLocal) return;
            try
            {
                Keep();
                var folder = ModList.Keep(fleet, install, item);
                var kept = ModList.Read(fleet, install).First(m => m.IsLocal && Flotilla.Layout.Same(m.Folder, folder));
                kept.Enabled = item.Enabled;
                item.Enabled = false;
                mods.Insert(mods.IndexOf(item), kept);  // takes the Workshop entry's place in the load order
                Refill(Shown.IndexOf(kept));
            }
            catch (FlotillaException e) { Warn(e.Message); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Warn("Could not keep the mod: " + e.Message); }
        }

        void Back_()
        {
            var kept = Selected;
            if (kept == null || !kept.IsKept) return;
            var item = mods.FirstOrDefault(m => !m.IsLocal && !m.Missing && m.SteamId == kept.KeptFrom);
            if (item == null) { Warn($"\"{kept.Name}\" is not subscribed on the Workshop right now. Subscribe to it in Steam first, then try again."); return; }
            var answer = MessageBox.Show(this, $"Delete this copy's kept \"{kept.Name}\" ({kept.Where}) and use the Workshop version again?" +
                (item.MadeFor.Length > 0 ? $"\nThe Workshop version says it is made for game {item.MadeFor}; this copy is {install.Build}." : ""),
                Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
            try
            {
                Keep();
                ModList.Remove(fleet, install, kept);
                item.Enabled = kept.Enabled;
                mods.Remove(item);
                int at = mods.IndexOf(kept);
                mods[at] = item;  // the Workshop entry takes the kept one's place
                Refill(Shown.IndexOf(item));
            }
            catch (FlotillaException e) { Warn(e.Message); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { Warn("Could not remove the kept copy: " + e.Message); }
        }

        void Save()
        {
            try
            {
                Keep();
                ModList.Save(fleet, install, mods);
                DialogResult = DialogResult.OK;
            }
            catch (FlotillaException e) { Warn(e.Message); }
        }

        void Warn(string text) => MessageBox.Show(this, text, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
