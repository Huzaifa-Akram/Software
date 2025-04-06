using Software.Data;
// ...existing code...
using System.Net.Http;
// ...existing code...
using System;
using System.Collections.Generic;
using System.Data.SQLite;
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
using System.Diagnostics;


namespace Software
{
    /// <summary>
    /// Interaction logic for PurchaseInvoicePage.xaml
    /// </summary>
    public partial class PurchaseInvoicePage : Page
    {
        private DatabaseHelper? _databaseHelper;
        private ObservableCollection<InvoiceItem>? _invoiceItems;
        private List<Item>? _allItems;
        private Dictionary<int, int>? _reservedQuantities;
        private InvoiceItem? _editingItem;
        private ToastNotification? _toastNotification;


        // ...existing code...
        public class InvoiceItem
        {
            public int ItemId { get; set; }
            public required string ItemName { get; set; }
            public decimal PurchaseRate { get; set; }
            public decimal RetailPrice { get; set; } // Add this property
            public int Quantity { get; set; }
            public int BonusQuantity { get; set; } // Add this property
            public decimal Total { get; set; }
            public decimal DiscountPercentage { get; set; } // Keep this for backward compatibility
            public DateTime? ExpiryDate { get; set; } // Add this property
        }

        public PurchaseInvoicePage()
        {
            InitializeComponent();
            _databaseHelper = new DatabaseHelper();
            _invoiceItems = new ObservableCollection<InvoiceItem>();
            _allItems = new List<Item>();
            _reservedQuantities = new Dictionary<int, int>(); // Add this line
            _toastNotification = new ToastNotification(Application.Current.MainWindow); // Add this line

            LoadInvoiceNumber();
            LoadItems();
            LoadSuppliers();

            InvoiceItemsListView.ItemsSource = _invoiceItems;

        }
        private void LoadInvoiceNumber()
        {
            if (_databaseHelper == null) throw new InvalidOperationException("DatabaseHelper is not initialized.");
            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                string invoiceType = "P"; // Assuming this is for SaleInvoicePage, use "P" for PurchaseInvoicePage
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
        private void LoadItems()
        {
            if (_databaseHelper == null) throw new InvalidOperationException("DatabaseHelper is not initialized.");
            if (_allItems == null) throw new InvalidOperationException("AllItems list is not initialized."); // Add this line
            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                string query = "SELECT Id, Name, LatestPurchaseRate, TotalQuantity FROM Items";
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
                                AvailableQuantity = reader.GetInt32(3)
                            });
                        }
                    }
                }
            }
        }
        private void LoadSuppliers()
        {
            if (_databaseHelper == null) throw new InvalidOperationException("DatabaseHelper is not initialized.");
            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                string query = "SELECT Id, Name, ContactNumber, Type FROM Customers where Type = 'Supplier'";
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
                                ContactNumber = reader.GetString(2),
                                Type = reader.GetString(3)
                            });
                        }
                        SupplierComboBox.ItemsSource = customers;
                    }
                }
            }
        }
        private void InvoiceItemsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (InvoiceItemsListView.SelectedItem is InvoiceItem selectedItem)
            {
                _editingItem = selectedItem;
                SelectedItemNameTextBlock.Text = selectedItem.ItemName;
                SelectedItemNameTextBlock.Tag = selectedItem.ItemId; // Store the item ID
                ItemRateTextBox.Text = selectedItem.PurchaseRate.ToString("F2"); // For purchase invoice, this is purchase rate
                RetailPriceTextBox.Text = selectedItem.RetailPrice.ToString("F2"); // Set retail price
                ItemQuantityTextBox.Text = selectedItem.Quantity.ToString();
                NewItemExpiryDatePicker.SelectedDate = selectedItem.ExpiryDate; // Set the date picker


                // For purchase invoices, we don't need to check available quantity
                // as we are adding inventory, not consuming it
                
                CalculateTotal();
                ItemDetailsPopup.IsOpen = true;
                
                // The dropdowns will be updated in the ItemDetailsPopup_Opened event
            }
        }

        // ...existing code...
        private void AddToInvoiceButton_Click(object sender, RoutedEventArgs e)
        {
            // Update date picker from dropdowns one more time before validation
            UpdateDatePickerFromDropdowns();

            if (NewItemExpiryDatePicker.SelectedDate == null)
            {
                ErrorLabel.Content = "Please select a valid expiry date.";
                ErrorLabel.Visibility = Visibility.Visible;
                return;
            }

            if (decimal.TryParse(ItemRateTextBox.Text, out decimal rate) &&
                decimal.TryParse(RetailPriceTextBox.Text, out decimal retailPrice) && // Parse retail price
                int.TryParse(ItemQuantityTextBox.Text, out int quantity) &&
                int.TryParse(ItemBonusQuantityTextBox.Text, out int bonusQuantity) && // Parse bonus quantity
                decimal.TryParse(ItemTotalTextBox.Text, out decimal total))
            {
                // Get the selected item ID from the tag
                int selectedItemId = (int)SelectedItemNameTextBlock.Tag;

                var invoiceItem = new InvoiceItem
                {
                    ItemId = selectedItemId,
                    ItemName = SelectedItemNameTextBlock.Text,
                    PurchaseRate = rate, // This represents purchase rate in this context
                    RetailPrice = retailPrice, // Set retail price
                    Quantity = quantity,
                    BonusQuantity = bonusQuantity, // Set bonus quantity
                    Total = total,
                    ExpiryDate = NewItemExpiryDatePicker.SelectedDate
                };

                // If we're editing an existing item, remove it first
                if (_editingItem != null)
                {
                    _invoiceItems?.Remove(_editingItem);
                    _editingItem = null;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _invoiceItems?.Add(invoiceItem);
                });

                UpdateTotals();
                ItemDetailsPopup.IsOpen = false;
                ErrorLabel.Visibility = Visibility.Collapsed;
            }
        }



        private void ItemsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ItemsListView.SelectedItem is Item selectedItem)
            {
                SelectedItemNameTextBlock.Text = selectedItem.Name;
                SelectedItemNameTextBlock.Tag = selectedItem.Id; // Store the item ID

                // For purchase invoices, show the last purchase price instead of sale price
                decimal lastPurchasePrice = GetLastPurchasePriceForSupplier(
                    ((Customer)SupplierComboBox.SelectedItem)?.Id ?? 0,
                    selectedItem.Id);

                if (lastPurchasePrice >= 0)
                {
                    // If you've already added a TextBlock for this in your XAML:
                    if (FindName("LastPurchasePriceTextBlock") is TextBlock lastPurchasePriceTextBlock)
                    {
                        lastPurchasePriceTextBlock.Text = "Last Purchase Price: " + lastPurchasePrice.ToString("F2");
                        lastPurchasePriceTextBlock.Visibility = Visibility.Visible;
                    }

                    // Pre-fill the rate with the last purchase price
                    ItemRateTextBox.Text = lastPurchasePrice.ToString("F2");
                    
                    // Set a default retail price with 20% markup
                    RetailPriceTextBox.Text = (lastPurchasePrice * 1.2m).ToString("F2");
                }
                else
                {
                    // If no previous purchase price found, use the current purchase rate from item
                    ItemRateTextBox.Text = selectedItem.PurchaseRate.ToString("F2");
                    
                    // Set a default retail price with 20% markup
                    RetailPriceTextBox.Text = (selectedItem.PurchaseRate * 1.2m).ToString("F2");

                    if (FindName("LastPurchasePriceTextBlock") is TextBlock lastPurchasePriceTextBlock)
                    {
                        lastPurchasePriceTextBlock.Visibility = Visibility.Collapsed;
                    }
                }

                ItemQuantityTextBox.Text = "1"; // Default to 1
                CalculateTotal();
                ItemSelectionPopup.IsOpen = false;
                ItemDetailsPopup.IsOpen = true;
            }
        }

        private decimal GetLastPurchasePriceForSupplier(int supplierId, int itemId)
        {
            if (_databaseHelper == null) throw new InvalidOperationException("DatabaseHelper is not initialized.");
            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                string query = @"
                SELECT ii.Rate
                FROM InvoiceItems ii
                JOIN Invoices i ON ii.InvoiceNumber = i.InvoiceNumber
                WHERE i.CustomerId = @SupplierId AND ii.ItemId = @ItemId AND i.InvoiceType = 'P'
                ORDER BY i.InvoiceDate DESC
                LIMIT 1";

                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SupplierId", supplierId);
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
        // ...existing code...
        private void SaveInvoice()
        {
            if (_invoiceItems == null || !_invoiceItems.Any())
            {
                Console.WriteLine("No invoice items to save.");
                return;
            }
            if (_databaseHelper == null) throw new InvalidOperationException("DatabaseHelper is not initialized.");
            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Insert the invoice header (Invoices)
                        string insertInvoiceQuery = @"
                        INSERT INTO Invoices (InvoiceNumber, CustomerId, SupplierName, InvoiceDate, InvoiceType)
                        VALUES (@InvoiceNumber, @SupplierId, @SupplierName, datetime('now'), 'P');";

                        string invoiceNumberText = InvoiceNumberTextBox.Text.Replace("Invoice no: ", "");

                        using (var command = new SQLiteCommand(insertInvoiceQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumberText);
                            var selectedSupplier = SupplierComboBox.SelectedItem as Customer;
                            if (selectedSupplier != null)
                            {
                                command.Parameters.AddWithValue("@SupplierId", selectedSupplier.Id);
                                command.Parameters.AddWithValue("@SupplierName", selectedSupplier.Name);
                            }
                            else
                            {
                                // Handle the case where no supplier is selected
                                command.Parameters.AddWithValue("@SupplierId", DBNull.Value);
                                command.Parameters.AddWithValue("@SupplierName", DBNull.Value);
                            }

                            command.ExecuteNonQuery();
                        }

                        // 2. Insert invoice items
                        string insertInvoiceItemQuery = @"
                        INSERT INTO InvoiceItems (InvoiceNumber, ItemId, Rate, Quantity, BonusQuantity, DiscountPercentage, RetailPrice, Total)
                        VALUES (@InvoiceNumber, @ItemId, @Rate, @Quantity, @BonusQuantity, @DiscountPercentage, @RetailPrice, @Total);";

                        // 3. Insert new batches for each item
                        string insertBatchQuery = @"
                        INSERT INTO ItemBatches (ItemId, PurchaseRate, RetailPrice, Quantity, ExpiryDate, PurchaseDate)
                        VALUES (@ItemId, @PurchaseRate, @RetailPrice, @Quantity, @ExpiryDate, datetime('now'))
                        RETURNING Id;";

                        if (_invoiceItems != null)
                        {
                            foreach (var item in _invoiceItems)
                            {
                                using (var command = new SQLiteCommand(insertInvoiceItemQuery, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumberText);
                                    command.Parameters.AddWithValue("@ItemId", item.ItemId);
                                    command.Parameters.AddWithValue("@Rate", item.PurchaseRate);
                                    command.Parameters.AddWithValue("@Quantity", item.Quantity);
                                    command.Parameters.AddWithValue("@BonusQuantity", item.BonusQuantity); // Add bonus quantity
                                    command.Parameters.AddWithValue("@DiscountPercentage", item.DiscountPercentage);
                                    command.Parameters.AddWithValue("@RetailPrice", item.RetailPrice); // Add retail price
                                    command.Parameters.AddWithValue("@Total", item.Total);
                                    command.ExecuteNonQuery();
                                }

                                int batchId;
                                using (var command = new SQLiteCommand(insertBatchQuery, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@ItemId", item.ItemId);
                                    command.Parameters.AddWithValue("@PurchaseRate", item.PurchaseRate);
                                    command.Parameters.AddWithValue("@RetailPrice", item.RetailPrice); // Add retail price
                                    command.Parameters.AddWithValue("@Quantity", item.Quantity + item.BonusQuantity); // Include bonus quantity
                                    command.Parameters.AddWithValue("@ExpiryDate", item.ExpiryDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value);

                                    batchId = Convert.ToInt32(command.ExecuteScalar());
                                }

                                string updateItemTotalQuery = @"
                                    UPDATE Items
                                    SET TotalQuantity = TotalQuantity + @Quantity,
                                        LatestPurchaseRate = @PurchaseRate,
                                        LatestRetailPrice = @RetailPrice
                                    WHERE Id = @ItemId;";

                                using (var command = new SQLiteCommand(updateItemTotalQuery, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@ItemId", item.ItemId);
                                    command.Parameters.AddWithValue("@Quantity", item.Quantity + item.BonusQuantity); // Include bonus quantity
                                    command.Parameters.AddWithValue("@PurchaseRate", item.PurchaseRate);
                                    command.Parameters.AddWithValue("@RetailPrice", item.RetailPrice); 
                                    command.ExecuteNonQuery();
                                }

                                var dbItem = _allItems?.FirstOrDefault(i => i.Id == item.ItemId);
                                if (dbItem != null)
                                {
                                    dbItem.AvailableQuantity += item.Quantity + item.BonusQuantity; // Include bonus quantity
                                    dbItem.PurchaseRate = item.PurchaseRate;
                                    dbItem.RetailPrice = item.RetailPrice; 
                                }
                            }
                        }

                        transaction.Commit();
                        Debug.WriteLine("Purchase Invoice saved successfully!");
                        _toastNotification?.Show("Purchase Invoice saved successfully!", ToastNotification.NotificationType.Success); // Add this line
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Debug.WriteLine($"Error Saving Invoice: {ex.Message}");
                        _toastNotification?.Show($"Error Saving Invoice {ex.Message}", ToastNotification.NotificationType.Error); // Add this line
                    }
                }
            }
        }


        private void CalculateTotal()
        {
            if (decimal.TryParse(ItemRateTextBox.Text, out decimal rate) &&
                int.TryParse(ItemQuantityTextBox.Text, out int quantity))
            {
                decimal total = rate * quantity;
                ItemTotalTextBox.Text = total.ToString("F2");
            }
        }
        private void UpdateTotals()
        {
            decimal totalDiscount = 0;
            decimal totalAmount = 0;
            if(_invoiceItems != null) { 
                foreach (var item in _invoiceItems)
                {
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
                Paragraph header = new Paragraph(new Run($"PURCHASE INVOICE"));
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
                customerDetails.Inlines.Add(new Run($"{((Customer)SupplierComboBox.SelectedItem)?.Name}"));
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
                invoiceNumberPara.Inlines.Add(new Run($"P{invoiceNumber}"));
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
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Bonus Quantity
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) }); // Discount
                table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) }); // Total

                // Add the table header with background color
                TableRowGroup headerGroup = new TableRowGroup();
                TableRow headerRow = new TableRow();
                headerRow.Background = Brushes.LightGray;

                // Create header cells with bold text
                TableCell itemNameHeader = new TableCell(new Paragraph(new Bold(new Run("Item Name"))));
                TableCell rateHeader = new TableCell(new Paragraph(new Bold(new Run("Unit Price"))));
                TableCell qtyHeader = new TableCell(new Paragraph(new Bold(new Run("Qty"))));
                TableCell discountHeader = new TableCell(new Paragraph(new Bold(new Run("Bonus-Qty"))));
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
                    foreach (var item in _invoiceItems.Where(i => i != null)) // Skip null items
                    {
                        TableRow row = new TableRow();
                        if (isAlternate)
                            row.Background = Brushes.WhiteSmoke;

                        // Create cells with formatted text (Handling possible null values)
                        TableCell nameCell = new TableCell(new Paragraph(new Run(item.ItemName ?? "N/A")));
                        TableCell rateCell = new TableCell(new Paragraph(new Run(item.PurchaseRate.ToString("F2"))));
                        TableCell quantityCell = new TableCell(new Paragraph(new Run(item.Quantity.ToString())));
                        TableCell discountCell = new TableCell(new Paragraph(new Run(item.BonusQuantity.ToString())));
                        TableCell totalCell = new TableCell(new Paragraph(new Run(item.Total.ToString("F2"))));

                        // Align text
                        foreach (var cell in new[] { nameCell, rateCell, quantityCell, discountCell, totalCell })
                        {
                            cell.TextAlignment = TextAlignment.Left;
                            cell.Padding = new Thickness(5);
                        }
                        totalCell.TextAlignment = TextAlignment.Right; // Right align total

                        row.Cells.Add(nameCell);
                        row.Cells.Add(rateCell);
                        row.Cells.Add(quantityCell);
                        row.Cells.Add(discountCell);
                        row.Cells.Add(totalCell);

                        bodyGroup.Rows.Add(row);
                        isAlternate = !isAlternate;
                    }
                }
                else
                {
                    MessageBox.Show("Error: Data is missing!", "Null Reference Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }


                table.RowGroups.Add(bodyGroup);

                // Add the table to the document
                doc.Blocks.Add(table);

                // Add summary section
                // Calculate subtotal and total discount
                decimal subtotal = 0;
                decimal totalDiscount = 0;
                if (_invoiceItems != null)
                {
                    foreach (var item in _invoiceItems)
                    {
                        subtotal += item.PurchaseRate * item.Quantity;
                    }
                }
                else
                {
                    // Handle the case where _invoiceItems is null
                    MessageBox.Show("Error: Invoice items list is null!", "Null Reference Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        private void ResetForm()
        {
            _invoiceItems?.Clear();
            SupplierComboBox.SelectedIndex = -1;
            LoadInvoiceNumber();
            TotalDiscountTextBlock.Text = "0.00";
            TotalAmountTextBlock.Text = "0.00";

            // Clear any other state as needed
            _editingItem = null;
            SelectedItemNameTextBlock.Text = string.Empty;
            SelectedItemNameTextBlock.Tag = null;
            ItemRateTextBox.Text = string.Empty;
            ItemQuantityTextBox.Text = string.Empty;
            ItemBonusQuantityTextBox.Text = string.Empty; // Clear bonus quantity
            ItemTotalTextBox.Text = string.Empty;
            NewItemExpiryDatePicker.SelectedDate = null;
            ErrorLabel.Visibility = Visibility.Collapsed;
            _reservedQuantities?.Clear();
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all fields and reset the form
            _invoiceItems?.Clear();
            InvoiceItemsListView.Items.Clear();
            SupplierComboBox.SelectedIndex = -1;
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
                UpdateTotals();
            }
        }
        // Item Selection and Search
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

        // Event handlers for popups
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

        private void ItemDetailsPopup_Opened(object sender, EventArgs e)
        {
            ItemRateTextBox.Focus();
            
            // Initialize date dropdowns if needed
            if (ExpiryDayComboBox.Items.Count == 0)
            {
                InitializeDateDropdowns();
            }
            
            // If editing an item with existing expiry date, set the dropdowns accordingly
            if (_editingItem != null && _editingItem.ExpiryDate.HasValue)
            {
                SetDateDropdownsFromDateTime(_editingItem.ExpiryDate.Value);
            }
            else
            {
                // Set default value to one year from today
                DateTime defaultExpiryDate = DateTime.Today.AddYears(1);
                SetDateDropdownsFromDateTime(defaultExpiryDate);
            }
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

        // Item details text changed events
        private void ItemDetails_TextChanged(object sender, TextChangedEventArgs e)
        {
            CalculateTotal();

            // For purchase invoices we don't need quantity validation
            // since we're adding inventory, not consuming it
        }

        private void CancelItemButton_Click(object sender, RoutedEventArgs e)
        {
            ItemDetailsPopup.IsOpen = false;
            ErrorLabel.Visibility = Visibility.Collapsed;
            _editingItem = null;
            SelectedItemNameTextBlock.Text = string.Empty;
            SelectedItemNameTextBlock.Tag = null;
            ItemRateTextBox.Text = string.Empty;
            RetailPriceTextBox.Text = string.Empty; // Reset retail price field
            ItemQuantityTextBox.Text = string.Empty;
            ItemTotalTextBox.Text = string.Empty;
            NewItemExpiryDatePicker.SelectedDate = null;
        }


        // Add New Item functionality for creating new inventory items
        private void AddNewItemButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear fields for new item entry
            NewItemNameTextBox.Text = "";
            NewItemCompanyTextBox.Text = "";

            AddNewItemPopup.IsOpen = true;
            NewItemNameTextBox.Focus();
        }

        private void AddNewItemPopup_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                AddNewItemPopup.IsOpen = false;
            }
            else if (e.Key == Key.Enter)
            {
                SaveNewItemButton_Click(SaveNewItemButton, new RoutedEventArgs());
            }
        }

        private void CancelNewItemButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewItemPopup.IsOpen = false;
        }

        private void SaveNewItemButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(NewItemNameTextBox.Text))
            {
                _toastNotification?.Show("Please Enter Item Name", ToastNotification.NotificationType.Error);
                return;
            }

            using (var connection = _databaseHelper?.GetConnection())
            {
                connection?.Open();
                using (var transaction = connection?.BeginTransaction())
                {
                    try
                    {
                        // Insert new item with ALL required fields
                        string insertItemQuery = @"
                    INSERT INTO Items (Name, CompanyName, LatestPurchaseRate, LatestRetailPrice, TotalQuantity)
                    VALUES (@Name, @CompanyName, @LatestPurchaseRate, @LatestRetailPrice, @TotalQuantity)
                    RETURNING Id;";

                        int newItemId;
                        using (var command = new SQLiteCommand(insertItemQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@Name", NewItemNameTextBox.Text.Trim());

                            // Handle CompanyName properly
                            if (string.IsNullOrWhiteSpace(NewItemCompanyTextBox.Text))
                            {
                                command.Parameters.AddWithValue("@CompanyName", DBNull.Value);
                            }
                            else
                            {
                                command.Parameters.AddWithValue("@CompanyName", NewItemCompanyTextBox.Text.Trim());
                            }

                            // Include required fields with default values
                            command.Parameters.AddWithValue("@LatestPurchaseRate", 0.0);
                            command.Parameters.AddWithValue("@TotalQuantity", 0);
                            command.Parameters.AddWithValue("@LatestRetailPrice", 0.0); 

                            // Get the new item's ID
                            newItemId = Convert.ToInt32(command.ExecuteScalar());
                        }

                        transaction?.Commit();

                        // Add the new item to our list and select it
                        var newItem = new Item
                        {
                            Id = newItemId,
                            Name = NewItemNameTextBox.Text.Trim(),
                            PurchaseRate = 0,  // Initialize with default value
                            RetailPrice = 0,   // Initialize with default value
                            AvailableQuantity = 0  // Initialize with default value
                        };

                        _allItems?.Add(newItem);

                        // Close the popup and select the new item
                        AddNewItemPopup.IsOpen = false;

                        _toastNotification?.Show($"Item '{newItem.Name}' has been created successfully!", ToastNotification.NotificationType.Success);

                        
                    }
                    catch (Exception ex)
                    {
                        transaction?.Rollback();
                        _toastNotification?.Show($"Error creating new item {ex}", ToastNotification.NotificationType.Error);
                    }
                }
            }
        }
        
        // Initialize the date dropdown values
        private void InitializeDateDropdowns()
        {
            // Clear existing items
            ExpiryDayComboBox.Items.Clear();
            ExpiryMonthComboBox.Items.Clear();
            ExpiryYearComboBox.Items.Clear();
            
            // Fill days (1-31)
            for (int day = 1; day <= 31; day++)
            {
                ExpiryDayComboBox.Items.Add(day.ToString("00"));
            }
            
            // Fill months (January-December)
            string[] monthNames = System.Globalization.DateTimeFormatInfo.CurrentInfo.MonthNames;
            for (int month = 0; month < 12; month++)
            {
                ExpiryMonthComboBox.Items.Add($"{month + 1:00} - {monthNames[month]}");
            }
            
            // Fill years (current year + 10 years)
            int currentYear = DateTime.Now.Year;
            for (int year = currentYear; year <= currentYear + 10; year++)
            {
                ExpiryYearComboBox.Items.Add(year.ToString());
            }
        }

        // Set dropdown values from a DateTime
        private void SetDateDropdownsFromDateTime(DateTime date)
        {
            ExpiryDayComboBox.SelectedIndex = date.Day - 1;
            ExpiryMonthComboBox.SelectedIndex = date.Month - 1;
            
            int yearIndex = ExpiryYearComboBox.Items.IndexOf(date.Year.ToString());
            if (yearIndex >= 0)
            {
                ExpiryYearComboBox.SelectedIndex = yearIndex;
            }
            else
            {
                // If the year isn't in the dropdown (older date), select the first year
                ExpiryYearComboBox.SelectedIndex = 0;
            }
        }

        // Event handler for dropdown selection changes
        private void ExpiryDate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDatePickerFromDropdowns();
        }

        // Update the hidden DatePicker from dropdown values
        private void UpdateDatePickerFromDropdowns()
        {
            if (ExpiryDayComboBox.SelectedIndex >= 0 && 
                ExpiryMonthComboBox.SelectedIndex >= 0 && 
                ExpiryYearComboBox.SelectedIndex >= 0)
            {
                try
                {
                    int day = ExpiryDayComboBox.SelectedIndex + 1;
                    int month = ExpiryMonthComboBox.SelectedIndex + 1;
                    int year = int.Parse(ExpiryYearComboBox.SelectedItem?.ToString() ?? throw new InvalidOperationException("Year selection is null."));
                    
                    // Validate the date (handle cases like February 30)
                    if (IsValidDate(year, month, day))
                    {
                        DateTime selectedDate = new DateTime(year, month, day);
                        NewItemExpiryDatePicker.SelectedDate = selectedDate;
                    }
                    else
                    {
                        // If date is invalid, adjust the day to the last day of the month
                        int lastDay = DateTime.DaysInMonth(year, month);
                        ExpiryDayComboBox.SelectedIndex = lastDay - 1;
                    }
                }
                catch
                {
                    // Handle any conversion errors
                    NewItemExpiryDatePicker.SelectedDate = null;
                }
            }
            else
            {
                NewItemExpiryDatePicker.SelectedDate = null;
            }
        }

        // Helper to validate if a date is valid
        private bool IsValidDate(int year, int month, int day)
        {
            if (month < 1 || month > 12)
                return false;
                
            int lastDay = DateTime.DaysInMonth(year, month);
            return day >= 1 && day <= lastDay;
        }
    }
}
