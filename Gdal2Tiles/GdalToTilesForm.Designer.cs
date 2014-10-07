namespace Gdal2Tiles
{
    partial class GdalToTilesForm
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
            if (disposing && (components != null))
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
            this.btnSelectImg = new System.Windows.Forms.Button();
            this.btnSelectPath = new System.Windows.Forms.Button();
            this.cmbBxBand1 = new System.Windows.Forms.ComboBox();
            this.cmbBxBand2 = new System.Windows.Forms.ComboBox();
            this.cmbBxBand3 = new System.Windows.Forms.ComboBox();
            this.cmBxSD = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.grpBxOptions = new System.Windows.Forms.GroupBox();
            this.btPreview = new System.Windows.Forms.Button();
            this.ckOrder = new System.Windows.Forms.CheckBox();
            this.ckStrech = new System.Windows.Forms.CheckBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.cmBxSize = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtBxStatus = new System.Windows.Forms.TextBox();
            this.pctBoxImg = new System.Windows.Forms.PictureBox();
            this.treeViewDescript = new System.Windows.Forms.TreeView();
            this.label6 = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.grpBxOptions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pctBoxImg)).BeginInit();
            this.SuspendLayout();
            // 
            // btnSelectImg
            // 
            this.btnSelectImg.Location = new System.Drawing.Point(6, 4);
            this.btnSelectImg.Name = "btnSelectImg";
            this.btnSelectImg.Size = new System.Drawing.Size(91, 23);
            this.btnSelectImg.TabIndex = 0;
            this.btnSelectImg.Text = "Select Image";
            this.btnSelectImg.UseVisualStyleBackColor = true;
            this.btnSelectImg.Click += new System.EventHandler(this.btnSelect_Click);
            // 
            // btnSelectPath
            // 
            this.btnSelectPath.Enabled = false;
            this.btnSelectPath.Location = new System.Drawing.Point(391, 4);
            this.btnSelectPath.Name = "btnSelectPath";
            this.btnSelectPath.Size = new System.Drawing.Size(105, 23);
            this.btnSelectPath.TabIndex = 3;
            this.btnSelectPath.Text = "Write tiles and  kml";
            this.btnSelectPath.UseVisualStyleBackColor = true;
            this.btnSelectPath.Click += new System.EventHandler(this.btnSelectPath_Click);
            // 
            // cmbBxBand1
            // 
            this.cmbBxBand1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbBxBand1.Enabled = false;
            this.cmbBxBand1.FormattingEnabled = true;
            this.cmbBxBand1.Location = new System.Drawing.Point(65, 65);
            this.cmbBxBand1.Name = "cmbBxBand1";
            this.cmbBxBand1.Size = new System.Drawing.Size(40, 21);
            this.cmbBxBand1.TabIndex = 7;
            // 
            // cmbBxBand2
            // 
            this.cmbBxBand2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbBxBand2.Enabled = false;
            this.cmbBxBand2.FormattingEnabled = true;
            this.cmbBxBand2.Location = new System.Drawing.Point(111, 65);
            this.cmbBxBand2.Name = "cmbBxBand2";
            this.cmbBxBand2.Size = new System.Drawing.Size(40, 21);
            this.cmbBxBand2.TabIndex = 8;
            // 
            // cmbBxBand3
            // 
            this.cmbBxBand3.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbBxBand3.Enabled = false;
            this.cmbBxBand3.FormattingEnabled = true;
            this.cmbBxBand3.Location = new System.Drawing.Point(157, 65);
            this.cmbBxBand3.Name = "cmbBxBand3";
            this.cmbBxBand3.Size = new System.Drawing.Size(40, 21);
            this.cmbBxBand3.TabIndex = 9;
            // 
            // cmBxSD
            // 
            this.cmBxSD.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmBxSD.Enabled = false;
            this.cmBxSD.FormattingEnabled = true;
            this.cmBxSD.Location = new System.Drawing.Point(200, 23);
            this.cmBxSD.Name = "cmBxSD";
            this.cmBxSD.Size = new System.Drawing.Size(40, 21);
            this.cmBxSD.TabIndex = 11;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(172, 26);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(22, 13);
            this.label2.TabIndex = 10;
            this.label2.Text = "SD";
            // 
            // grpBxOptions
            // 
            this.grpBxOptions.Controls.Add(this.btPreview);
            this.grpBxOptions.Controls.Add(this.ckOrder);
            this.grpBxOptions.Controls.Add(this.ckStrech);
            this.grpBxOptions.Controls.Add(this.textBox1);
            this.grpBxOptions.Controls.Add(this.cmBxSize);
            this.grpBxOptions.Controls.Add(this.cmBxSD);
            this.grpBxOptions.Controls.Add(this.label3);
            this.grpBxOptions.Controls.Add(this.label2);
            this.grpBxOptions.Controls.Add(this.label5);
            this.grpBxOptions.Controls.Add(this.label4);
            this.grpBxOptions.Controls.Add(this.label1);
            this.grpBxOptions.Controls.Add(this.cmbBxBand3);
            this.grpBxOptions.Controls.Add(this.cmbBxBand1);
            this.grpBxOptions.Controls.Add(this.cmbBxBand2);
            this.grpBxOptions.Enabled = false;
            this.grpBxOptions.Location = new System.Drawing.Point(251, 285);
            this.grpBxOptions.Name = "grpBxOptions";
            this.grpBxOptions.Size = new System.Drawing.Size(245, 126);
            this.grpBxOptions.TabIndex = 12;
            this.grpBxOptions.TabStop = false;
            this.grpBxOptions.Text = "Tiles Options";
            // 
            // btPreview
            // 
            this.btPreview.Location = new System.Drawing.Point(9, 97);
            this.btPreview.Name = "btPreview";
            this.btPreview.Size = new System.Drawing.Size(110, 23);
            this.btPreview.TabIndex = 12;
            this.btPreview.Text = "Preview Image";
            this.btPreview.UseVisualStyleBackColor = true;
            this.btPreview.Click += new System.EventHandler(this.btPreview_Click);
            // 
            // ckOrder
            // 
            this.ckOrder.AutoSize = true;
            this.ckOrder.Location = new System.Drawing.Point(9, 65);
            this.ckOrder.Name = "ckOrder";
            this.ckOrder.Size = new System.Drawing.Size(52, 17);
            this.ckOrder.TabIndex = 4;
            this.ckOrder.Text = "Order";
            this.ckOrder.UseVisualStyleBackColor = true;
            this.ckOrder.CheckedChanged += new System.EventHandler(this.ckOrder_CheckedChanged);
            // 
            // ckStrech
            // 
            this.ckStrech.AutoSize = true;
            this.ckStrech.Location = new System.Drawing.Point(117, 25);
            this.ckStrech.Name = "ckStrech";
            this.ckStrech.Size = new System.Drawing.Size(57, 17);
            this.ckStrech.TabIndex = 4;
            this.ckStrech.Text = "Strech";
            this.ckStrech.UseVisualStyleBackColor = true;
            this.ckStrech.CheckedChanged += new System.EventHandler(this.ckStrech_CheckedChanged);
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(0, 152);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.textBox1.Size = new System.Drawing.Size(283, 99);
            this.textBox1.TabIndex = 1;
            // 
            // cmBxSize
            // 
            this.cmBxSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmBxSize.FormattingEnabled = true;
            this.cmBxSize.Location = new System.Drawing.Point(56, 23);
            this.cmBxSize.Name = "cmBxSize";
            this.cmBxSize.Size = new System.Drawing.Size(46, 21);
            this.cmBxSize.TabIndex = 11;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(50, 13);
            this.label3.TabIndex = 10;
            this.label3.Text = "Size Tile ";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(170, 48);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(14, 13);
            this.label5.TabIndex = 6;
            this.label5.Text = "B";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(124, 48);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(15, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "G";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(78, 48);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(15, 13);
            this.label1.TabIndex = 6;
            this.label1.Text = "R";
            // 
            // txtBxStatus
            // 
            this.txtBxStatus.Location = new System.Drawing.Point(6, 285);
            this.txtBxStatus.Multiline = true;
            this.txtBxStatus.Name = "txtBxStatus";
            this.txtBxStatus.ReadOnly = true;
            this.txtBxStatus.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtBxStatus.Size = new System.Drawing.Size(243, 126);
            this.txtBxStatus.TabIndex = 1;
            // 
            // pctBoxImg
            // 
            this.pctBoxImg.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.pctBoxImg.Location = new System.Drawing.Point(251, 33);
            this.pctBoxImg.Name = "pctBoxImg";
            this.pctBoxImg.Size = new System.Drawing.Size(245, 245);
            this.pctBoxImg.TabIndex = 13;
            this.pctBoxImg.TabStop = false;
            // 
            // treeViewDescript
            // 
            this.treeViewDescript.Location = new System.Drawing.Point(6, 34);
            this.treeViewDescript.Name = "treeViewDescript";
            this.treeViewDescript.Size = new System.Drawing.Size(245, 228);
            this.treeViewDescript.TabIndex = 14;
            this.treeViewDescript.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.treeViewDescript_MouseDoubleClick);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(2, 269);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(236, 13);
            this.label6.TabIndex = 15;
            this.label6.Text = "*A double click on image information to clipboard";
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(6, 414);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(490, 21);
            this.progressBar1.TabIndex = 16;
            // 
            // GdalToTilesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(502, 438);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.treeViewDescript);
            this.Controls.Add(this.pctBoxImg);
            this.Controls.Add(this.grpBxOptions);
            this.Controls.Add(this.btnSelectPath);
            this.Controls.Add(this.txtBxStatus);
            this.Controls.Add(this.btnSelectImg);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "GdalToTilesForm";
            this.Text = "GdalToTilesWin";
            this.grpBxOptions.ResumeLayout(false);
            this.grpBxOptions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pctBoxImg)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Button btnSelectImg;
        private System.Windows.Forms.Button btnSelectPath;
        private System.Windows.Forms.ComboBox cmbBxBand1;
        private System.Windows.Forms.ComboBox cmbBxBand2;
        private System.Windows.Forms.ComboBox cmbBxBand3;
        private System.Windows.Forms.ComboBox cmBxSD;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox grpBxOptions;
        private System.Windows.Forms.ComboBox cmBxSize;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.CheckBox ckOrder;
        private System.Windows.Forms.CheckBox ckStrech;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TextBox txtBxStatus;
        private System.Windows.Forms.PictureBox pctBoxImg;
        private System.Windows.Forms.Button btPreview;
        private System.Windows.Forms.TreeView treeViewDescript;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ProgressBar progressBar1;

        #endregion

    }
}

