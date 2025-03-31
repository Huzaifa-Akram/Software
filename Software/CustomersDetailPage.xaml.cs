using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Software.Data;

namespace Software
{
    /// <summary>
    /// Interaction logic for CustomersDetailPage.xaml
    /// </summary>
    public partial class CustomersDetailPage : Page
    {
        private DatabaseHelper? _databaseHelper;
        private ObservableCollection<Customer>? _customers;
        private ICollectionView? _customersView;

        public CustomersDetailPage()
        {
            InitializeComponent();
            _databaseHelper = new DatabaseHelper();
            _customers = new ObservableCollection<Customer>();
            LoadCustomersAndSuppliers();
        }

        private void LoadCustomersAndSuppliers()
        {
            _customers?.Clear();
            ResultCountTextBlock.Text = $"Showing {_customersView?.Cast<object>().Count() ?? 0} results";

            if (_databaseHelper != null)
            {
                using (var connection = _databaseHelper.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT Id, Name, ContactNumber, Address, Type, CompanyName FROM Customers";
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _customers?.Add(new Customer
                                {
                                    Id = reader.GetInt32(0),
                                    Name = reader.GetString(1),
                                    ContactNumber = reader.GetString(2),
                                    Address = reader.GetString(3),
                                    Type = reader.GetString(4),
                                    CompanyName = reader.IsDBNull(5) ? null : reader.GetString(5)
                                });
                            }
                        }
                    }
                }
            }

            CustomerDataGrid.ItemsSource = _customers;
            _customersView = CollectionViewSource.GetDefaultView(_customers);

            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int customerCount = 0;
            int supplierCount = 0;

            if (_customersView != null)
            {
                foreach (Customer customer in _customersView)
                {
                    if (customer.Type == "Customer")
                    {
                        customerCount++;
                    }
                    else if (customer.Type == "Supplier")
                    {
                        supplierCount++;
                    }
                }
            }

            CustomerCountTextBlock.Text = $"Total Customers: {customerCount}";
            SupplierCountTextBlock.Text = $"Total Suppliers: {supplierCount}";
            ResultCountTextBlock.Text = $"Showing {_customersView?.Cast<object>().Count()} results";
        }

        private void AddNewCustomer_Click(object sender, RoutedEventArgs e)
        {
            var newCustomerPopup = new NewCustomerPopup();
            if (newCustomerPopup.ShowDialog() == true)
            {
                LoadCustomersAndSuppliers();
            }
        }

        private void DeleteCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (CustomerDataGrid.SelectedItem is Customer selectedCustomer)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to delete {selectedCustomer.Name}?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (_databaseHelper != null)
                        {
                            using (var connection = _databaseHelper.GetConnection())
                            {
                                connection.Open();
                                string query = "DELETE FROM Customers WHERE Id = @Id";
                                using (var command = new SQLiteCommand(query, connection))
                                {
                                    command.Parameters.AddWithValue("@Id", selectedCustomer.Id);
                                    int rowsAffected = command.ExecuteNonQuery();

                                    if (rowsAffected > 0)
                                    {
                                        LoadCustomersAndSuppliers();
                                        MessageBox.Show("Customer deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Failed to delete customer.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting customer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a customer to delete.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_customersView != null)
            {
                string? searchText = SearchTextBox?.Text.ToLower();

                if (string.IsNullOrEmpty(searchText))
                {
                    _customersView.Filter = null;
                }
                else
                {
                    _customersView.Filter = o =>
                    {
                        var customer = o as Customer;
                        return customer != null &&
                               (customer.Name.ToLower().Contains(searchText) ||
                                customer.ContactNumber.ToLower().Contains(searchText) ||
                                (customer.Address?.ToLower().Contains(searchText) ?? false) ||
                                customer.Type.ToLower().Contains(searchText) ||
                                (customer.CompanyName?.ToLower().Contains(searchText) ?? false));
                    };
                }

                UpdateSummary();
            }
        }
    }

    
}
