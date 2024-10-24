using Cellahattin.Configuration;
using Cellahattin.Encryption;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Cellahattin
{
    public partial class MainWindow : Window
    {
        private readonly string ENCRYPTION_KEY = SecurityConfig.Instance.EncryptionKey;
        private readonly string SYMMETRIC_KEY = SecurityConfig.Instance.SymmetricKey;

        private string originalEncryptedValue;
        private SqlConnection connection;
        private DataTable selectedTable;
        private string selectedColumn;
        private DataRowView selectedRow;
        private string selectedValue;
        private string clickedColumnName;

        private string previousValue;
        private string currentTableName;
        private string currentColumnName;
        private string currentPrimaryKeyValue;
        private string currentPrimaryKeyColumn;

        public MainWindow()
        {
            InitializeComponent();
            SetControlState(false);
        }

        private void SetControlState(bool enabled)
        {
            txtNewDatabaseName.IsEnabled = enabled;
            btnRenameDatabase.IsEnabled = enabled;
            cmbTableSelect.IsEnabled = enabled;
            txtNewTableName.IsEnabled = enabled;
            btnRenameTable.IsEnabled = enabled;
            cmbColumnSelect.IsEnabled = enabled;
            txtNewColumnName.IsEnabled = enabled;
            btnRenameColumn.IsEnabled = enabled;

            cmbTables.IsEnabled = enabled;
            cmbColumns.IsEnabled = enabled;
            cmbValues.IsEnabled = enabled;
            btnUpdate.IsEnabled = enabled;
            btnUndoUpdate.IsEnabled = enabled;
            txtEditValue.IsEnabled = enabled;
            chkIsEncrypted.IsEnabled = enabled;
            btnDisconnect.IsEnabled = enabled;
        }
        private void ResetAllData()
        {
            txtCurrentDatabase.Clear();
            txtNewDatabaseName.Clear();
            cmbTableSelect.Items.Clear();
            txtNewTableName.Clear();
            cmbColumnSelect.Items.Clear();
            txtNewColumnName.Clear();
            txtManagementStatus.Text = string.Empty;

            cmbTables.Items.Clear();
            cmbColumns.Items.Clear();
            cmbValues.Items.Clear();
            dgResults.ItemsSource = null;
            txtEditValue.Clear();
            txtSelectedCell.Text = "Selected Cell Value --> ";
            txtSelectedValue.Text = "Selected Value --> ";
            txtSelectedColumn.Text = "Selected Column --> ";
            txtStatus.Text = "Disconnected";
            txtStatus.Foreground = Brushes.Gray;
            chkIsEncrypted.IsChecked = false;
            SetControlState(false);
        }
        private async Task LoadTablesWithSchema()
        {
            try
            {
                string query = @"
                    SELECT 
                        SCHEMA_NAME(t.schema_id) as SchemaName,
                        t.name as TableName
                    FROM 
                        sys.tables t
                    ORDER BY 
                        SchemaName, TableName";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    cmbTables.Items.Clear();
                    cmbTableSelect.Items.Clear();

                    while (await reader.ReadAsync())
                    {
                        string schemaName = reader["SchemaName"].ToString();
                        string tableName = reader["TableName"].ToString();
                        string fullTableName = schemaName == "dbo" ? tableName : $"{schemaName}.{tableName}";

                        cmbTables.Items.Add(fullTableName);
                        cmbTableSelect.Items.Add(fullTableName);
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("Error loading tables", ex);
            }
        }
        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (connection != null && connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }

                connection = new SqlConnection(txtConnectionString.Text);
                await connection.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("SELECT DB_NAME()", connection))
                {
                    txtCurrentDatabase.Text = (await cmd.ExecuteScalarAsync())?.ToString();
                }

                await LoadTablesWithSchema();

                SetControlState(true);
                txtStatus.Text = "Connected successfully!";
                txtStatus.Foreground = Brushes.Green;
                txtManagementStatus.Text = "Connected successfully!";
                txtManagementStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                HandleError("Connection error", ex);
            }
        }
        private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (connection != null && connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                    connection = null;
                }
                ResetAllData();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Disconnect error: {ex.Message}";
                txtStatus.Foreground = Brushes.Red;
            }
        }
        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (selectedRow == null || string.IsNullOrEmpty(clickedColumnName))
            {
                txtStatus.Text = "Please select a cell to edit.";
                txtStatus.Foreground = Brushes.Red;
                return;
            }

            try
            {
                SaveCurrentStateForUndo();

                string tableName = cmbTables.SelectedItem.ToString();
                string primaryKeyColumn = selectedTable.PrimaryKey.Length > 0
                    ? selectedTable.PrimaryKey[0].ColumnName
                    : selectedTable.Columns[0].ColumnName;

                string primaryKeyValue = selectedRow[primaryKeyColumn].ToString();
                string valueToSave = txtEditValue.Text;

                if (chkIsEncrypted.IsChecked == true)
                {
                    valueToSave = EncryptionHelper.EncryptString(txtEditValue.Text, SYMMETRIC_KEY);
                }

                string updateQuery = $"UPDATE {tableName} SET {clickedColumnName} = @newValue WHERE {primaryKeyColumn} = @primaryKey";

                using (SqlCommand cmd = new SqlCommand(updateQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@newValue", valueToSave);
                    cmd.Parameters.AddWithValue("@primaryKey", primaryKeyValue);
                    await cmd.ExecuteNonQueryAsync();
                }

                selectedValue = valueToSave;
                await RefreshCurrentView();

                txtStatus.Text = "Update successful!";
                txtStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Update error: {ex.Message}";
                txtStatus.Foreground = Brushes.Red;
                MessageBox.Show($"Update error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void BtnUndoUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(previousValue))
            {
                txtStatus.Text = "No previous update to undo.";
                txtStatus.Foreground = Brushes.Red;
                return;
            }

            try
            {
                string updateQuery = $"UPDATE {currentTableName} SET {currentColumnName} = @previousValue " +
                                   $"WHERE {currentPrimaryKeyColumn} = @primaryKey";

                using (SqlCommand cmd = new SqlCommand(updateQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@previousValue", previousValue);
                    cmd.Parameters.AddWithValue("@primaryKey", currentPrimaryKeyValue);
                    await cmd.ExecuteNonQueryAsync();
                }

                selectedValue = previousValue;
                await RefreshCurrentView();

                txtStatus.Text = "Undo successful!";
                txtStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Undo error: {ex.Message}";
                txtStatus.Foreground = Brushes.Red;
                MessageBox.Show($"Undo error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void BtnRenameDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewDatabaseName.Text))
            {
                MessageBox.Show("Please enter a new database name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string currentDb = txtCurrentDatabase.Text;
                string newDb = txtNewDatabaseName.Text;

                // veritabanına başka kullanıcıların bağlanmasını engelliyorum
                string query1 = $"ALTER DATABASE [{currentDb}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                string query2 = $"ALTER DATABASE [{currentDb}] MODIFY NAME = [{newDb}]";
                // iş bitince tekrar multi user moduna alıyorum
                string query3 = $"ALTER DATABASE [{newDb}] SET MULTI_USER";

                using (SqlCommand cmd = new SqlCommand(query1, connection))
                    await cmd.ExecuteNonQueryAsync();

                using (SqlCommand cmd = new SqlCommand(query2, connection))
                    await cmd.ExecuteNonQueryAsync();

                using (SqlCommand cmd = new SqlCommand(query3, connection))
                    await cmd.ExecuteNonQueryAsync();

                txtCurrentDatabase.Text = newDb;
                txtManagementStatus.Text = "Database renamed successfully!";
                txtManagementStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                HandleError("Database rename error", ex);
            }
        }
        private async void BtnRenameTable_Click(object sender, RoutedEventArgs e)
        {
            if (cmbTableSelect.SelectedItem == null || string.IsNullOrWhiteSpace(txtNewTableName.Text))
            {
                MessageBox.Show("Please select a table and enter a new name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string[] oldTableParts = cmbTableSelect.SelectedItem.ToString().Split('.');
                string schema = oldTableParts.Length > 1 ? oldTableParts[0] : "dbo";
                string oldTableName = oldTableParts.Length > 1 ? oldTableParts[1] : oldTableParts[0];
                string newTableName = txtNewTableName.Text;

                string query = $"EXEC sp_rename '{schema}.{oldTableName}', '{newTableName}'";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                    await cmd.ExecuteNonQueryAsync();

                await LoadTablesWithSchema();
                txtManagementStatus.Text = "Table renamed successfully!";
                txtManagementStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                HandleError("Table rename error", ex);
            }
        }
        private async void BtnRenameColumn_Click(object sender, RoutedEventArgs e)
        {
            if (cmbTableSelect.SelectedItem == null || cmbColumnSelect.SelectedItem == null ||
                string.IsNullOrWhiteSpace(txtNewColumnName.Text))
            {
                MessageBox.Show("Please select a table, column and enter a new name.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string[] tableParts = cmbTableSelect.SelectedItem.ToString().Split('.');
                string schema = tableParts.Length > 1 ? tableParts[0] : "dbo";
                string tableName = tableParts.Length > 1 ? tableParts[1] : tableParts[0];
                string oldColumnName = cmbColumnSelect.SelectedItem.ToString();
                string newColumnName = txtNewColumnName.Text;

                string query = $"EXEC sp_rename '{schema}.{tableName}.{oldColumnName}', '{newColumnName}', 'COLUMN'";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                    await cmd.ExecuteNonQueryAsync();

                await LoadColumnsForTable(schema, tableName);
                txtManagementStatus.Text = "Column renamed successfully!";
                txtManagementStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                HandleError("Column rename error", ex);
            }
        }
        private async void CmbTableSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTableSelect.SelectedItem == null) return;

            try
            {
                string[] tableParts = cmbTableSelect.SelectedItem.ToString().Split('.');
                string schema = tableParts.Length > 1 ? tableParts[0] : "dbo";
                string tableName = tableParts.Length > 1 ? tableParts[1] : tableParts[0];

                string query = $@"
                    SELECT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_SCHEMA = '{schema}' 
                    AND TABLE_NAME = '{tableName}'
                    ORDER BY ORDINAL_POSITION";

                using (SqlCommand cmd = new SqlCommand(query, connection))
                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    cmbColumnSelect.Items.Clear();
                    while (await reader.ReadAsync())
                    {
                        cmbColumnSelect.Items.Add(reader["COLUMN_NAME"].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("Error loading columns", ex);
            }
        }
        private void CmbColumnSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbColumnSelect.SelectedItem == null) return;

            try
            {
                string selectedColumn = cmbColumnSelect.SelectedItem.ToString();
                txtNewColumnName.Text = selectedColumn;  // Seçilen kolonun adını text box'a yazsın

                txtManagementStatus.Text = $"Column '{selectedColumn}' selected.";
                txtManagementStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                HandleError("Column selection error", ex);
            }
        }
        private async void CmbTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTables.SelectedItem == null) return;

            try
            {
                string query = $"SELECT * FROM {cmbTables.SelectedItem}";
                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    selectedTable = new DataTable();
                    await Task.Run(() => adapter.Fill(selectedTable));
                }

                cmbColumns.Items.Clear();
                foreach (DataColumn column in selectedTable.Columns)
                {
                    cmbColumns.Items.Add(column.ColumnName);
                }

                txtStatus.Text = "Table loaded successfully!";
                txtStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Table loading error: {ex.Message}";
                txtStatus.Foreground = Brushes.Red;
            }
        }
        private void CmbColumns_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbColumns.SelectedItem == null) return;

            try
            {
                selectedColumn = cmbColumns.SelectedItem.ToString();
                cmbValues.Items.Clear();

                var distinctValues = selectedTable.AsEnumerable()
                    .Select(row => row[selectedColumn]?.ToString())
                    .Distinct()
                    .Where(x => !string.IsNullOrEmpty(x));

                foreach (var value in distinctValues)
                {
                    cmbValues.Items.Add(value);
                }

                txtStatus.Text = "Column values loaded successfully!";
                txtStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Column loading error: {ex.Message}";
                txtStatus.Foreground = Brushes.Red;
            }
        }
        private async void CmbValues_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbValues.SelectedItem == null && sender != null) return;

            try
            {
                string value;
                if (sender == null)
                {
                    value = cmbValues.SelectedItem?.ToString() ?? selectedValue ?? "";
                }
                else
                {
                    value = cmbValues.SelectedItem?.ToString() ?? "";
                    selectedValue = value;
                }

                if (string.IsNullOrEmpty(value)) return;

                string tableName = cmbTables.SelectedItem.ToString();
                string columnName = cmbColumns.SelectedItem.ToString();

                txtSelectedValue.Text = $"Selected Value --> {value}";

                string query = $"SELECT * FROM {tableName} WHERE {columnName} = @value";
                using (SqlCommand cmd = new SqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@value", value);
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataTable results = new DataTable();
                    await Task.Run(() => adapter.Fill(results));
                    dgResults.ItemsSource = results.DefaultView;
                }

                txtStatus.Text = "Results loaded successfully!";
                txtStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Results loading error: {ex.Message}";
                txtStatus.Foreground = Brushes.Red;
            }
        }
        private void DgResults_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            try
            {
                if (dgResults.SelectedCells.Count == 0) return;

                var cell = dgResults.SelectedCells[0];
                if (cell.Item is DataRowView row && cell.Column != null)
                {
                    selectedRow = row;
                    clickedColumnName = cell.Column.Header.ToString();

                    var cellValue = row[clickedColumnName]?.ToString() ?? "";
                    txtSelectedCell.Text = $"Selected Value: {cellValue}";
                    txtSelectedColumn.Text = $"Selected Column: {clickedColumnName}";

                    originalEncryptedValue = cellValue;
                    txtEditValue.Text = cellValue;

                    chkIsEncrypted.IsChecked = false;

                    txtStatus.Text = "Edit and click the Update Button.";
                    txtStatus.Foreground = Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Cell selection error: {ex.Message}";
                txtStatus.Foreground = Brushes.Red;
            }
        }
        private void DgResults_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }
        private void ChkIsEncrypted_Click(object sender, RoutedEventArgs e)
        {
            if (chkIsEncrypted.IsChecked == true)
            {
                try
                {
                    bool isEncrypted = EncryptionHelper.IsEncrypted(originalEncryptedValue);

                    if (isEncrypted)
                    {
                        var decryptedValue = EncryptionHelper.DecryptString(originalEncryptedValue, SYMMETRIC_KEY);
                        txtEditValue.Text = decryptedValue;
                    }
                    else
                    {
                        txtEditValue.Text = originalEncryptedValue;
                        MessageBox.Show("The selected value is not encrypted.",
                            "Not Encrypted", MessageBoxButton.OK, MessageBoxImage.Information);
                        chkIsEncrypted.IsChecked = false;
                    }
                }
                catch
                {
                    txtEditValue.Text = originalEncryptedValue;
                    chkIsEncrypted.IsChecked = false;
                    MessageBox.Show("The selected value uses a different encryption key.",
                        "Decryption Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                txtEditValue.Text = originalEncryptedValue;
            }
        }
        private async Task LoadColumnsForTable(string schema, string tableName)
        {
            string query = $@"
                SELECT COLUMN_NAME 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_SCHEMA = '{schema}' 
                AND TABLE_NAME = '{tableName}'
                ORDER BY ORDINAL_POSITION";

            using (SqlCommand cmd = new SqlCommand(query, connection))
            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                cmbColumnSelect.Items.Clear();
                while (await reader.ReadAsync())
                {
                    cmbColumnSelect.Items.Add(reader["COLUMN_NAME"].ToString());
                }
            }
        }
        private void HandleError(string context, Exception ex)
        {
            string errorMessage = $"{context}: {ex.Message}";
            txtStatus.Text = errorMessage;
            txtStatus.Foreground = Brushes.Red;
            txtManagementStatus.Text = errorMessage;
            txtManagementStatus.Foreground = Brushes.Red;
            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        private async Task RefreshCurrentView()
        {
            if (selectedValue != null)
            {
                cmbValues.SelectedItem = selectedValue;
                await Task.Delay(100);
                CmbValues_SelectionChanged(null, null);
            }
        }
        private void SaveCurrentStateForUndo()
        {
            currentTableName = cmbTables.SelectedItem.ToString();
            currentColumnName = clickedColumnName;
            currentPrimaryKeyColumn = selectedTable.PrimaryKey.Length > 0
                ? selectedTable.PrimaryKey[0].ColumnName
                : selectedTable.Columns[0].ColumnName;
            currentPrimaryKeyValue = selectedRow[currentPrimaryKeyColumn].ToString();
            previousValue = originalEncryptedValue;
        }
        
    }
}