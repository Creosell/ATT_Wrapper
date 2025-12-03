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
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.btnCommon = new System.Windows.Forms.Button();
            this.btnSpecial = new System.Windows.Forms.Button();
            this.btnCommonOffline = new System.Windows.Forms.Button();
            this.taskKillBtn = new System.Windows.Forms.Button();
            this.dgvResults = new System.Windows.Forms.DataGridView();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.tabControlOutput = new System.Windows.Forms.TabControl();
            this.tabSimple = new System.Windows.Forms.TabPage();
            this.tabExpert = new System.Windows.Forms.TabPage();
            this.rtbLog = new System.Windows.Forms.RichTextBox();
            this.tabControlActions = new System.Windows.Forms.TabControl();
            this.main = new System.Windows.Forms.TabPage();
            this.dev = new System.Windows.Forms.TabPage();
            this.mainLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.flowLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).BeginInit();
            this.statusStrip1.SuspendLayout();
            this.tabControlOutput.SuspendLayout();
            this.tabSimple.SuspendLayout();
            this.tabExpert.SuspendLayout();
            this.tabControlActions.SuspendLayout();
            this.main.SuspendLayout();
            this.mainLayoutPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.btnUpdate);
            this.flowLayoutPanel1.Controls.Add(this.btnCommon);
            this.flowLayoutPanel1.Controls.Add(this.btnSpecial);
            this.flowLayoutPanel1.Controls.Add(this.btnCommonOffline);
            this.flowLayoutPanel1.Controls.Add(this.taskKillBtn);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(3, 3);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(183, 483);
            this.flowLayoutPanel1.TabIndex = 0;
            // 
            // btnUpdate
            // 
            this.btnUpdate.Location = new System.Drawing.Point(3, 3);
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.Size = new System.Drawing.Size(136, 47);
            this.btnUpdate.TabIndex = 4;
            this.btnUpdate.Text = "Update";
            this.btnUpdate.UseVisualStyleBackColor = true;
            this.btnUpdate.Click += new System.EventHandler(this.btnUpdate_Click);
            // 
            // btnCommon
            // 
            this.btnCommon.Location = new System.Drawing.Point(3, 56);
            this.btnCommon.Name = "btnCommon";
            this.btnCommon.Size = new System.Drawing.Size(136, 47);
            this.btnCommon.TabIndex = 5;
            this.btnCommon.Text = "Common";
            this.btnCommon.UseVisualStyleBackColor = true;
            this.btnCommon.Click += new System.EventHandler(this.btnCommon_Click);
            // 
            // btnSpecial
            // 
            this.btnSpecial.Location = new System.Drawing.Point(3, 109);
            this.btnSpecial.Name = "btnSpecial";
            this.btnSpecial.Size = new System.Drawing.Size(136, 47);
            this.btnSpecial.TabIndex = 6;
            this.btnSpecial.Text = "Special";
            this.btnSpecial.UseVisualStyleBackColor = true;
            this.btnSpecial.Click += new System.EventHandler(this.btnSpecial_Click);
            // 
            // btnCommonOffline
            // 
            this.btnCommonOffline.Location = new System.Drawing.Point(3, 162);
            this.btnCommonOffline.Name = "btnCommonOffline";
            this.btnCommonOffline.Size = new System.Drawing.Size(136, 47);
            this.btnCommonOffline.TabIndex = 7;
            this.btnCommonOffline.Text = "Common (Offline)";
            this.btnCommonOffline.UseVisualStyleBackColor = true;
            this.btnCommonOffline.Click += new System.EventHandler(this.btnCommonOffline_Click);
            // 
            // taskKillBtn
            // 
            this.taskKillBtn.Location = new System.Drawing.Point(3, 215);
            this.taskKillBtn.Name = "taskKillBtn";
            this.taskKillBtn.Size = new System.Drawing.Size(136, 47);
            this.taskKillBtn.TabIndex = 8;
            this.taskKillBtn.Text = "Kill task";
            this.taskKillBtn.UseVisualStyleBackColor = true;
            this.taskKillBtn.Click += new System.EventHandler(this.taskKillBtn_Click);
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
            this.dgvResults.RowTemplate.Height = 24;
            this.dgvResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvResults.Size = new System.Drawing.Size(951, 500);
            this.dgvResults.TabIndex = 1;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip1.Location = new System.Drawing.Point(0, 528);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(815, 24);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            this.statusLabel.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(56, 18);
            this.statusLabel.Text = "Status";
            // 
            // tabControlOutput
            // 
            this.tabControlOutput.Controls.Add(this.tabSimple);
            this.tabControlOutput.Controls.Add(this.tabExpert);
            this.tabControlOutput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlOutput.Font = new System.Drawing.Font("Segoe UI", 12F);
            this.tabControlOutput.Location = new System.Drawing.Point(206, 3);
            this.tabControlOutput.Name = "tabControlOutput";
            this.tabControlOutput.SelectedIndex = 0;
            this.tabControlOutput.Size = new System.Drawing.Size(606, 527);
            this.tabControlOutput.TabIndex = 3;
            // 
            // tabSimple
            // 
            this.tabSimple.Controls.Add(this.dgvResults);
            this.tabSimple.Location = new System.Drawing.Point(4, 37);
            this.tabSimple.Name = "tabSimple";
            this.tabSimple.Padding = new System.Windows.Forms.Padding(3);
            this.tabSimple.Size = new System.Drawing.Size(816, 491);
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
            this.tabExpert.Size = new System.Drawing.Size(598, 486);
            this.tabExpert.TabIndex = 1;
            this.tabExpert.Text = "Expert";
            this.tabExpert.UseVisualStyleBackColor = true;
            // 
            // rtbLog
            // 
            this.rtbLog.BackColor = System.Drawing.Color.White;
            this.rtbLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbLog.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtbLog.Location = new System.Drawing.Point(3, 3);
            this.rtbLog.Name = "rtbLog";
            this.rtbLog.ReadOnly = true;
            this.rtbLog.Size = new System.Drawing.Size(592, 480);
            this.rtbLog.TabIndex = 0;
            this.rtbLog.Text = "";
            // 
            // tabControlActions
            // 
            this.tabControlActions.Controls.Add(this.main);
            this.tabControlActions.Controls.Add(this.dev);
            this.tabControlActions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlActions.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.tabControlActions.Location = new System.Drawing.Point(3, 3);
            this.tabControlActions.Name = "tabControlActions";
            this.tabControlActions.SelectedIndex = 0;
            this.tabControlActions.Size = new System.Drawing.Size(197, 527);
            this.tabControlActions.TabIndex = 4;
            // 
            // main
            // 
            this.main.Controls.Add(this.flowLayoutPanel1);
            this.main.Location = new System.Drawing.Point(4, 34);
            this.main.Name = "main";
            this.main.Padding = new System.Windows.Forms.Padding(3);
            this.main.Size = new System.Drawing.Size(189, 489);
            this.main.TabIndex = 0;
            this.main.Text = "Main";
            this.main.UseVisualStyleBackColor = true;
            // 
            // dev
            // 
            this.dev.Location = new System.Drawing.Point(4, 34);
            this.dev.Name = "dev";
            this.dev.Padding = new System.Windows.Forms.Padding(3);
            this.dev.Size = new System.Drawing.Size(297, 490);
            this.dev.TabIndex = 1;
            this.dev.Text = "Dev";
            this.dev.UseVisualStyleBackColor = true;
            // 
            // mainLayoutPanel
            // 
            this.mainLayoutPanel.ColumnCount = 2;
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.mainLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 75F));
            this.mainLayoutPanel.Controls.Add(this.tabControlActions, 0, 0);
            this.mainLayoutPanel.Controls.Add(this.tabControlOutput, 1, 0);
            this.mainLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.mainLayoutPanel.Name = "mainLayoutPanel";
            this.mainLayoutPanel.RowCount = 1;
            this.mainLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainLayoutPanel.Size = new System.Drawing.Size(815, 533);
            this.mainLayoutPanel.TabIndex = 5;
            // 
            // JatlasTestRunnerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(978, 663);
            this.Controls.Add(this.mainLayoutPanel);
            this.Controls.Add(this.statusStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "JatlasTestRunnerForm";
            this.Text = "ATT Runner";
            this.flowLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.tabControlOutput.ResumeLayout(false);
            this.tabSimple.ResumeLayout(false);
            this.tabExpert.ResumeLayout(false);
            this.tabControlActions.ResumeLayout(false);
            this.main.ResumeLayout(false);
            this.mainLayoutPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

            }

        #endregion
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.Button btnCommon;
        private System.Windows.Forms.Button btnSpecial;
        private System.Windows.Forms.Button btnCommonOffline;
        private System.Windows.Forms.DataGridView dgvResults;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.Button taskKillBtn;
        private System.Windows.Forms.TabControl tabControlOutput;
        private System.Windows.Forms.TabPage tabSimple;
        private System.Windows.Forms.TabPage tabExpert;
        private System.Windows.Forms.RichTextBox rtbLog;
        private System.Windows.Forms.TabControl tabControlActions;
        private System.Windows.Forms.TabPage main;
        private System.Windows.Forms.TabPage dev;
        private System.Windows.Forms.TableLayoutPanel mainLayoutPanel;
        }
    }