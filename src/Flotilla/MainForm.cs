using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Flotilla
{
    public sealed class MainForm : Form
    {
        static readonly Color Navy = Color.FromArgb(16, 28, 40), Cyan = Color.FromArgb(70, 205, 225), Mist = Color.FromArgb(170, 185, 200);

        readonly Fleet fleet;
        readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
        readonly Button play = Btn("Play"), create = Btn("New copy..."), update = Btn("Update"), shortcut = Btn("Desktop shortcut"),
                        open = Btn("Open folder"), delete = Btn("Delete..."), cancel = Btn("Cancel");
        readonly Label status = new Label { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        readonly Label fleetLabel = new Label { AutoSize = true, Margin = new Padding(0, 6, 8, 0) };
        readonly ProgressBar bar = new ProgressBar { Width = 220, Dock = DockStyle.Right, Visible = false, Maximum = 1000 };
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 2000 };
        CancellationTokenSource busy;
        string lastRunning = "";  // the status shown last; "" = nothing shown yet
        bool closeWhenDone;
        DateTime startedAt;

        public MainForm(Fleet fleet)
        {
            this.fleet = fleet;
            Text = Program.Title;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(940, 480);
            MinimumSize = new Size(760, 380);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            list.Columns.Add("Install", 140);
            list.Columns.Add("Kind", 110);
            list.Columns.Add("Game version", 190);
            list.Columns.Add("Mod loader", 90);
            list.Columns.Add("Local mods", 80);
            list.Columns.Add("Folder", 300);
            list.Resize += (s, e) => list.Columns[list.Columns.Count - 1].Width = -2;  // the folder column takes the rest
            list.SelectedIndexChanged += (s, e) => UpdateButtons();
            list.DoubleClick += (s, e) => Play();

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 170, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10, 8, 10, 0), WrapContents = false };
            buttons.Controls.AddRange(new Control[] { play, create, update, shortcut, open, delete });
            play.Font = new Font(Font, FontStyle.Bold);
            play.Click += (s, e) => Play();
            create.Click += (s, e) => Create();
            update.Click += (s, e) => Update_();
            shortcut.Click += (s, e) => Shortcut_();
            open.Click += (s, e) => { var i = Selected(); if (i != null) Process.Start("explorer.exe", $"\"{i.Dir}\"")?.Dispose(); };
            delete.Click += (s, e) => Delete();
            cancel.Visible = false;
            cancel.Dock = DockStyle.Right;
            cancel.Click += (s, e) => busy?.Cancel();

            var fleetBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(10, 4, 10, 0), WrapContents = false };
            var change = new LinkLabel { Text = "Change...", AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
            change.LinkClicked += (s, e) => ChangeFleetDir();
            fleetBar.Controls.AddRange(new Control[] { fleetLabel, change });

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(10, 4, 10, 4) };
            var signature = new Label
            {
                Text = "R.A.T.S. – Ratte's Assorted Tactical Systems", Dock = DockStyle.Bottom, Height = 18,
                ForeColor = SystemColors.GrayText, Font = new Font(Font, FontStyle.Italic), TextAlign = ContentAlignment.MiddleRight,
            };
            var statusRow = new Panel { Dock = DockStyle.Fill };
            statusRow.Controls.Add(status);
            statusRow.Controls.Add(bar);
            statusRow.Controls.Add(cancel);
            footer.Controls.Add(statusRow);
            footer.Controls.Add(signature);

            Controls.Add(list);
            Controls.Add(buttons);
            Controls.Add(fleetBar);
            Controls.Add(Header());
            Controls.Add(footer);

            timer.Tick += (s, e) => ShowRunning();
            Load += (s, e) => Start();
            FormClosing += Closing_;
        }

        static Button Btn(string text) => new Button { Text = text, Width = 148, Height = 32, Margin = new Padding(0, 0, 0, 6) };

        Control Header()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Navy, Padding = new Padding(14, 8, 14, 8) };
            var mark = new Label { Text = "R . A . T . S .", ForeColor = Cyan, AutoSize = true, Location = new Point(14, 8), Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold) };
            var name = new Label { Text = "Flotilla", ForeColor = Color.White, AutoSize = true, Location = new Point(12, 24), Font = new Font(Font.FontFamily, 18f, FontStyle.Bold) };
            var line = new Label
            {
                Text = "Several Sea Power installs side by side, each with its own mods, settings and saves",
                ForeColor = Mist, AutoSize = true, Location = new Point(122, 36),
            };
            header.Controls.AddRange(new Control[] { mark, name, line });
            header.Layout += (s, e) => line.Left = name.Right + 12;
            return header;
        }

        void Start()
        {
            if (fleet.SteamGame == null && !Locate()) { Close(); return; }
            try
            {
                if (fleet.Swap.NeedsRepair && !fleet.GameRunning() && fleet.EnsureNormal())
                {
                    Say("The last copy did not close cleanly: your Steam install's settings and saves are back in place.");
                    lastRunning = null;  // keep this message
                }
            }
            catch (FlotillaException e) { Warn(e.Message); }
            Refill();
            timer.Start();
            var unfinished = fleet.Unfinished();
            if (unfinished.Count > 0)
                RunBusy("Removing a copy that was interrupted", (progress, token) =>
                {
                    foreach (var dir in unfinished) fleet.RemoveUnfinished(dir);
                    return unfinished.Count == 1
                        ? $"Removed the unfinished copy \"{Path.GetFileName(unfinished[0])}\". It was interrupted while being made."
                        : $"Removed {unfinished.Count} unfinished copies. They were interrupted while being made.";
                }, showBar: false);
        }

        bool Locate()
        {
            MessageBox.Show("Flotilla did not find Sea Power in your Steam libraries. Show it the Sea Power folder (the one with \"Sea Power.exe\").",
                Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            using (var pick = new FolderBrowserDialog { Description = "The Sea Power folder of your Steam library" })
            {
                while (pick.ShowDialog(this) == DialogResult.OK)
                {
                    if (File.Exists(Path.Combine(pick.SelectedPath, Game.Exe)))
                    {
                        fleet.SteamGame = pick.SelectedPath;
                        fleet.FleetDir ??= Fleet.DefaultFleetDir(fleet.SteamGame);
                        Settings.Write("SteamGame", pick.SelectedPath);
                        return true;
                    }
                    Warn($"There is no {Game.Exe} in that folder.");
                }
            }
            return false;
        }

        void Refill()
        {
            var keep = Selected()?.Name;
            list.BeginUpdate();
            list.Items.Clear();
            var steamBuild = Game.BuildOf(fleet.SteamGame);
            foreach (var install in fleet.List())
            {
                var build = install.Build;
                var outdated = !install.IsSteam && build != steamBuild;
                var item = new ListViewItem(new[]
                {
                    install.Name, install.KindText, outdated ? $"{build} – update" : build,
                    Game.HasLoader(install.Dir) ? "BepInEx" : "none", Game.LocalMods(install.Dir).Count.ToString(), install.Dir,
                }) { Tag = install };
                if (outdated) item.ForeColor = Color.DarkOrange;
                if (install.IsSteam) item.Font = new Font(list.Font, FontStyle.Bold);
                list.Items.Add(item);
                if (install.Name == keep) item.Selected = true;
            }
            if (list.SelectedItems.Count == 0 && list.Items.Count > 0) list.Items[0].Selected = true;
            list.EndUpdate();
            fleetLabel.Text = $"Copies are kept in:  {fleet.FleetDir}";
            UpdateButtons();
            ShowRunning();
        }

        Install Selected() => list.SelectedItems.Count == 0 ? null : (Install)list.SelectedItems[0].Tag;

        void UpdateButtons()
        {
            var install = Selected();
            bool idle = busy == null, copy = install != null && !install.IsSteam;
            play.Enabled = idle && install != null;
            create.Enabled = idle;
            update.Enabled = idle && copy;
            shortcut.Enabled = idle && copy;
            delete.Enabled = idle && copy;
            open.Enabled = install != null;
        }

        void ShowRunning()
        {
            if (busy != null) return;
            string text = null;
            if (fleet.GameRunning())
            {
                var active = fleet.List().FirstOrDefault(fleet.IsActive);
                text = active != null ? $"At sea: \"{active.Name}\". Its settings and saves are in use; the Steam install's come back when the game closes."
                                      : "At sea: the Steam install.";
            }
            if (text == lastRunning) return;
            if (text == null && DateTime.Now - startedAt < TimeSpan.FromSeconds(30)) return;  // the game takes a while to appear
            var docked = text == null && !string.IsNullOrEmpty(lastRunning);
            lastRunning = text;
            if (docked) Refill();
            Say(text ?? "In port. Pick an install and press Play.");
        }

        void Play()
        {
            var install = Selected();
            if (install == null || busy != null) return;
            try
            {
                fleet.CheckCanPlay();
                fleet.EnsureNormal();
                if (install.IsSteam)
                {
                    fleet.Play(install);
                    Say("Starting the Steam install through Steam...");
                    lastRunning = "starting";
                    startedAt = DateTime.Now;
                    return;
                }
                // A separate Flotilla process waits for the game and puts the data folder back, so this window can be closed.
                Process.Start(new ProcessStartInfo(Application.ExecutablePath, $"--play \"{install.Name}\"") { UseShellExecute = false })?.Dispose();
                Say($"Starting \"{install.Name}\"...");
                lastRunning = "starting";
                startedAt = DateTime.Now;
            }
            catch (FlotillaException e) { Warn(e.Message); }
        }

        void Create()
        {
            using (var dialog = new CreateForm(fleet))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                RunBusy($"Copying Sea Power to \"{dialog.CopyName}\"", (progress, token) =>
                {
                    fleet.Create(dialog.CopyName, dialog.Clean, progress, token);
                    return $"\"{dialog.CopyName}\" is ready. Select it and press Play.";
                });
            }
        }

        void Update_()
        {
            var install = Selected();
            if (install == null || install.IsSteam) return;
            RunBusy($"Updating \"{install.Name}\" to the Steam install's game files", (progress, token) =>
            {
                var result = fleet.Update(install, progress, token);
                return result.Copied + result.Deleted == 0
                    ? $"\"{install.Name}\" already had the Steam install's game files."
                    : $"\"{install.Name}\" updated: {result.Copied} files copied, {result.Deleted} removed. Its mods, missions and settings are unchanged.";
            });
        }

        void Shortcut_()
        {
            var install = Selected();
            if (install == null || install.IsSteam) return;
            try { Say($"Shortcut on the desktop: {Path.GetFileName(Shortcut.MakeOnDesktop(install))}"); }
            catch (Exception e) { Warn("Could not make the shortcut: " + e.Message); }
        }

        void Delete()
        {
            var install = Selected();
            if (install == null || install.IsSteam) return;
            var answer = MessageBox.Show(
                $"Delete \"{install.Name}\"?\n\n{install.Dir}\n\nThe copy's game files, its mods, missions, settings and saves are deleted for good. " +
                "The Steam install and the other copies are not touched.",
                Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
            RunBusy($"Deleting \"{install.Name}\"", (progress, token) =>
            {
                fleet.Delete(install);
                return $"\"{install.Name}\" deleted.";
            }, showBar: false);
        }

        void ChangeFleetDir()
        {
            using (var pick = new FolderBrowserDialog { Description = "Folder for the Sea Power copies (about 19 GB each)", SelectedPath = fleet.FleetDir ?? "" })
            {
                if (pick.ShowDialog(this) != DialogResult.OK) return;
                var problem = fleet.CheckFleetDir(pick.SelectedPath);
                if (problem != null) { Warn(problem); return; }
                fleet.FleetDir = pick.SelectedPath;
                Settings.Write("FleetDir", pick.SelectedPath);
                Refill();
                Say("New copies go to this folder. Copies in the old folder stay there; Flotilla lists the ones in this folder.");
            }
        }

        async void RunBusy(string what, Func<IProgress<double>, CancellationToken, string> work, bool showBar = true)
        {
            busy = new CancellationTokenSource();
            UpdateButtons();
            bar.Value = 0;
            bar.Visible = cancel.Visible = showBar;
            Say(what + "...");
            var progress = new Progress<double>(f =>
            {
                bar.Value = (int)(Math.Min(1, f) * 1000);
                status.Text = $"{what}... {f * 100:0}%";
            });
            var token = busy.Token;
            try { Say(await Task.Run(() => work(progress, token))); }
            catch (OperationCanceledException) { Say("Cancelled. Nothing was left behind."); }
            catch (FlotillaException e) { Say(""); Warn(e.Message); }
            catch (Exception e) { Settings.Log("error: " + e); Say(""); Warn($"{what} failed: {e.Message}"); }
            finally
            {
                busy.Dispose();
                busy = null;
                bar.Visible = cancel.Visible = false;
                lastRunning = null;  // keep the result message until the game state changes
                Refill();
                if (closeWhenDone) Close();
            }
        }

        void Closing_(object sender, FormClosingEventArgs e)
        {
            if (busy == null) return;
            var answer = MessageBox.Show("Flotilla is still working. Stop it and close? A half-made copy is removed.", Program.Title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            e.Cancel = true;  // RunBusy closes the window once the work has stopped and cleaned up
            if (answer != DialogResult.Yes) return;
            closeWhenDone = true;
            busy.Cancel();
        }

        void Say(string text) => status.Text = text;

        void Warn(string text) => MessageBox.Show(this, text, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
