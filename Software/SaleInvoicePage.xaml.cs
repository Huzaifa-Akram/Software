using System;
// ...existing code...
using System.Net.Http;
// ...existing code...
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Software.Data;

namespace Software
{
    public partial class SaleInvoicePage : Page
    {
        private DatabaseHelper? _databaseHelper;
        private List<InvoiceItem>? _invoiceItems;
        private List<Item>? _allItems;
        // Track reserved quantities for each item
        private Dictionary<int, int>? _reservedQuantities;
        private List<BatchDeduction>? _batchDeductions = new List<BatchDeduction>();
        private ToastNotification? _toastNotification;



        public SaleInvoicePage()
        {
            InitializeComponent();
            _databaseHelper = new DatabaseHelper();
            _invoiceItems = new List<InvoiceItem>();
            _allItems = new List<Item>();
            _reservedQuantities = new Dictionary<int, int>();
            _batchDeductions = new List<BatchDeduction>();
            _toastNotification = new ToastNotification(Application.Current.MainWindow);

            LoadInvoiceNumber();
            LoadItems();
            LoadCustomers();
        }
        private class BatchItem
        {
            public int Id { get; set; }
            public int ItemId { get; set; }
            public decimal PurchaseRate { get; set; }
            public int Quantity { get; set; }
            public DateTime? ExpiryDate { get; set; }
            public DateTime PurchaseDate { get; set; }
        }
        // A class to track batch deductions for sales
        private class BatchDeduction
        {
            public int BatchId { get; set; }
            public int ItemId { get; set; }
            public int Quantity { get; set; }
            public decimal PurchaseRate { get; set; }
        }

        private void LoadInvoiceNumber()
        {
            if (_databaseHelper == null)
            {
                throw new InvalidOperationException("DatabaseHelper is not initialized.");
            }

            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                string invoiceType = "S"; // Assuming this is for SaleInvoicePage, use "P" for PurchaseInvoicePage
                string query = $@"
                SELECT IFNULL(MAX(CAST(SUBSTR(InvoiceNumber, 2) AS INTEGER)), 0) + 1 
                FROM Invoices 
                WHERE InvoiceType = '{invoiceType}'";
                using (var command = new SQLiteCommand(query, connection))
                {
                    var result = command.ExecuteScalar();
                    InvoiceNumberTextBox.Text = $"Invoice no: {invoiceType}{result}";
                }
            }
        }

        private InvoiceItem? _editingItem;

