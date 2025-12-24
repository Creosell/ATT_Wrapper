namespace ATT_Wrapper
    {
    partial class JatlasTestRunnerForm
        {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
            {
            if (disposing && ( components != null ))
                {
                components.Dispose();
                }
            base.Dispose(disposing);
            }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
            {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(JatlasTestRunnerForm));
            this.btnUpdate = new MaterialSkin.Controls.MaterialButton();
            this.btnCommon = new MaterialSkin.Controls.MaterialButton();
            this.btnSpecial = new MaterialSkin.Controls.MaterialButton();
            this.dgvResults = new System.Windows.Forms.DataGridView();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.tabControlOutput = new System.Windows.Forms.TabControl();
            this.tabSimple = new System.Windows.Forms.TabPage();
            this.tabExpert = new System.Windows.Forms.TabPage();
            this.rtbLog = new System.Windows.Forms.RichTextBox();
            this.tabControlActions = new System.Windows.Forms.TabControl();
            this.mainButtonsTab = new System.Windows.Forms.TabPage();
            this.mainButtonsLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnAging = new MaterialSkin.Controls.MaterialButton();
            this.mockReport = new MaterialSkin.Controls.MaterialButton();
            this.taskKillBtnMain = new MaterialSkin.Controls.MaterialButton();
            this.extraButtonsTab = new System.Windows.Forms.TabPage();
            this.extraButtonsLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.btnCommonOffline = new MaterialSkin.Controls.MaterialButton();
            this.taskKillBtnExtra = new MaterialSkin.Controls.MaterialButton();
            this.formTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.tabControlOutput.SuspendLayout();
            this.tabSimple.SuspendLayout();
            this.tabExpert.SuspendLayout();
            this.tabControlActions.SuspendLayout();
            this.mainButtonsTab.SuspendLayout();
            this.mainButtonsLayoutPanel.SuspendLayout();
            this.extraButtonsTab.SuspendLayout();
            this.extraButtonsLayoutPanel.SuspendLayout();
            this.formTableLayoutPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnUpdate
            // 
            this.btnUpdate.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnUpdate.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.btnUpdate.Depth = 0;
            this.btnUpdate.HighEmphasis = true;
            this.btnUpdate.Icon = null;
            this.btnUpdate.Location = new System.Drawing.Point(4, 6);
            this.btnUpdate.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.btnUpdate.MouseState = MaterialSkin.MouseState.HOVER;
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.NoAccentTextColor = System.Drawing.Color.Empty;
            this.btnUpdate.Size = new System.Drawing.Size(77, 36);
            this.btnUpdate.TabIndex = 4;
            this.btnUpdate.TabStop = false;
            this.btnUpdate.Text = "Update";
            this.btnUpdate.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.btnUpdate.UseAccentColor = false;
            this.btnUpdate.UseVisualStyleBackColor = true;
            this.btnUpdate.Click += new System.EventHandler(this.UpdateATT);
            // 
            // btnCommon
            // 
            this.btnCommon.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnCommon.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.btnCommon.Depth = 0;
            this.btnCommon.HighEmphasis = true;
            this.btnCommon.Icon = null;
            this.btnCommon.Location = new System.Drawing.Point(4, 54);
            this.btnCommon.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.btnCommon.MouseState = MaterialSkin.MouseState.HOVER;
            this.btnCommon.Name = "btnCommon";
            this.btnCommon.NoAccentTextColor = System.Drawing.Color.Empty;
            this.btnCommon.Size = new System.Drawing.Size(88, 36);
            this.btnCommon.TabIndex = 5;
            this.btnCommon.TabStop = false;
            this.btnCommon.Text = "Common";
            this.btnCommon.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.btnCommon.UseAccentColor = false;
            this.btnCommon.UseVisualStyleBackColor = true;
            this.btnCommon.Click += new System.EventHandler(this.CommonATT);
            // 
            // btnSpecial
            // 
            this.btnSpecial.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnSpecial.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.btnSpecial.Depth = 0;
            this.btnSpecial.HighEmphasis = true;
            this.btnSpecial.Icon = null;
            this.btnSpecial.Location = new System.Drawing.Point(4, 102);
            this.btnSpecial.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.btnSpecial.MouseState = MaterialSkin.MouseState.HOVER;
            this.btnSpecial.Name = "btnSpecial";
            this.btnSpecial.NoAccentTextColor = System.Drawing.Color.Empty;
            this.btnSpecial.Size = new System.Drawing.Size(80, 36);
            this.btnSpecial.TabIndex = 6;
            this.btnSpecial.TabStop = false;
            this.btnSpecial.Text = "Special";
            this.btnSpecial.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.btnSpecial.UseAccentColor = false;
            this.btnSpecial.UseVisualStyleBackColor = true;
            this.btnSpecial.Click += new System.EventHandler(this.SpecialATT);
            // 
            // dgvResults
            // 
            this.dgvResults.AllowUserToAddRows = false;
            this.dgvResults.AllowUserToDeleteRows = false;
            this.dgvResults.AllowUserToResizeColumns = false;
            this.dgvResults.AllowUserToResizeRows = false;
            this.dgvResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvResults.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvResults.Location = new System.Drawing.Point(3, 3);
            this.dgvResults.Name = "dgvResults";
            this.dgvResults.ReadOnly = true;
            this.dgvResults.RowHeadersVisible = false;
            this.dgvResults.RowHeadersWidth = 51;
            this.dgvResults.RowTemplate.Height = 12;
            this.dgvResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvResults.Size = new System.Drawing.Size(601, 516);
            this.dgvResults.TabIndex = 1;
            this.dgvResults.TabStop = false;
            // 
            // statusStrip
            // 
            this.statusStrip.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(3, 633);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(827, 31);
            this.statusStrip.TabIndex = 2;
            this.statusStrip.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            this.statusLabel.Font = new System.Drawing.Font("Segoe UI", 11F);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(62, 25);
            this.statusLabel.Text = "Ready";
            // 
            // tabControlOutput
            // 
            this.tabControlOutput.Controls.Add(this.tabSimple);
            this.tabControlOutput.Controls.Add(this.tabExpert);
            this.tabControlOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlOutput.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.tabControlOutput.Location = new System.Drawing.Point(209, 3);
            this.tabControlOutput.Name = "tabControlOutput";
            this.tabControlOutput.SelectedIndex = 0;
            this.tabControlOutput.Size = new System.Drawing.Size(615, 563);
            this.tabControlOutput.TabIndex = 3;
            this.tabControlOutput.TabStop = false;
            // 
            // tabSimple
            // 
            this.tabSimple.Controls.Add(this.dgvResults);
            this.tabSimple.Location = new System.Drawing.Point(4, 37);
            this.tabSimple.Name = "tabSimple";
            this.tabSimple.Padding = new System.Windows.Forms.Padding(3);
            this.tabSimple.Size = new System.Drawing.Size(607, 522);
            this.tabSimple.TabIndex = 0;
            this.tabSimple.Text = "Simple";
            this.tabSimple.UseVisualStyleBackColor = true;
            // 
            // tabExpert
            // 
            this.tabExpert.Controls.Add(this.rtbLog);
            this.tabExpert.Location = new System.Drawing.Point(4, 37);
            this.tabExpert.Name = "tabExpert";
            this.tabExpert.Padding = new System.Windows.Forms.Padding(3);
            this.tabExpert.Size = new System.Drawing.Size(607, 522);
            this.tabExpert.TabIndex = 1;
            this.tabExpert.Text = "Expert";
            this.tabExpert.UseVisualStyleBackColor = true;
            // 
            // rtbLog
            // 
            this.rtbLog.BackColor = System.Drawing.Color.White;
            this.rtbLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbLog.Font = new System.Drawing.Font("Cascadia Mono SemiBold", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtbLog.Location = new System.Drawing.Point(3, 3);
            this.rtbLog.Name = "rtbLog";
            this.rtbLog.ReadOnly = true;
            this.rtbLog.Size = new System.Drawing.Size(601, 516);
            this.rtbLog.TabIndex = 0;
            this.rtbLog.TabStop = false;
            this.rtbLog.Text = "";
            // 
            // tabControlActions
            // 
            this.tabControlActions.Controls.Add(this.mainButtonsTab);
            this.tabControlActions.Controls.Add(this.extraButtonsTab);
            this.tabControlActions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlActions.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.tabControlActions.Location = new System.Drawing.Point(3, 3);
            this.tabControlActions.Name = "tabControlActions";
            this.tabControlActions.SelectedIndex = 0;
            this.tabControlActions.Size = new System.Drawing.Size(200, 563);
            this.tabControlActions.TabIndex = 4;
            this.tabControlActions.TabStop = false;
            // 
            // mainButtonsTab
            // 
            this.mainButtonsTab.Controls.Add(this.mainButtonsLayoutPanel);
            this.mainButtonsTab.Location = new System.Drawing.Point(4, 34);
            this.mainButtonsTab.Name = "mainButtonsTab";
            this.mainButtonsTab.Padding = new System.Windows.Forms.Padding(3);
            this.mainButtonsTab.Size = new System.Drawing.Size(192, 525);
            this.mainButtonsTab.TabIndex = 0;
            this.mainButtonsTab.Text = "Main";
            this.mainButtonsTab.UseVisualStyleBackColor = true;
            // 
            // mainButtonsLayoutPanel
            // 
            this.mainButtonsLayoutPanel.AutoScroll = true;
            this.mainButtonsLayoutPanel.Controls.Add(this.btnUpdate);
            this.mainButtonsLayoutPanel.Controls.Add(this.btnCommon);
            this.mainButtonsLayoutPanel.Controls.Add(this.btnSpecial);
            this.mainButtonsLayoutPanel.Controls.Add(this.btnAging);
            this.mainButtonsLayoutPanel.Controls.Add(this.mockReport);
            this.mainButtonsLayoutPanel.Controls.Add(this.taskKillBtnMain);
            this.mainButtonsLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainButtonsLayoutPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.mainButtonsLayoutPanel.Location = new System.Drawing.Point(3, 3);
            this.mainButtonsLayoutPanel.Name = "mainButtonsLayoutPanel";
            this.mainButtonsLayoutPanel.Size = new System.Drawing.Size(186, 519);
            this.mainButtonsLayoutPanel.TabIndex = 9;
            this.mainButtonsLayoutPanel.WrapContents = false;
            // 
            // btnAging
            // 
            this.btnAging.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnAging.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.btnAging.Depth = 0;
            this.btnAging.HighEmphasis = true;
            this.btnAging.Icon = null;
            this.btnAging.Location = new System.Drawing.Point(4, 150);
            this.btnAging.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.btnAging.MouseState = MaterialSkin.MouseState.HOVER;
            this.btnAging.Name = "btnAging";
            this.btnAging.NoAccentTextColor = System.Drawing.Color.Empty;
            this.btnAging.Size = new System.Drawing.Size(66, 36);
            this.btnAging.TabIndex = 11;
            this.btnAging.TabStop = false;
            this.btnAging.Text = "Aging";
            this.btnAging.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.btnAging.UseAccentColor = false;
            this.btnAging.UseVisualStyleBackColor = true;
            this.btnAging.Click += new System.EventHandler(this.AgingATT);
            // 
            // mockReport
            // 
            this.mockReport.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.mockReport.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.mockReport.Depth = 0;
            this.mockReport.HighEmphasis = true;
            this.mockReport.Icon = null;
            this.mockReport.Location = new System.Drawing.Point(4, 198);
            this.mockReport.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.mockReport.MouseState = MaterialSkin.MouseState.HOVER;
            this.mockReport.Name = "mockReport";
            this.mockReport.NoAccentTextColor = System.Drawing.Color.Empty;
            this.mockReport.Size = new System.Drawing.Size(122, 36);
            this.mockReport.TabIndex = 16;
            this.mockReport.TabStop = false;
            this.mockReport.Text = "Mock report";
            this.mockReport.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.mockReport.UseAccentColor = false;
            this.mockReport.UseVisualStyleBackColor = true;
            this.mockReport.Click += new System.EventHandler(this.MockReportATT);
            // 
            // taskKillBtnMain
            // 
            this.taskKillBtnMain.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.taskKillBtnMain.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.taskKillBtnMain.Depth = 0;
            this.taskKillBtnMain.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.taskKillBtnMain.HighEmphasis = true;
            this.taskKillBtnMain.Icon = null;
            this.taskKillBtnMain.Location = new System.Drawing.Point(4, 246);
            this.taskKillBtnMain.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.taskKillBtnMain.MouseState = MaterialSkin.MouseState.HOVER;
            this.taskKillBtnMain.Name = "taskKillBtnMain";
            this.taskKillBtnMain.NoAccentTextColor = System.Drawing.Color.Empty;
            this.taskKillBtnMain.Size = new System.Drawing.Size(122, 36);
            this.taskKillBtnMain.TabIndex = 13;
            this.taskKillBtnMain.TabStop = false;
            this.taskKillBtnMain.Text = "Kill task";
            this.taskKillBtnMain.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.taskKillBtnMain.UseAccentColor = false;
            this.taskKillBtnMain.UseVisualStyleBackColor = true;
            this.taskKillBtnMain.Click += new System.EventHandler(this.KillTask);
            // 
            // extraButtonsTab
            // 
            this.extraButtonsTab.Controls.Add(this.extraButtonsLayoutPanel);
            this.extraButtonsTab.Location = new System.Drawing.Point(4, 34);
            this.extraButtonsTab.Name = "extraButtonsTab";
            this.extraButtonsTab.Padding = new System.Windows.Forms.Padding(3);
            this.extraButtonsTab.Size = new System.Drawing.Size(192, 525);
            this.extraButtonsTab.TabIndex = 1;
            this.extraButtonsTab.Text = "Extra";
            this.extraButtonsTab.UseVisualStyleBackColor = true;
            // 
            // extraButtonsLayoutPanel
            // 
            this.extraButtonsLayoutPanel.AutoScroll = true;
            this.extraButtonsLayoutPanel.Controls.Add(this.btnCommonOffline);
            this.extraButtonsLayoutPanel.Controls.Add(this.taskKillBtnExtra);
            this.extraButtonsLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.extraButtonsLayoutPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.extraButtonsLayoutPanel.Location = new System.Drawing.Point(3, 3);
            this.extraButtonsLayoutPanel.Name = "extraButtonsLayoutPanel";
            this.extraButtonsLayoutPanel.Size = new System.Drawing.Size(186, 519);
            this.extraButtonsLayoutPanel.TabIndex = 0;
            this.extraButtonsLayoutPanel.WrapContents = false;
            // 
            // btnCommonOffline
            // 
            this.btnCommonOffline.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.btnCommonOffline.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.btnCommonOffline.Depth = 0;
            this.btnCommonOffline.HighEmphasis = true;
            this.btnCommonOffline.Icon = null;
            this.btnCommonOffline.Location = new System.Drawing.Point(4, 6);
            this.btnCommonOffline.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.btnCommonOffline.MouseState = MaterialSkin.MouseState.HOVER;
            this.btnCommonOffline.Name = "btnCommonOffline";
            this.btnCommonOffline.NoAccentTextColor = System.Drawing.Color.Empty;
            this.btnCommonOffline.Size = new System.Drawing.Size(160, 36);
            this.btnCommonOffline.TabIndex = 8;
            this.btnCommonOffline.Text = "Common (Offline)";
            this.btnCommonOffline.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.btnCommonOffline.UseAccentColor = false;
            this.btnCommonOffline.UseVisualStyleBackColor = true;
            this.btnCommonOffline.Click += new System.EventHandler(this.CommonOfflineATT);
            // 
            // taskKillBtnExtra
            // 
            this.taskKillBtnExtra.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.taskKillBtnExtra.Density = MaterialSkin.Controls.MaterialButton.MaterialButtonDensity.Default;
            this.taskKillBtnExtra.Depth = 0;
            this.taskKillBtnExtra.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.taskKillBtnExtra.ForeColor = System.Drawing.SystemColors.ControlText;
            this.taskKillBtnExtra.HighEmphasis = true;
            this.taskKillBtnExtra.Icon = null;
            this.taskKillBtnExtra.Location = new System.Drawing.Point(4, 54);
            this.taskKillBtnExtra.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.taskKillBtnExtra.MouseState = MaterialSkin.MouseState.HOVER;
            this.taskKillBtnExtra.Name = "taskKillBtnExtra";
            this.taskKillBtnExtra.NoAccentTextColor = System.Drawing.Color.Empty;
            this.taskKillBtnExtra.Size = new System.Drawing.Size(160, 36);
            this.taskKillBtnExtra.TabIndex = 14;
            this.taskKillBtnExtra.Text = "Kill task";
            this.taskKillBtnExtra.Type = MaterialSkin.Controls.MaterialButton.MaterialButtonType.Contained;
            this.taskKillBtnExtra.UseAccentColor = false;
            this.taskKillBtnExtra.UseVisualStyleBackColor = true;
            this.taskKillBtnExtra.Click += new System.EventHandler(this.KillTask);
            // 
            // formTableLayoutPanel
            // 
            this.formTableLayoutPanel.ColumnCount = 2;
            this.formTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.formTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 75F));
            this.formTableLayoutPanel.Controls.Add(this.tabControlActions, 0, 0);
            this.formTableLayoutPanel.Controls.Add(this.tabControlOutput, 1, 0);
            this.formTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formTableLayoutPanel.Location = new System.Drawing.Point(3, 64);
            this.formTableLayoutPanel.Name = "formTableLayoutPanel";
            this.formTableLayoutPanel.RowCount = 1;
            this.formTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.formTableLayoutPanel.Size = new System.Drawing.Size(827, 569);
            this.formTableLayoutPanel.TabIndex = 5;
            // 
            // JatlasTestRunnerForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(833, 667);
            this.Controls.Add(this.formTableLayoutPanel);
            this.Controls.Add(this.statusStrip);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "JatlasTestRunnerForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ATT Runner";
            this.Load += new System.EventHandler(this.JatlasTestRunnerForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.tabControlOutput.ResumeLayout(false);
            this.tabSimple.ResumeLayout(false);
            this.tabExpert.ResumeLayout(false);
            this.tabControlActions.ResumeLayout(false);
            this.mainButtonsTab.ResumeLayout(false);
            this.mainButtonsLayoutPanel.ResumeLayout(false);
            this.mainButtonsLayoutPanel.PerformLayout();
            this.extraButtonsTab.ResumeLayout(false);
            this.extraButtonsLayoutPanel.ResumeLayout(false);
            this.extraButtonsLayoutPanel.PerformLayout();
            this.formTableLayoutPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

            }

        #endregion
        private MaterialSkin.Controls.MaterialButton btnUpdate;
        private MaterialSkin.Controls.MaterialButton btnCommon;
        private MaterialSkin.Controls.MaterialButton btnSpecial;
        private System.Windows.Forms.DataGridView dgvResults;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.TabControl tabControlOutput;
        private System.Windows.Forms.TabPage tabSimple;
        private System.Windows.Forms.TabPage tabExpert;
        private System.Windows.Forms.RichTextBox rtbLog;
        private System.Windows.Forms.TabControl tabControlActions;
        private System.Windows.Forms.TabPage mainButtonsTab;
        private System.Windows.Forms.TabPage extraButtonsTab;
        private System.Windows.Forms.TableLayoutPanel formTableLayoutPanel;
        private System.Windows.Forms.FlowLayoutPanel mainButtonsLayoutPanel;
        private System.Windows.Forms.FlowLayoutPanel extraButtonsLayoutPanel;
        private MaterialSkin.Controls.MaterialButton btnAging;
        private MaterialSkin.Controls.MaterialButton taskKillBtnMain;
        private MaterialSkin.Controls.MaterialButton btnCommonOffline;
        private MaterialSkin.Controls.MaterialButton taskKillBtnExtra;
        private MaterialSkin.Controls.MaterialButton mockReport;
        }
    }