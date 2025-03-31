using System;
// ...existing code...
using System.Net.Http;
// ...existing code...
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Data.SQLite;
using Software.Data;

namespace Software
{
    /// <summary>
    /// Interaction logic for StockDetailPage.xaml
    /// </summary>
    public partial class StockDetailPage : Page
    {
        private DatabaseHelper _databaseHelper;
        private ObservableCollection<Item> _items;
        private ObservableCollection<ItemBatch> _batches;

        public StockDetailPage()
        {
            InitializeComponent();
            _databaseHelper = new DatabaseHelper();
            _items = new ObservableCollection<Item>();
            _batches = new ObservableCollection<ItemBatch>();

            LoadItems();
        }

        // Also update the LoadItems method to initialize the item count
        private void LoadItems()
        {
            _items.Clear();

            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();

                string query = "SELECT * FROM Items";

                using (var command = new SQLiteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new Item
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Name = reader["Name"]?.ToString() ?? string.Empty,
                                CompanyName = reader["CompanyName"]?.ToString() ?? string.Empty,
                                LatestPurchaseRate = Convert.ToDecimal(reader["LatestPurchaseRate"]),
                                TotalQuantity = Convert.ToInt32(reader["TotalQuantity"])
                            };

                            _items.Add(item);
                        }
                    }
                }
            }

            ItemsDataGrid.ItemsSource = _items;

            // Update the item count indicator
            ItemCountTextBlock.Text = $"{_items.Count} item{(_items.Count != 1 ? "s" : "")}";
        }

        private void LoadBatches(int itemId)
        {
            _batches.Clear();

            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();

                string query = "SELECT * FROM ItemBatches WHERE ItemId = @ItemId";

                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ItemId", itemId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var batch = new ItemBatch
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                ItemId = Convert.ToInt32(reader["ItemId"]),
                                PurchaseRate = Convert.ToDecimal(reader["PurchaseRate"]),
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                ExpiryDate = reader["ExpiryDate"]?.ToString() ?? string.Empty,
                                PurchaseDate = reader["PurchaseDate"]?.ToString() ?? string.Empty
                            };

                            _batches.Add(batch);
                        }
                    }
                }
            }

            BatchesDataGrid.ItemsSource = _batches;
        }

        private void ItemsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ItemsDataGrid.SelectedItem is Item selectedItem)
            {
                LoadBatches(selectedItem.Id);
            }
        }

        private void DeleteBatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (BatchesDataGrid.SelectedItem is ItemBatch selectedBatch)
            {
                MessageBoxResult result = MessageBox.Show("Are you sure you want to delete this batch?", "Delete Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    using (var connection = _databaseHelper.GetConnection())
                    {
                        connection.Open();

                        string query = "DELETE FROM ItemBatches WHERE Id = @Id";

                        using (var command = new SQLiteCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@Id", selectedBatch.Id);
                            command.ExecuteNonQuery();
                        }
                    }

                    _batches.Remove(selectedBatch);
                }
            }
        }

        // Update the existing SearchTextBox_TextChanged method to update UI elements
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();
            var filteredItems = new ObservableCollection<Item>();

            foreach (var item in _items)
            {
                if (item.Name.ToLower().Contains(searchText) ||
                    (item.CompanyName != null && item.CompanyName.ToLower().Contains(searchText)))
                {
                    filteredItems.Add(item);
                }
            }

            ItemsDataGrid.ItemsSource = filteredItems;

            // Update item count
            ItemCountTextBlock.Text = $"{filteredItems.Count} item{(filteredItems.Count != 1 ? "s" : "")}";

            // Show/hide clear button based on search text
            ClearSearchButton.Visibility = string.IsNullOrWhiteSpace(searchText) ?
                Visibility.Collapsed : Visibility.Visible;
        }

        // Only adding the new method for the clear search button

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear the search box
            SearchTextBox.Clear();

            // This will trigger the TextChanged event and reset the items
        }

        // New method for closing a batch
        private void CloseBatchButton_Click(object sender, RoutedEventArgs e)
        {
            BatchesDataGrid.ItemsSource = null;
        }

        public class Item
        {
            public int Id { get; set; }
            public required string Name { get; set; }
            public string? CompanyName { get; set; }
            public decimal LatestPurchaseRate { get; set; }
            public int TotalQuantity { get; set; }
        }

        public class ItemBatch
        {
            public int Id { get; set; }
            public int ItemId { get; set; }
            public decimal PurchaseRate { get; set; }
            public int Quantity { get; set; }
            public string? ExpiryDate { get; set; }
            public string? PurchaseDate { get; set; }
            public string? Status { get; set; }
        }
    }
}
