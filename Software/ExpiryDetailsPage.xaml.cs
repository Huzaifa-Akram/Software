using System;
// ...existing code...
using System.Net.Http;
// ...existing code...
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Software.Data;

namespace Software
{
    public partial class ExpiryDetailsPage : Page
    {
        private DatabaseHelper? _databaseHelper;
        private List<ItemBatch>? _allBatches;

        public ExpiryDetailsPage()
        {
            InitializeComponent();
            _databaseHelper = new DatabaseHelper();
            LoadExpiryDetails();
        }

        private void LoadExpiryDetails()
        {
            try
            {
                _allBatches = _databaseHelper?.GetExpiryDetails() ?? new List<ItemBatch>();
                ExpiryDataGrid.ItemsSource = _allBatches;
                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading expiry details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSummary()
        {
            if (_allBatches == null)
            {
                TotalItemsTextBlock.Text = "0";
                TotalQuantityTextBlock.Text = "0";
                ResultCountTextBlock.Text = "Showing 0 results";
                return;
            }

            TotalItemsTextBlock.Text = _allBatches.Count.ToString();
            TotalQuantityTextBlock.Text = _allBatches.Sum(b => b.Quantity).ToString();
            ResultCountTextBlock.Text = $"Showing {_allBatches.Count} results";
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void DateRangeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateRangeSelector.SelectedIndex == 6) // Custom Range
            {
                if (CustomDatePanel != null)
                {
                    CustomDatePanel.Visibility = Visibility.Visible;
                }
            }
            else
            {
                if (CustomDatePanel != null)
                {
                    CustomDatePanel.Visibility = Visibility.Collapsed;
                }

                // Set date range based on selection
                DateTime now = DateTime.Now;
                DateTime startDate = now;
                DateTime endDate = now;

                switch (DateRangeSelector.SelectedIndex)
                {
                    case 0: // Show All
                        // Don't set any date range
                        if (FromDatePicker != null)
                        {
                            FromDatePicker.SelectedDate = null;
                        }
                        if (ToDatePicker != null)
                        {
                            ToDatePicker.SelectedDate = null;
                        }
                        return; // Exit the method early as we don't need to set dates
                    case 1: // Today
                        startDate = now.Date;
                        endDate = now.Date;
                        break;
                    case 2: // Next 7 Days
                        startDate = now.Date;
                        endDate = now.AddDays(7).Date;
                        break;
                    case 3: // Next 30 Days
                        startDate = now.Date;
                        endDate = now.AddDays(30).Date;
                        break;
                    case 4: // This Month
                        startDate = new DateTime(now.Year, now.Month, 1);
                        endDate = startDate.AddMonths(1).AddDays(-1);
                        break;
                    case 5: // Next Month
                        startDate = new DateTime(now.Year, now.Month, 1).AddMonths(1);
                        endDate = startDate.AddMonths(1).AddDays(-1);
                        break;
                    case 6: // Custom Range
                        startDate = FromDatePicker?.SelectedDate ?? DateTime.MinValue;
                        endDate = ToDatePicker?.SelectedDate ?? DateTime.MaxValue;
                        break;
                }

                if (FromDatePicker != null)
                {
                    FromDatePicker.SelectedDate = startDate;
                }
                if (ToDatePicker != null)
                {
                    ToDatePicker.SelectedDate = endDate;
                }

                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            if (_allBatches == null)
            {
                return;
            }

            var filteredBatches = _allBatches.AsEnumerable();

            if (DateRangeSelector.SelectedIndex != 0) // Not "Show All"
            {
                DateTime startDate = DateTime.MinValue;
                DateTime endDate = DateTime.MaxValue;

                switch (DateRangeSelector.SelectedIndex)
                {
                    case 1: // Today
                        startDate = DateTime.Today;
                        endDate = DateTime.Today;
                        break;
                    case 2: // Next 7 Days
                        startDate = DateTime.Today;
                        endDate = DateTime.Today.AddDays(7);
                        break;
                    case 3: // Next 30 Days
                        startDate = DateTime.Today;
                        endDate = DateTime.Today.AddDays(30);
                        break;
                    case 4: // This Month
                        startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                        endDate = startDate.AddMonths(1).AddDays(-1);
                        break;
                    case 5: // Next Month
                        startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
                        endDate = startDate.AddMonths(1).AddDays(-1);
                        break;
                    case 6: // Custom Range
                        startDate = FromDatePicker?.SelectedDate ?? DateTime.MinValue;
                        endDate = ToDatePicker?.SelectedDate ?? DateTime.MaxValue;
                        break;
                }

                filteredBatches = filteredBatches.Where(b => b.ExpiryDate >= startDate && b.ExpiryDate <= endDate);
            }

            if (!string.IsNullOrEmpty(SearchTextBox.Text))
            {
                string searchText = SearchTextBox.Text.ToLower();
                filteredBatches = filteredBatches.Where(b =>
                    (b.ItemName?.ToLower().Contains(searchText) ?? false) ||
                    (b.CompanyName?.ToLower().Contains(searchText) ?? false));
            }

            ExpiryDataGrid.ItemsSource = filteredBatches.ToList();
            UpdateSummary();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ExpiryDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Handle double-click event if needed
        }
    }

    public class ItemBatch
    {
        public required string ItemName { get; set; }
        public string? CompanyName { get; set; }
        public required decimal PurchaseRate { get; set; }
        public required int Quantity { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string? Alert { get; set; }
    }
}
