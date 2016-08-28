﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Collections;

namespace SteamCloudFileManager
{
    public partial class MainForm : Form
    {
        IRemoteStorage storage;
        int sortColumn;

        public MainForm()
        {
            InitializeComponent();
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            try
            {
                uint appId;
                if (string.IsNullOrWhiteSpace(appIdTextBox.Text))
                {
                    MessageBox.Show(this, "Please enter an App ID.", "Failed to connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (!uint.TryParse(appIdTextBox.Text.Trim(), out appId))
                {
                    MessageBox.Show(this, "Please make sure the App ID you entered is valid.", "Failed to connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                storage = RemoteStorage.CreateInstance(uint.Parse(appIdTextBox.Text));
                //storage = new RemoteStorageLocal("remote", uint.Parse(appIdTextBox.Text));
                refreshButton.Enabled = true;
                addButton.Enabled = true;
                refreshButton_Click(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Failed to connect", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            if (storage == null)
            {
                MessageBox.Show(this, "Not connected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            remoteListView.BeginUpdate();
            try
            {
                List<IRemoteFile> files = storage.GetFiles();
                remoteListView.Items.Clear();
                foreach (IRemoteFile file in files)
                {
                    ListViewItem itm = new ListViewItem();
                    itm.SubItems[0].Text = file.Name;
                    itm.SubItems[0].Tag = file.Name;
                    itm.SubItems.Add(new ListViewItem.ListViewSubItem(itm, file.Timestamp.ToString()) { Tag = file.Timestamp });
                    itm.SubItems.Add(new ListViewItem.ListViewSubItem(itm, file.Size.ToString()) { Tag = file.Size });
                    itm.SubItems.Add(new ListViewItem.ListViewSubItem(itm, file.IsPersisted.ToString()) { Tag = file.IsPersisted });
                    itm.SubItems.Add(new ListViewItem.ListViewSubItem(itm, file.Exists.ToString()) { Tag = file.Exists });
                    itm.Tag = file;
                    remoteListView.Items.Add(itm);
                }
                updateQuota();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Can't refresh." + Environment.NewLine + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            remoteListView.EndUpdate();
        }

        void updateQuota()
        {
            if (storage == null) throw new InvalidOperationException("Not connected");
            int totalBytes, availBytes;
            storage.GetQuota(out totalBytes, out availBytes);
            var numFiles = remoteListView.Items.Count;
            quotaLabel.Text = string.Format("{2} files, {0}/{1} bytes used", totalBytes - availBytes, totalBytes, numFiles);
        }

        private void downloadButton_Click(object sender, EventArgs e)
        {
            if (storage == null)
            {
                MessageBox.Show(this, "Not connected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (remoteListView.SelectedIndices.Count != 1)
            {
                MessageBox.Show(this, "Please select only one file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            IRemoteFile file = remoteListView.SelectedItems[0].Tag as IRemoteFile;
            saveFileDialog1.FileName = Path.GetFileName(file.Name);
            if (saveFileDialog1.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    File.WriteAllBytes(saveFileDialog1.FileName, file.ReadAllBytes());
                    MessageBox.Show(this, "File downloaded.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "File download failed." + Environment.NewLine + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
        }

        private void deleteButton_Click(object sender, EventArgs e)
        {
            if (storage == null)
            {
                MessageBox.Show(this, "Not connected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (remoteListView.SelectedIndices.Count == 0)
            {
                MessageBox.Show(this, "Please select files to delete.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            if (MessageBox.Show(this, "Are you sure you want to delete the selected files?", "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == System.Windows.Forms.DialogResult.No) return;

            bool allSuccess = true;
            remoteListView.BeginUpdate();
            foreach (ListViewItem item in remoteListView.SelectedItems)
            {
                IRemoteFile file = item.Tag as IRemoteFile;
                try
                {
                    bool success = file.Delete();
                    if (!success)
                    {
                        allSuccess = false;
                        MessageBox.Show(this, file.Name + " failed to delete.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                    else
                    {
                        item.Remove();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, file.Name + " failed to delete." + Environment.NewLine + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
            remoteListView.EndUpdate();

            updateQuota();
            if (allSuccess) MessageBox.Show(this, "Files deleted.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void remoteListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            downloadButton.Enabled = deleteButton.Enabled = (storage != null && remoteListView.SelectedIndices.Count > 0);
        }

        private class ListViewItemComparer : IComparer
        {
            private int column;
            private SortOrder sortOrder;

            public ListViewItemComparer(int column, SortOrder sortOrder)
            {
                this.column = column;
                this.sortOrder = sortOrder;
            }

            public int Compare(object x, object y) 
            {
                var xx = ((ListViewItem)x).SubItems[column];
                var yy = ((ListViewItem)y).SubItems[column];
                var a = xx.Tag as IComparable;
                var b = yy.Tag as IComparable;
                var order = (sortOrder == SortOrder.Ascending ? 1 : -1);
                if (a != null && b != null)
                    return a.CompareTo(b) * order;

                // If the userdata isnt IComparable just fall back to string compare.
                return String.Compare(xx.Text, yy.Text) * order;
            }
        };

        private void remoteListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (sortColumn != e.Column)
            {
                // If column is different to last column just sort ascending.
                sortColumn = e.Column;
                remoteListView.Sorting = SortOrder.Ascending;
            }
            else
            {
                // Otherwise toggle between ascending/descending.
                remoteListView.Sorting = remoteListView.Sorting == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }

            remoteListView.SetSortIcon(e.Column, remoteListView.Sorting);
            remoteListView.ListViewItemSorter = new ListViewItemComparer(e.Column, remoteListView.Sorting);
            remoteListView.Sort();
        }

        private void addButton_Click(object sender, EventArgs e)
        {
            if (storage == null)
            {
                MessageBox.Show(this, "Not connected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            
            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var fileName in openFileDialog1.FileNames)
                {
                    CreateFile(fileName);
                }
            }
            refreshButton_Click(this, EventArgs.Empty);
        }

        private void CreateFile(string path)
        {
            var name = Path.GetFileName(path);
            var remoteFile = storage.GetFile(name);
            var bytes = File.ReadAllBytes(path);
            remoteFile.WriteAllBytes(bytes);
        }

        private void appIdTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                e.Handled = true;
                connectButton_Click(this, EventArgs.Empty);
            }
        }
    }
}
