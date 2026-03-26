namespace SQLTestDataScriptGenerator
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer? components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            serverLabel = new Label();
            serverTextBox = new TextBox();
            connectButton = new Button();
            databaseSearchLabel = new Label();
            databaseSearchTextBox = new TextBox();
            label2 = new Label();
            databasesListBox = new ListBox();
            selectAllTablesButton = new Button();
            clearTablesButton = new Button();
            tablesCheckedListBox = new CheckedListBox();
            rowSelectionLabel = new Label();
            rowsComboBox = new ComboBox();
            exportButton = new Button();
            exportCsvButton = new Button();
            progressBar = new ProgressBar();
            statusLabel = new Label();
            selectedDatabaseLabel = new Label();
            schemaFilterLabel = new Label();
            schemaFilterComboBox = new ComboBox();
            manageRelationshipsButton = new Button();
            fkGraphButton = new Button();
            autoMapButton = new Button();
            refreshSchemaButton = new Button();
            SuspendLayout();
            // serverLabel
            serverLabel.Location = new Point(12,15);
            serverLabel.AutoSize = true;
            serverLabel.Text = "Server URL:";
            // serverTextBox
            serverTextBox.Location = new Point(95,12);
            serverTextBox.Size = new Size(398,23);
            // connectButton
            connectButton.Location = new Point(499,12);
            connectButton.Size = new Size(150,23);
            connectButton.Text = "Connect Server";
            connectButton.TabIndex =2;
            connectButton.Click += connectButton_Click;
            // databaseSearchLabel
            databaseSearchLabel.Location = new Point(12,47);
            databaseSearchLabel.AutoSize = true;
            databaseSearchLabel.Text = "Search by Database Name:";
            // databaseSearchTextBox
            databaseSearchTextBox.Location = new Point(175,44);
            databaseSearchTextBox.Size = new Size(318,23);
            databaseSearchTextBox.TabIndex =4;
            databaseSearchTextBox.TextChanged += databaseSearchTextBox_TextChanged;
            // label2
            label2.Location = new Point(12,76);
            label2.AutoSize = true;
            label2.Text = "Databases (0):";
            // databasesListBox
            databasesListBox.Location = new Point(12,94);
            databasesListBox.Size = new Size(712,110);
            databasesListBox.TabIndex =6;
            databasesListBox.IntegralHeight = false;
            databasesListBox.SelectedIndexChanged += databasesListBox_SelectedIndexChanged;
            // selectedDatabaseLabel
            selectedDatabaseLabel.Location = new Point(12,209);
            selectedDatabaseLabel.Size = new Size(712,18);
            selectedDatabaseLabel.Text = "Selected Database: (none)";
            selectedDatabaseLabel.BorderStyle = BorderStyle.FixedSingle;
            // autoMapButton
            autoMapButton.Location = new Point(12,229);
            autoMapButton.Size = new Size(160,25);
            autoMapButton.Text = "Suggested ForeignKeys";
            autoMapButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            autoMapButton.Click += autoMapButton_Click;
            // manageRelationshipsButton
            manageRelationshipsButton.Location = new Point(autoMapButton.Right +10,229);
            manageRelationshipsButton.Size = new Size(160,25);
            manageRelationshipsButton.Text = "Add ForeignKeys";
            // fkGraphButton
            fkGraphButton.Location = new Point(352,229);
            fkGraphButton.Size = new Size(110,25);
            fkGraphButton.Text = "FK Graph";
            // schemaFilterLabel
            schemaFilterLabel.Location = new Point(472,234);
            schemaFilterLabel.AutoSize = true;
            schemaFilterLabel.Text = "Schema:";
            // schemaFilterComboBox
            schemaFilterComboBox.Location = new Point(530,231);
            schemaFilterComboBox.Size = new Size(120,23); // reduced width for ~12 chars
            schemaFilterComboBox.TabIndex =11;
            schemaFilterComboBox.Items.AddRange(new object[] {"(All)"});
            schemaFilterComboBox.SelectedIndex = 0;
            schemaFilterComboBox.SelectedIndexChanged += schemaFilterComboBox_SelectedIndexChanged;
            // refreshSchemaButton
            refreshSchemaButton.Location = new Point(434,229); // provisional, LayoutTopControls will reposition
            refreshSchemaButton.Size = new Size(90,25);
            refreshSchemaButton.Text = "Refresh";
            refreshSchemaButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            refreshSchemaButton.Click += refreshSchemaButton_Click;
            // selectAllTablesButton
            selectAllTablesButton.Location = new Point(530,232);
            selectAllTablesButton.Size = new Size(90,25);
            selectAllTablesButton.TabIndex =12;
            selectAllTablesButton.Text = "Select All";
            selectAllTablesButton.Click += (s,e)=> { if (_currentDatabase == null) { MessageBox.Show("Select a database first."); return; } for(int i=0;i<tablesCheckedListBox.Items.Count;i++) tablesCheckedListBox.SetItemChecked(i,true); };
            // clearTablesButton
            clearTablesButton.Location = new Point(626,232);
            clearTablesButton.Size = new Size(98,25);
            clearTablesButton.TabIndex =13;
            clearTablesButton.Text = "Clear";
            clearTablesButton.Click += (s,e)=> { if (_currentDatabase == null) { MessageBox.Show("Select a database first."); return; } for(int i=0;i<tablesCheckedListBox.Items.Count;i++) tablesCheckedListBox.SetItemChecked(i,false); };
            // tablesCheckedListBox
            tablesCheckedListBox.Location = new Point(12,263);
            tablesCheckedListBox.Size = new Size(712,123);
            tablesCheckedListBox.TabIndex =14;
            tablesCheckedListBox.CheckOnClick = true;
            tablesCheckedListBox.HorizontalScrollbar = true;
            tablesCheckedListBox.IntegralHeight = false;
            // rowSelectionLabel
            rowSelectionLabel.Location = new Point(305,395); // moved right before rowsComboBox
            rowSelectionLabel.AutoSize = true;
            rowSelectionLabel.Text = "Rows Mode:";
            // rowsComboBox
            rowsComboBox.Location = new Point(390,391); // placed between label and export buttons
            rowsComboBox.Size = new Size(121,23);
            rowsComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            rowsComboBox.Items.Clear();
            rowsComboBox.Items.AddRange(new object[] {"All Data","First10","First100","First1000","Last10","Last100","Last1000"});
            if (rowsComboBox.Items.Count >0) rowsComboBox.SelectedIndex =0;
            // exportCsvButton
            exportCsvButton.Location = new Point(520,391);
            exportCsvButton.Size = new Size(100,23);
            exportCsvButton.Text = "Export CSV";
            exportCsvButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            exportCsvButton.Click += exportCsvButton_Click;
            // exportButton
            exportButton.Location = new Point(626,391);
            exportButton.Size = new Size(98,23);
            exportButton.Text = "Export SQL";
            exportButton.Click += exportButton_Click;
            // progressBar
            progressBar.Location = new Point(12,420);
            progressBar.Size = new Size(712,15);
            progressBar.Step =1;
            progressBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            progressBar.Visible = true;
            // statusLabel
            statusLabel.Location = new Point(12,440);
            statusLabel.Size = new Size(712,18);
            statusLabel.Text = "Status";
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            // Ensure they are added (if not already)
            if (!Controls.Contains(progressBar)) Controls.Add(progressBar);
            if (!Controls.Contains(statusLabel)) Controls.Add(statusLabel);
            // Adjust anchoring for responsive layout
            serverLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            serverTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            connectButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            databaseSearchLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            databaseSearchTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            databasesListBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            selectedDatabaseLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            manageRelationshipsButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            fkGraphButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            schemaFilterLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            schemaFilterComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            refreshSchemaButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            selectAllTablesButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            clearTablesButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            tablesCheckedListBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            rowSelectionLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            rowsComboBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            exportButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            exportCsvButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            progressBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            // Make form sizable
            FormBorderStyle = FormBorderStyle.Sizable;
            AutoScaleMode = AutoScaleMode.Font;
            // Optional: set an initial ClientSize if not already set
            if (ClientSize.Width <760 || ClientSize.Height <500)
            ClientSize = new Size(760,500);
            // Form
            Controls.Add(autoMapButton);
            Controls.Add(manageRelationshipsButton);
            Controls.Add(exportButton);
            Controls.Add(exportCsvButton);
            Controls.Add(rowsComboBox);
            Controls.Add(rowSelectionLabel);
            Controls.Add(tablesCheckedListBox);
            Controls.Add(clearTablesButton);
            Controls.Add(selectAllTablesButton);
            Controls.Add(refreshSchemaButton);
            Controls.Add(selectedDatabaseLabel);
            Controls.Add(databasesListBox);
            Controls.Add(label2);
            Controls.Add(databaseSearchTextBox);
            Controls.Add(databaseSearchLabel);
            Controls.Add(connectButton);
            Controls.Add(serverTextBox);
            Controls.Add(serverLabel);
            Controls.Add(schemaFilterLabel);
            Controls.Add(schemaFilterComboBox);
            Controls.Add(fkGraphButton);
            // Enhance form appearance
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MinimumSize = new Size(760, 500);
            Name = "MainForm";
            Text = "SQL Test Data Script Generator";
            ResumeLayout(false);
            PerformLayout();
        }
        #endregion

        private Label serverLabel;
        private TextBox serverTextBox;
        private Button connectButton;
        private Label databaseSearchLabel;
        private TextBox databaseSearchTextBox;
        private Label label2;
        private ListBox databasesListBox;
        private Button selectAllTablesButton;
        private Button clearTablesButton;
        private CheckedListBox tablesCheckedListBox;
        private Label rowSelectionLabel;
        private ComboBox rowsComboBox;
        private Button exportButton;
        private Button exportCsvButton;
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label selectedDatabaseLabel;
        private Label schemaFilterLabel;
        private ComboBox schemaFilterComboBox;
        private Button manageRelationshipsButton;
        private Button fkGraphButton;
        private Button autoMapButton;
        private Button refreshSchemaButton;
    }
}
