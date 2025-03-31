using System;
// ...existing code...
using System.Net.Http;
// ...existing code...
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;
using Software.Data;

namespace Software
{
    public partial class NewCustomerPopup : Window
    {
        private DatabaseHelper _databaseHelper;

        public NewCustomerPopup()
        {
            InitializeComponent();
            _databaseHelper = new DatabaseHelper();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text;
            string contactNumber = ContactNumberTextBox.Text;
            string address = AddressTextBox.Text;
            string? type = (TypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string companyName = CompanyNameTextBox.Text;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(contactNumber) || string.IsNullOrEmpty(type))
            {
                MessageBox.Show("Name, Contact Number, and Type are required fields.");
                return;
            }

            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                string query = "INSERT INTO Customers (Name, ContactNumber, Address, Type, CompanyName) VALUES (@Name, @ContactNumber, @Address, @Type, @CompanyName)";
                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Name", name);
                    command.Parameters.AddWithValue("@ContactNumber", contactNumber);
                    command.Parameters.AddWithValue("@Address", address);
                    command.Parameters.AddWithValue("@Type", type);
                    command.Parameters.AddWithValue("@CompanyName", string.IsNullOrEmpty(companyName) ? (object)DBNull.Value : companyName);
                    command.ExecuteNonQuery();
                }
            }

            DialogResult = true;
            Close();
        }
    }
}
