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
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.btnCommon = new System.Windows.Forms.Button();
            this.btnSpecial = new System.Windows.Forms.Button();
            this.btnCommonOffline = new System.Windows.Forms.Button();
            this.dgvResults = new System.Windows.Forms.DataGridView();
            this.flowLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).BeginInit();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.btnUpdate);
            this.flowLayoutPanel1.Controls.Add(this.btnCommon);
            this.flowLayoutPanel1.Controls.Add(this.btnSpecial);
            this.flowLayoutPanel1.Controls.Add(this.btnCommonOffline);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(12, 12);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(139, 410);
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
            // dgvResults
            // 
            this.dgvResults.AllowUserToAddRows = false;
            this.dgvResults.AllowUserToDeleteRows = false;
            this.dgvResults.AllowUserToResizeColumns = false;
            this.dgvResults.AllowUserToResizeRows = false;
            this.dgvResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvResults.Location = new System.Drawing.Point(171, 18);
            this.dgvResults.Name = "dgvResults";
            this.dgvResults.ReadOnly = true;
            this.dgvResults.RowHeadersWidth = 51;
            this.dgvResults.RowTemplate.Height = 24;
            this.dgvResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvResults.Size = new System.Drawing.Size(647, 403);
            this.dgvResults.TabIndex = 1;
            // 
            // JatlasTestRunnerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(870, 459);
            this.Controls.Add(this.dgvResults);
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "JatlasTestRunnerForm";
            this.Text = "JatlasTestRunnerForm";
            this.flowLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).EndInit();
            this.ResumeLayout(false);

            }

        #endregion
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.Button btnCommon;
        private System.Windows.Forms.Button btnSpecial;
        private System.Windows.Forms.Button btnCommonOffline;
        private System.Windows.Forms.DataGridView dgvResults;
        }
    }