        private void InvoiceItemsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (InvoiceItemsListView.SelectedItem is InvoiceItem selectedItem)
            {
                _editingItem = selectedItem;
                SelectedItemNameTextBlock.Text = selectedItem.ItemName;
                SelectedItemNameTextBlock.Tag = selectedItem.ItemId; // Store the item ID
                ItemRateTextBox.Text = selectedItem.SaleRate.ToString("F2");
                ItemQuantityTextBox.Text = selectedItem.Quantity.ToString();
                ItemDiscountTextBox.Text = selectedItem.DiscountPercentage.ToString("F2");

                // Display available quantity, accounting for this item's quantity being edited
                var selectedItemId = selectedItem.ItemId;
                var item = _allItems?.FirstOrDefault(i => i.Id == selectedItemId);
                if (item != null)
                {
                    int reservedQty = GetReservedQuantityExcept(selectedItemId, selectedItem);
                    QuantityAvailable.Text = $"Available Quantity: {item.AvailableQuantity - reservedQty}";
                }

                CalculateTotal();
                ItemDetailsPopup.IsOpen = true;
            }
        }

        // Get reserved quantity for an item, excluding a specific invoice item if provided
        private int GetReservedQuantityExcept(int itemId, InvoiceItem? excludeItem = null)
        {
            int reserved = 0;
            if (_invoiceItems != null)
            {
                foreach (var item in _invoiceItems)
                {
                    if (item.ItemId == itemId && item != excludeItem)
                    {
                        reserved += item.Quantity;
                    }
                }
            }
            return reserved;
        }

        // Get total available quantity for an item (database quantity minus reserved)
        private int GetAvailableQuantity(int itemId, InvoiceItem? excludeItem = null)
        {
            if (_databaseHelper == null)
            {
                throw new InvalidOperationException("DatabaseHelper is not initialized.");
            }

            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();

                // Get total quantity from batches
                string query = "SELECT SUM(Quantity) FROM ItemBatches WHERE ItemId = @ItemId AND Quantity > 0";
                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@ItemId", itemId);
                    var result = command.ExecuteScalar();
                    int dbQuantity = result != DBNull.Value ? Convert.ToInt32(result) : 0;

                    // Subtract reserved quantities (items in the current invoice)
                    int reservedQty = GetReservedQuantityExcept(itemId, excludeItem);
                    return dbQuantity - reservedQty;
                }
            }
        }

        private void AddToInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_invoiceItems == null)
            {
                throw new InvalidOperationException("Invoice items list is not initialized.");
            }

            if (decimal.TryParse(ItemRateTextBox.Text, out decimal rate) &&
                int.TryParse(ItemQuantityTextBox.Text, out int quantity) &&
                decimal.TryParse(ItemDiscountTextBox.Text, out decimal discount) &&
                decimal.TryParse(ItemTotalTextBox.Text, out decimal total))
            {
                // Get the selected item ID from the tag
                int selectedItemId = (int)SelectedItemNameTextBlock.Tag;

                // Check if the quantity is available, accounting for quantities already in the invoice
                int availableQuantity = GetAvailableQuantity(selectedItemId, _editingItem);
                if (quantity > availableQuantity)
                {
                    ErrorLabel.Content = $"Quantity not available. Available Quantity: {availableQuantity}";
                    ErrorLabel.Visibility = Visibility.Visible;
                    return;
                }

                var invoiceItem = new InvoiceItem
                {
                    ItemId = selectedItemId,
                    ItemName = SelectedItemNameTextBlock.Text,
                    SaleRate = rate,
                    Quantity = quantity,
                    DiscountPercentage = discount,
                    Total = total
                };

                // If we're editing an existing item, remove it first
                if (_editingItem != null)
                {
                    _invoiceItems.Remove(_editingItem);
                    InvoiceItemsListView.Items.Remove(_editingItem);
                    _editingItem = null;
                }

                _invoiceItems.Add(invoiceItem);
                InvoiceItemsListView.Items.Add(invoiceItem);
                UpdateTotals();
                ItemDetailsPopup.IsOpen = false;
                ErrorLabel.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadItems()
        {
            if (_databaseHelper == null)
            {
                throw new InvalidOperationException("DatabaseHelper is not initialized.");
            }

            if (_allItems == null)
            {
                throw new InvalidOperationException("AllItems list is not initialized.");
            }

            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                string query = "SELECT Id, Name, LatestPurchaseRate, LatestRetailPrice, TotalQuantity FROM Items";
                using (var command = new SQLiteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _allItems.Add(new Item
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                PurchaseRate = reader.GetDecimal(2),
                                RetailPrice = reader.GetDecimal(3),
                                TotalQuantity = reader.GetInt32(4),
                                AvailableQuantity = reader.GetInt32(4),
                            });
                        }
                    }
                }
            }
        }

        private void LoadCustomers()
        {
            if (_databaseHelper == null)
            {
                throw new InvalidOperationException("DatabaseHelper is not initialized.");
            }

            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                string query = "SELECT Id, Name FROM Customers where Type = 'Customer'";
                using (var command = new SQLiteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        var customers = new List<Customer>();
                        while (reader.Read())
                        {
                            customers.Add(new Customer
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                ContactNumber = string.Empty,
                                Type = "Customer"
                            });
                        }
                        CustomerComboBox.ItemsSource = customers;
                    }
                }
            }
        }

        private void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            ItemSelectionPopup.IsOpen = true;
            ItemsListView.ItemsSource = _allItems;
        }

        private void ItemSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allItems == null) return;

            string searchText = ItemSearchBox.Text.ToLower();
            var filteredItems = _allItems.Where(item => item.Name.ToLower().Contains(searchText)).ToList();
            ItemsListView.ItemsSource = filteredItems;
        }

        private decimal GetLastSalePriceForCustomer(int customerId, int itemId)
        {
            if (_databaseHelper == null)
            {
                throw new InvalidOperationException("DatabaseHelper is not initialized.");
            }

            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                string query = @"
                    SELECT ii.Rate
                    FROM InvoiceItems ii
                    JOIN Invoices i ON ii.InvoiceNumber = i.InvoiceNumber
                    WHERE i.CustomerId = @CustomerId AND ii.ItemId = @ItemId
                    ORDER BY i.InvoiceDate DESC
                    LIMIT 1";

                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CustomerId", customerId);
                    command.Parameters.AddWithValue("@ItemId", itemId);
                    var result = command.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToDecimal(result);
                    }
                    return -1;
                }
            }
        }

        private void ItemsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ItemsListView.SelectedItem is Item selectedItem)
            {
                SelectedItemNameTextBlock.Text = selectedItem.Name;
                SelectedItemNameTextBlock.Tag = selectedItem.Id; // Store the item ID
                PurchaseRate.Text = "Purchase Rate: " + selectedItem.PurchaseRate.ToString("F2");
                RetailPrice.Text = "Retail Price: " + selectedItem.RetailPrice.ToString("F2");

                // Calculate available quantity (database quantity minus already reserved)
                int availableQuantity = GetAvailableQuantity(selectedItem.Id);
                QuantityAvailable.Text = $"Available Quantity: {availableQuantity}";

                if (CustomerComboBox.SelectedItem is Customer selectedCustomer)
                {
                    // Get last sale price for this customer and item using item ID
                    decimal lastSalePrice = GetLastSalePriceForCustomer(selectedCustomer.Id, selectedItem.Id);

                    // Display last sale price if available
                    if (lastSalePrice >= 0)
                    {
                        // If you've already added a TextBlock for this in your XAML:
                        if (FindName("LastSalePriceTextBlock") is TextBlock lastSalePriceTextBlock)
                        {
                            lastSalePriceTextBlock.Text = "Last Time Sale Price: " + lastSalePrice.ToString("F2");
                            lastSalePriceTextBlock.Visibility = Visibility.Visible;
                        }

                        // Pre-fill the rate with the last sale price
                        ItemRateTextBox.Text = lastSalePrice.ToString("F2");
                    }
                    else
                    {
                        // If you've already added a TextBlock for this in your XAML:
                        if (FindName("LastSalePriceTextBlock") is TextBlock lastSalePriceTextBlock)
                        {
                            lastSalePriceTextBlock.Visibility = Visibility.Collapsed;
                        }

                        ItemRateTextBox.Text = "";
                    }
                }
                else
                {
                    ItemRateTextBox.Text = "";
                }

                ItemQuantityTextBox.Text = "1"; // Default to 1
                ItemDiscountTextBox.Text = "0";
                CalculateTotal();
                ItemSelectionPopup.IsOpen = false;
                ItemDetailsPopup.IsOpen = true;
            }
        }

        private void ItemDetails_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateTotal();

            // Update quantity validation in real-time
            if (int.TryParse(ItemQuantityTextBox.Text, out int quantity) &&
                SelectedItemNameTextBlock.Tag != null)
            {
                int itemId = (int)SelectedItemNameTextBlock.Tag;
                int availableQuantity = GetAvailableQuantity(itemId, _editingItem);

                if (quantity > availableQuantity)
                {
                    ErrorLabel.Content = $"Quantity exceeds available amount ({availableQuantity})";
                    ErrorLabel.Visibility = Visibility.Visible;
                }
                else
                {
                    ErrorLabel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void CalculateTotal()
        {
            if (decimal.TryParse(ItemRateTextBox.Text, out decimal rate) &&
                int.TryParse(ItemQuantityTextBox.Text, out int quantity) &&
                decimal.TryParse(ItemDiscountTextBox.Text, out decimal discount))
            {
                decimal total = rate * quantity * (1 - discount / 100);
                ItemTotalTextBox.Text = total.ToString("F2");
            }
        }

        private void CancelItemButton_Click(object sender, RoutedEventArgs e)
        {
            ItemDetailsPopup.IsOpen = false;
            ErrorLabel.Visibility = Visibility.Collapsed;
            _editingItem = null;
        }

        private void UpdateTotals()
        {
            decimal totalDiscount = 0;
            decimal totalAmount = 0;

            if (_invoiceItems != null)
            {
                foreach (var item in _invoiceItems)
                {
                    totalDiscount += item.SaleRate * item.Quantity * (item.DiscountPercentage / 100);
                    totalAmount += item.Total;
                }
            }

            TotalDiscountTextBlock.Text = totalDiscount.ToString("F2");
            TotalAmountTextBlock.Text = totalAmount.ToString("F2");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveInvoice();
            MessageBox.Show("Invoice saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            ResetForm();
        }

        private void SaveAndPrintButton_Click(object sender, RoutedEventArgs e)
        {
            SaveInvoice();
            string invoiceNumberText = InvoiceNumberTextBox.Text.Replace("Invoice no: ", "");
            long invoiceNumber = long.Parse(invoiceNumberText.Substring(1));
            PrintInvoice(invoiceNumber);
            ResetForm();
        }



        private void PrintInvoice(long invoiceNumber)
        {
            // Create a PrintDialog
            PrintDialog printDialog = new PrintDialog();

            // Check if the user has selected a printer
            if (printDialog.ShowDialog() == true)
            {
                // Create a FlowDocument to format the invoice for printing
                FlowDocument doc = new FlowDocument();
                doc.PagePadding = new Thickness(50);
                doc.ColumnWidth = printDialog.PrintableAreaWidth;

                // Add a logo at the top center
                try
                {
                    // Create a BitmapImage for the logo
                    BitmapImage logo = new BitmapImage(new Uri("pack://application:,,,/Software;component/Resources/logo.png"));
                    Image logoImage = new Image
                    {
                        Source = logo,
                        Width = 150,
                        Height = 80,
                        Stretch = Stretch.Uniform
                    };

                    // Create a BlockUIContainer to hold the image
                    BlockUIContainer logoContainer = new BlockUIContainer(logoImage);

                    // Add the logo to the document
                    Section logoSection = new Section();
                    logoSection.TextAlignment = TextAlignment.Center;
                    logoSection.Blocks.Add(logoContainer);
                    doc.Blocks.Add(logoSection);
                }
                catch (Exception)
                {
                    // If logo fails to load, show company name with larger font instead
                    Paragraph companyNameAlt = new Paragraph(new Run("YourCompany"));
                    companyNameAlt.FontSize = 28;
                    companyNameAlt.FontWeight = FontWeights.Bold;
                    companyNameAlt.TextAlignment = TextAlignment.Center;
                    doc.Blocks.Add(companyNameAlt);
                }

                // Add company name
                Paragraph companyName = new Paragraph(new Run("Your Company Name"));
                companyName.FontSize = 22;
                companyName.FontWeight = FontWeights.Bold;
                companyName.TextAlignment = TextAlignment.Center;
                doc.Blocks.Add(companyName);

                // Add company address
                Paragraph companyAddress = new Paragraph(new Run("123 Business Street, City, Country"));
                companyAddress.FontSize = 14;
                companyAddress.TextAlignment = TextAlignment.Center;
                doc.Blocks.Add(companyAddress);

                // Add company contact
                Paragraph companyContact = new Paragraph(new Run("Tel: +1-234-567-8900 | Email: sales@yourcompany.com"));
                companyContact.FontSize = 12;
                companyContact.TextAlignment = TextAlignment.Center;
                doc.Blocks.Add(companyContact);

                // Add horizontal line separator
                Border border = new Border();
                border.BorderBrush = Brushes.Gray;
                border.BorderThickness = new Thickness(0, 1, 0, 0);
                border.Padding = new Thickness(0, 5, 0, 5);
                BlockUIContainer borderContainer = new BlockUIContainer(border);
                doc.Blocks.Add(borderContainer);

                // Add the invoice title and number
                Paragraph header = new Paragraph(new Run($"INVOICE"));
                header.FontSize = 20;
                header.FontWeight = FontWeights.Bold;
                header.TextAlignment = TextAlignment.Center;
                doc.Blocks.Add(header);

                // Create a table for invoice header info
                Table infoTable = new Table();
                infoTable.CellSpacing = 0;

                // Define columns
                infoTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                infoTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

                // Add row group
                TableRowGroup infoGroup = new TableRowGroup();

                // Create invoice info row
                TableRow infoRow = new TableRow();

                // Left side - Customer info
                TableCell customerCell = new TableCell();
                Paragraph customerTitle = new Paragraph(new Bold(new Run("BILL TO:")));
                customerTitle.FontSize = 12;
                customerTitle.Margin = new Thickness(0, 0, 0, 5);

                Paragraph customerDetails = new Paragraph();
                customerDetails.Inlines.Add(new Run($"{((Customer)CustomerComboBox.SelectedItem)?.Name}"));
                customerDetails.Margin = new Thickness(0);
                customerDetails.FontSize = 12;

                customerCell.Blocks.Add(customerTitle);
                customerCell.Blocks.Add(customerDetails);

                // Right side - Invoice details
                TableCell invoiceDetailsCell = new TableCell();
                Paragraph invoiceDetailsTitle = new Paragraph();
                invoiceDetailsTitle.Inlines.Add(new Bold(new Run("DETAILS:")));
                invoiceDetailsTitle.Margin = new Thickness(0, 0, 0, 5);
                invoiceDetailsTitle.FontSize = 12;

                Paragraph invoiceNumberPara = new Paragraph();
                invoiceNumberPara.Inlines.Add(new Bold(new Run("Invoice #: ")));
                invoiceNumberPara.Inlines.Add(new Run($"{invoiceNumber}"));
                invoiceNumberPara.Margin = new Thickness(0);
                invoiceNumberPara.FontSize = 12;

                Paragraph invoiceDatePara = new Paragraph();
                invoiceDatePara.Inlines.Add(new Bold(new Run("Date: ")));
                invoiceDatePara.Inlines.Add(new Run($"{DateTime.Now.ToString("MMMM dd, yyyy")}"));
                invoiceDatePara.Margin = new Thickness(0);
                invoiceDatePara.FontSize = 12;

                invoiceDetailsCell.Blocks.Add(invoiceDetailsTitle);
                invoiceDetailsCell.Blocks.Add(invoiceNumberPara);
                invoiceDetailsCell.Blocks.Add(invoiceDatePara);

                infoRow.Cells.Add(customerCell);
                infoRow.Cells.Add(invoiceDetailsCell);
                infoGroup.Rows.Add(infoRow);
                infoTable.RowGroups.Add(infoGroup);

                doc.Blocks.Add(infoTable);

                // Add some space
                doc.Blocks.Add(new Paragraph(new Run(" ")) { Margin = new Thickness(0, 10, 0, 10) });

                // Add the invoice items table with improved styling
                Table table = new Table();
                table.CellSpacing = 0;
                table.BorderBrush = Brushes.Black;
                table.BorderThickness = new Thickness(1);

                // Define the columns with better proportions
                table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) }); // Item name
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Rate
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Quantity
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Discount
                table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) }); // Total

                // Add the table header with background color
                TableRowGroup headerGroup = new TableRowGroup();
                TableRow headerRow = new TableRow();
                headerRow.Background = Brushes.LightGray;

                // Create header cells with bold text
                TableCell itemNameHeader = new TableCell(new Paragraph(new Bold(new Run("Item Description"))));
                TableCell rateHeader = new TableCell(new Paragraph(new Bold(new Run("Unit Price"))));
                TableCell qtyHeader = new TableCell(new Paragraph(new Bold(new Run("Qty"))));
                TableCell discountHeader = new TableCell(new Paragraph(new Bold(new Run("Discount"))));
                TableCell totalHeader = new TableCell(new Paragraph(new Bold(new Run("Amount"))));

                // Center align header text
                itemNameHeader.TextAlignment = TextAlignment.Left;
                rateHeader.TextAlignment = TextAlignment.Left;
                qtyHeader.TextAlignment = TextAlignment.Left;
                discountHeader.TextAlignment = TextAlignment.Left;
                totalHeader.TextAlignment = TextAlignment.Right;

                // Add padding to cells
                itemNameHeader.Padding = new Thickness(5);
                rateHeader.Padding = new Thickness(5);
                qtyHeader.Padding = new Thickness(5);
                discountHeader.Padding = new Thickness(5);
                totalHeader.Padding = new Thickness(5);

                // Add cells to header row
                headerRow.Cells.Add(itemNameHeader);
                headerRow.Cells.Add(rateHeader);
                headerRow.Cells.Add(qtyHeader);
                headerRow.Cells.Add(discountHeader);
                headerRow.Cells.Add(totalHeader);

                headerGroup.Rows.Add(headerRow);
                table.RowGroups.Add(headerGroup);

                // Add the invoice items to the table with alternating row colors
                TableRowGroup bodyGroup = new TableRowGroup();
                bool isAlternate = false;

                if (_invoiceItems != null && bodyGroup != null) // Ensure collections are not null
                {
                    foreach (var item in _invoiceItems)
                    {
                        if (item == null) continue; // Skip null items to prevent crashes

                        TableRow row = new TableRow();
                        if (isAlternate)
                            row.Background = Brushes.WhiteSmoke;

                        // Create cells with formatted text
                        TableCell nameCell = new TableCell(new Paragraph(new Run(item.ItemName ?? "N/A"))); // Handle null strings
                        TableCell rateCell = new TableCell(new Paragraph(new Run(item.SaleRate.ToString("F2"))));
                        TableCell quantityCell = new TableCell(new Paragraph(new Run(item.Quantity.ToString())));
                        TableCell discountCell = new TableCell(new Paragraph(new Run(item.DiscountPercentage.ToString("F2") + "%")));
                        TableCell totalCell = new TableCell(new Paragraph(new Run(item.Total.ToString("F2"))));

                        // Align text
                        nameCell.TextAlignment = TextAlignment.Left;
                        rateCell.TextAlignment = TextAlignment.Left;
                        quantityCell.TextAlignment = TextAlignment.Left;
                        discountCell.TextAlignment = TextAlignment.Left;
                        totalCell.TextAlignment = TextAlignment.Right;

                        // Add padding to cells
                        foreach (var cell in new[] { nameCell, rateCell, quantityCell, discountCell, totalCell })
                        {
                            cell.Padding = new Thickness(5);
                        }

                        row.Cells.Add(nameCell);
                        row.Cells.Add(rateCell);
                        row.Cells.Add(quantityCell);
                        row.Cells.Add(discountCell);
                        row.Cells.Add(totalCell);

                        bodyGroup.Rows.Add(row);
                        isAlternate = !isAlternate;
                    }
                }


                table.RowGroups.Add(bodyGroup);

                // Add the table to the document
                doc.Blocks.Add(table);

                // Add summary section
                // Calculate subtotal and total discount
                decimal subtotal = 0;
                decimal totalDiscount = 0;
                foreach (var item in _invoiceItems ?? Enumerable.Empty<InvoiceItem>()) 
                {
                    if (item != null) // Check if item is null
                    {
                        subtotal += item.SaleRate * item.Quantity;
                        totalDiscount += item.SaleRate * item.Quantity * (item.DiscountPercentage / 100);
                    }
                }

                // Create a table for totals
                Table totalsTable = new Table();
                totalsTable.CellSpacing = 0;

                // Define columns - empty space on left, totals on right
                totalsTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                totalsTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

                // Add row group
                TableRowGroup totalsGroup = new TableRowGroup();

                // Subtotal Row
                TableRow subtotalRow = new TableRow();
                TableCell subtotalLabelCell = new TableCell(new Paragraph(new Run("Subtotal:")));
                subtotalLabelCell.TextAlignment = TextAlignment.Right;
                subtotalLabelCell.Padding = new Thickness(5);

                TableCell subtotalValueCell = new TableCell(new Paragraph(new Run(subtotal.ToString("F2"))));
                subtotalValueCell.TextAlignment = TextAlignment.Right;
                subtotalValueCell.Padding = new Thickness(5);

                subtotalRow.Cells.Add(subtotalLabelCell);
                subtotalRow.Cells.Add(subtotalValueCell);
                totalsGroup.Rows.Add(subtotalRow);

                // Discount Row
                TableRow discountRow = new TableRow();
                TableCell discountLabelCell = new TableCell(new Paragraph(new Run("Total Discount:")));
                discountLabelCell.TextAlignment = TextAlignment.Right;
                discountLabelCell.Padding = new Thickness(5);

                TableCell discountValueCell = new TableCell(new Paragraph(new Run(totalDiscount.ToString("F2"))));
                discountValueCell.TextAlignment = TextAlignment.Right;
                discountValueCell.Padding = new Thickness(5);

                discountRow.Cells.Add(discountLabelCell);
                discountRow.Cells.Add(discountValueCell);
                totalsGroup.Rows.Add(discountRow);

                // Total Row with emphasis
                TableRow totalRow = new TableRow();
                totalRow.Background = Brushes.LightGray;

                TableCell totalLabelCell = new TableCell();
                Paragraph totalLabelPara = new Paragraph(new Bold(new Run("TOTAL:")));
                totalLabelPara.FontSize = 14;
                totalLabelCell.Blocks.Add(totalLabelPara);
                totalLabelCell.TextAlignment = TextAlignment.Right;
                totalLabelCell.Padding = new Thickness(5);

                TableCell totalValueCell = new TableCell();
                Paragraph totalValuePara = new Paragraph(new Bold(new Run(TotalAmountTextBlock.Text)));
                totalValuePara.FontSize = 14;
                totalValueCell.Blocks.Add(totalValuePara);
                totalValueCell.TextAlignment = TextAlignment.Right;
                totalValueCell.Padding = new Thickness(5);

                totalRow.Cells.Add(totalLabelCell);
                totalRow.Cells.Add(totalValueCell);
                totalsGroup.Rows.Add(totalRow);

                totalsTable.RowGroups.Add(totalsGroup);
                doc.Blocks.Add(totalsTable);

                // Add footer with thank you message and terms
                doc.Blocks.Add(new Paragraph(new Run(" ")) { Margin = new Thickness(0, 20, 0, 0) });

                Paragraph thankYou = new Paragraph(new Bold(new Run("Thank You For Your Business!")));
                thankYou.TextAlignment = TextAlignment.Center;
                thankYou.FontSize = 14;
                doc.Blocks.Add(thankYou);

                Paragraph terms = new Paragraph(new Run("Terms & Conditions: Payment is due within 30 days. Late payments subject to a 2% monthly fee."));
                terms.TextAlignment = TextAlignment.Center;
                terms.FontSize = 10;
                terms.Foreground = Brushes.Gray;
                doc.Blocks.Add(terms);

                // Print the document
                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Invoice");
            }
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all fields and reset the form
            _invoiceItems?.Clear();
            InvoiceItemsListView.Items.Clear();
            CustomerComboBox.SelectedIndex = -1;
            LoadInvoiceNumber();
            TotalDiscountTextBlock.Text = "0.00";
            TotalAmountTextBlock.Text = "0.00";

            // Clear reserved quantities
            _reservedQuantities?.Clear();
        }

        private void DeleteInvoiceItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is InvoiceItem selectedItem)
            {
                _invoiceItems?.Remove(selectedItem);
                InvoiceItemsListView.Items.Remove(selectedItem);
                UpdateTotals();
            }
        }

        private void SaveInvoice()
        {
            if (_invoiceItems == null || !_invoiceItems.Any())
            {
                return;
            }
            using (var connection = _databaseHelper?.GetConnection())
            {
                connection?.Open();
                using (var transaction = connection?.BeginTransaction())
                {
                    try
                    {
                        // 1. Verify all quantities are still available and calculate FIFO deductions
                        Dictionary<int, int> totalQuantities = new Dictionary<int, int>();
                        if (_invoiceItems != null && totalQuantities != null) // Ensure collections are initialized
                        {
                            foreach (var item in _invoiceItems.Where(i => i != null)) // Skip null items
                            {
                                if (!totalQuantities.ContainsKey(item.ItemId))
                                {
                                    totalQuantities[item.ItemId] = 0;
                                }
                                totalQuantities[item.ItemId] += item.Quantity;
                            }
                        }
                        else
                        {
                            MessageBox.Show("Error: Data is missing!", "Null Reference Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }


                        _batchDeductions?.Clear(); // Reset batch deductions

                        if (totalQuantities != null && _allItems != null) // Ensure collections are initialized
                        {
                            foreach (var kvp in totalQuantities)
                            {
                                int itemId = kvp.Key;
                                int requestedQuantity = kvp.Value;

                                // Get available batches in FIFO order
                                List<BatchItem> batches = connection != null ? GetBatchesForItem(connection, itemId) ?? new List<BatchItem>() : new List<BatchItem>();
                                int availableQuantity = batches.Any() ? batches.Sum(b => b.Quantity) : 0;

                                var item = _allItems.FirstOrDefault(i => i.Id == itemId);
                                if (item == null || requestedQuantity > availableQuantity)
                                {
                                    _toastNotification?.Show($"Cannot save invoice. Item \"{item?.Name ?? "Unknown"}\" has insufficient quantity.",
                                        ToastNotification.NotificationType.Error);

                                    transaction?.Rollback(); // Rollback the transaction safely if it's not null
                                    return; // Return early if there's an error
                                }

                                // Calculate FIFO batch deductions
                                int remainingToDeduct = requestedQuantity;
                                foreach (var batch in batches)
                                {
                                    if (remainingToDeduct <= 0) break;

                                    int deductFromBatch = Math.Min(remainingToDeduct, batch.Quantity);
                                    _batchDeductions?.Add(new BatchDeduction // Ensure _batchDeductions is initialized
                                    {
                                        BatchId = batch.Id,
                                        ItemId = itemId,
                                        Quantity = deductFromBatch,
                                        PurchaseRate = batch.PurchaseRate
                                    });

                                    remainingToDeduct -= deductFromBatch;
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("Error: Data is missing!", "Null Reference Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }


                        // 2. Insert the invoice header (Invoices) - Use the InvoiceNumber from the Textbox
                        string insertInvoiceQuery = @"
                INSERT INTO Invoices (InvoiceNumber, CustomerId, InvoiceDate, InvoiceType)
                VALUES (@InvoiceNumber, @CustomerId, datetime('now'), 'S');"; // 'S' for Sales

                        // Remove the "Invoice no: " prefix before saving
                        string invoiceNumberText = InvoiceNumberTextBox.Text.Replace("Invoice no: ", "");

                        using (var command = new SQLiteCommand(insertInvoiceQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumberText);
                            command.Parameters.AddWithValue("@CustomerId", ((Customer)CustomerComboBox.SelectedItem)?.Id);

                            command.ExecuteNonQuery(); // No need to get last_insert_rowid() since you're providing the InvoiceNumber
                        }

                        // 3. Insert invoice items (InvoiceItems)
                        string insertInvoiceItemQuery = @"
                INSERT INTO InvoiceItems (InvoiceNumber, ItemId, Rate, Quantity, DiscountPercentage, Total)
                VALUES (@InvoiceNumber, @ItemId, @Rate, @Quantity, @DiscountPercentage, @Total);";

                        if (_invoiceItems != null && connection != null && transaction != null) // Ensure objects are not null
                        {
                            foreach (var item in _invoiceItems.Where(i => i != null)) // Skip null items
                            {
                                using (var command = new SQLiteCommand(insertInvoiceItemQuery, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumberText ?? string.Empty);
                                    command.Parameters.AddWithValue("@ItemId", item.ItemId);
                                    command.Parameters.AddWithValue("@Rate", item.SaleRate);
                                    command.Parameters.AddWithValue("@Quantity", item.Quantity);
                                    command.Parameters.AddWithValue("@DiscountPercentage", item.DiscountPercentage);
                                    command.Parameters.AddWithValue("@Total", item.Total);

                                    try
                                    {
                                        command.ExecuteNonQuery();
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show($"Database Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                        transaction.Rollback(); // Rollback on error
                                        return;
                                    }
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("Error: Data is missing!", "Null Reference Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }


                        // 4. Update batch quantities using FIFO
                        string updateBatchQuery = @"
                UPDATE ItemBatches
                SET Quantity = Quantity - @Quantity
                WHERE Id = @BatchId;";

                        string updateItemTotalQuery = @"
                UPDATE Items
                SET TotalQuantity = TotalQuantity - @Quantity
                WHERE Id = @ItemId;";

                        // Apply the FIFO batch deductions
                        if (_batchDeductions != null && connection != null && transaction != null && _allItems != null) // Ensure collections are initialized
                        {
                            foreach (var deduction in _batchDeductions.Where(d => d != null)) // Skip null deductions
                            {
                                try
                                {
                                    // Update batch quantity
                                    using (var command = new SQLiteCommand(updateBatchQuery, connection, transaction))
                                    {
                                        command.Parameters.AddWithValue("@BatchId", deduction.BatchId);
                                        command.Parameters.AddWithValue("@Quantity", deduction.Quantity);
                                        command.ExecuteNonQuery();
                                    }

                                    // Update item total quantity
                                    using (var command = new SQLiteCommand(updateItemTotalQuery, connection, transaction))
                                    {
                                        command.Parameters.AddWithValue("@ItemId", deduction.ItemId);
                                        command.Parameters.AddWithValue("@Quantity", deduction.Quantity);
                                        command.ExecuteNonQuery();
                                    }

                                    // Update our local copy of the items data
                                    var dbItem = _allItems.FirstOrDefault(i => i.Id == deduction.ItemId);
                                    if (dbItem != null)
                                    {
                                        dbItem.AvailableQuantity = Math.Max(0, dbItem.AvailableQuantity - deduction.Quantity); // Prevent negative values
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Database Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback(); // Rollback on error
                                    return;
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("Error: Data is missing!", "Null Reference Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }


                        // 5. Track batch deductions (SalesInvoiceBatchDeductions)
                        string insertBatchDeductionQuery = @"
                INSERT INTO SalesInvoiceBatchDeductions (InvoiceNumber, BatchId, ItemId, Quantity, PurchaseRate)
                VALUES (@InvoiceNumber, @BatchId, @ItemId, @Quantity, @PurchaseRate);";

                        if (_batchDeductions != null && connection != null && transaction != null) // Ensure objects are not null
                        {
                            foreach (var deduction in _batchDeductions.Where(d => d != null)) // Skip null deductions
                            {
                                try
                                {
                                    using (var command = new SQLiteCommand(insertBatchDeductionQuery, connection, transaction))
                                    {
                                        command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumberText ?? string.Empty); // Prevent null invoice number
                                        command.Parameters.AddWithValue("@BatchId", deduction.BatchId);
                                        command.Parameters.AddWithValue("@ItemId", deduction.ItemId);
                                        command.Parameters.AddWithValue("@Quantity", deduction.Quantity);
                                        command.Parameters.AddWithValue("@PurchaseRate", deduction.PurchaseRate);
                                        command.ExecuteNonQuery();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Database Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                    transaction.Rollback(); // Rollback on error
                                    return;
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("Error: Data is missing!", "Null Reference Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }


                        transaction?.Commit();
                        _toastNotification?.Show("Sale Invoice saved successfully!", ToastNotification.NotificationType.Success);
                    }
                    catch (Exception ex)
                    {
                        transaction?.Rollback();
                        _toastNotification?.Show($"Error Saving Invoice {ex.Message}", ToastNotification.NotificationType.Error);
                    }
                }
            }
        }



        private void ResetForm()
        {
            _invoiceItems?.Clear();
            _batchDeductions?.Clear();
            InvoiceItemsListView.Items.Clear();
            CustomerComboBox.SelectedIndex = -1;
            LoadInvoiceNumber();
            TotalDiscountTextBlock.Text = "0.00";
            TotalAmountTextBlock.Text = "0.00";
        }
        private List<BatchItem> GetBatchesForItem(SQLiteConnection connection, int itemId)
        {
            var batches = new List<BatchItem>();

            string query = @"
        SELECT Id, ItemId, PurchaseRate, Quantity, ExpiryDate, PurchaseDate
        FROM ItemBatches 
        WHERE ItemId = @ItemId AND Quantity > 0
        ORDER BY PurchaseDate ASC"; // FIFO: oldest first

            using (var command = new SQLiteCommand(query, connection))
            {
                command.Parameters.AddWithValue("@ItemId", itemId);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        batches.Add(new BatchItem
                        {
                            Id = reader.GetInt32(0),
                            ItemId = reader.GetInt32(1),
                            PurchaseRate = reader.GetDecimal(2),
                            Quantity = reader.GetInt32(3),
                            ExpiryDate = reader.IsDBNull(4) ? (DateTime?)null : DateTime.Parse(reader.GetString(4)),
                            PurchaseDate = DateTime.Parse(reader.GetString(5))
                        });
                    }
                }
            }

            return batches;
        }

        private void ItemDetailsPopup_Opened(object sender, EventArgs e)
        {
            ItemRateTextBox.Focus();
        }

        private void ItemDetailsPopup_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddToInvoiceButton_Click(AddToInvoiceButton, new RoutedEventArgs());
            }
            if (e.Key == Key.Escape)
            {
                CancelItemButton_Click(CancelItemButton, new RoutedEventArgs());
            }
        }

        private void ItemSelectionPopup_Opened(object sender, EventArgs e)
        {
            ItemSearchBox.Focus();
        }

        private void ItemSelectionPopup_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ItemSelectionPopup.IsOpen = false;
            }
        }
    }

    public class InvoiceItem
    {
        public int ItemId { get; set; } // Added ItemId property
        public required string ItemName { get; set; }
        public decimal SaleRate { get; set; }
        public int Quantity { get; set; }
        public decimal DiscountPercentage { get; set; }
        public decimal Total { get; set; }
    }

    public class Item
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public decimal PurchaseRate { get; set; }
        public int TotalQuantity { get; set; }
        public decimal RetailPrice { get; set; }
        public int AvailableQuantity { get; set; }
    }

    public class Customer
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string ContactNumber { get; set; }
        public string? Address { get; set; }
        public required string Type { get; set; }
        public string? CompanyName { get; set; }
    }


}