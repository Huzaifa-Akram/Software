using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging; // Add this using directive
using Software.Data;

namespace Software
{
    public partial class PurchaseDetailPage : Page, INotifyPropertyChanged
    {
        private DatabaseHelper? _databaseHelper;
        private ObservableCollection<PurchaseInvoice>? _purchaseInvoices;
        private ObservableCollection<InvoiceItem>? _invoiceItems;
        private ICollectionView? _purchaseInvoicesView;
        private bool? _isInvoicePopupOpen;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsInvoicePopupOpen
        {
            get => _isInvoicePopupOpen ?? false;
            set
            {
                _isInvoicePopupOpen = value;
                OnPropertyChanged(nameof(IsInvoicePopupOpen));
            }
        }

        public DateTime InvoiceDate { get; private set; }

        public PurchaseDetailPage()
        {
            InitializeComponent();
            DataContext = this; // Ensure DataContext is set
            _databaseHelper = new DatabaseHelper();
            _purchaseInvoices = new ObservableCollection<PurchaseInvoice>();
            _invoiceItems = new ObservableCollection<InvoiceItem>();

            LoadPurchaseData();
        }

        // Add RetailPrice and BonusQuantity properties to the InvoiceItem class
        public class InvoiceItem
        {
            public int ItemId { get; set; }
            public required string ItemName { get; set; }
            public decimal PurchaseRate { get; set; }
            public decimal RetailPrice { get; set; } // Added property
            public int Quantity { get; set; }
            public int BonusQuantity { get; set; } // Added property
            public decimal Total { get; set; }
            public decimal DiscountPercentage { get; set; }
            public DateTime? ExpiryDate { get; set; }
        }

        private void LoadPurchaseData()
        {
            try
            {
                if (_purchaseInvoices == null)
                {
                    _purchaseInvoices = new ObservableCollection<PurchaseInvoice>();
                }

                _purchaseInvoices.Clear();

                bool showAll = DateRangeSelector?.SelectedIndex == 0;
                DateTime? fromDate = showAll ? null : FromDatePicker?.SelectedDate;
                DateTime? toDate = showAll ? null : ToDatePicker?.SelectedDate;

                using (var connection = _databaseHelper?.GetConnection())
                {
                    connection?.Open();

                    string query;

                    if (showAll)
                    {
                        query = @"
                    SELECT 
                        i.InvoiceNumber,
                        i.InvoiceDate,
                        i.SupplierName,
                        (SELECT SUM(ii.Total) FROM InvoiceItems ii WHERE ii.InvoiceNumber = i.InvoiceNumber) AS TotalCost,
                        0 AS TotalCostOfGoodsSold,
                        0 AS Profit
                    FROM 
                        Invoices i
                    WHERE 
                        i.InvoiceType = 'P'
                    ORDER BY 
                        i.InvoiceDate DESC";
                    }
                    else
                    {
                        if (toDate.HasValue)
                        {
                            toDate = toDate.Value.AddDays(1).AddSeconds(-1);
                        }

                        query = @"
                    SELECT 
                        i.InvoiceNumber,
                        i.InvoiceDate,
                        i.SupplierName,
                        (SELECT SUM(ii.Total) FROM InvoiceItems ii WHERE ii.InvoiceNumber = i.InvoiceNumber) AS TotalCost,
                        0 AS TotalCostOfGoodsSold,
                        0 AS Profit
                    FROM 
                        Invoices i
                    WHERE 
                        i.InvoiceType = 'P' 
                        AND i.InvoiceDate BETWEEN @FromDate AND @ToDate
                    ORDER BY 
                        i.InvoiceDate DESC";
                    }

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        if (!showAll)
                        {
                            command.Parameters.AddWithValue("@FromDate", fromDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                            command.Parameters.AddWithValue("@ToDate", toDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty);
                        }

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var invoice = new PurchaseInvoice
                                {
                                    InvoiceDate = DateTime.Parse(reader["InvoiceDate"]?.ToString() ?? throw new InvalidOperationException("InvoiceDate is null")),
                                    SupplierName = reader["SupplierName"]?.ToString() ?? "Unknown",
                                    TotalCost = reader["TotalCost"] != DBNull.Value ? Convert.ToDecimal(reader["TotalCost"]) : 0m,
                                    TotalCostOfGoodsSold = reader["TotalCostOfGoodsSold"] != DBNull.Value ? Convert.ToDecimal(reader["TotalCostOfGoodsSold"]) : 0m,
                                    InvoiceNumber = reader["InvoiceNumber"]?.ToString() ?? string.Empty,
                                    Profit = reader["Profit"] != DBNull.Value ? Convert.ToDecimal(reader["Profit"]) : 0m
                                };

                                if (invoice.TotalCost > 0)
                                {
                                    invoice.ProfitPercentage = invoice.Profit / invoice.TotalCost;
                                }

                                _purchaseInvoices.Add(invoice);
                            }
                        }
                    }
                }

                PurchaseDataGrid.ItemsSource = _purchaseInvoices;
                _purchaseInvoicesView = CollectionViewSource.GetDefaultView(_purchaseInvoices);

                if (ResultCountTextBlock != null)
                {
                    ResultCountTextBlock.Text = $"Showing {_purchaseInvoices.Count} results";
                }

                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading purchase data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Update the LoadInvoiceItems method to retrieve RetailPrice and BonusQuantity
        private void LoadInvoiceItems(string invoiceNumber)
        {
            _invoiceItems?.Clear();

            if (_databaseHelper == null)
            {
                throw new InvalidOperationException("DatabaseHelper is not initialized.");
            }

            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();

                string query = @"
                SELECT 
                    i.Name AS ItemName,
                    ii.Rate AS PurchaseRate,
                    ii.RetailPrice, 
                    ii.Quantity,
                    ii.BonusQuantity,
                    ii.DiscountPercentage,
                    ii.Total
                FROM 
                    InvoiceItems ii
                JOIN 
                    Items i ON ii.ItemId = i.Id
                WHERE 
                    ii.InvoiceNumber = @InvoiceNumber";

                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var item = new InvoiceItem
                            {
                                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                                PurchaseRate = Convert.ToDecimal(reader["PurchaseRate"]),
                                RetailPrice = reader.IsDBNull(reader.GetOrdinal("RetailPrice")) ? 0m : Convert.ToDecimal(reader["RetailPrice"]),
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                BonusQuantity = reader.IsDBNull(reader.GetOrdinal("BonusQuantity")) ? 0 : Convert.ToInt32(reader["BonusQuantity"]),
                                DiscountPercentage = Convert.ToDecimal(reader["DiscountPercentage"]),
                                Total = Convert.ToDecimal(reader["Total"])
                            };

                            _invoiceItems?.Add(item);
                        }
                    }
                }
            }
        }

        private void UpdateSummary()
        {
            decimal totalCost = 0;
            decimal totalProfit = 0;

            if (_purchaseInvoicesView != null)
            {
                foreach (PurchaseInvoice invoice in _purchaseInvoicesView)
                {
                    totalCost += invoice.TotalCost;
                    totalProfit += invoice.Profit;
                }
            }

            if (TotalCostTextBlock != null)
            {
                TotalCostTextBlock.Text = totalCost.ToString("C2");
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPurchaseData();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_purchaseInvoicesView != null)
            {
                string searchText = SearchTextBox.Text.ToLower();

                if (string.IsNullOrEmpty(searchText))
                {
                    _purchaseInvoicesView.Filter = null;
                }
                else
                {
                    _purchaseInvoicesView.Filter = o =>
                    {
                        var invoice = o as PurchaseInvoice;
                        return invoice != null &&
                               (invoice.InvoiceNumber.ToLower().Contains(searchText) ||
                                invoice.SupplierName.ToLower().Contains(searchText));
                    };
                }

                int filteredCount = _purchaseInvoicesView.Cast<object>().Count();
                if (ResultCountTextBlock != null)
                {
                    ResultCountTextBlock.Text = $"Showing {filteredCount} results";
                }

                UpdateSummary();
            }
        }

        private void PurchaseDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (PurchaseDataGrid.SelectedItem is PurchaseInvoice selectedInvoice)
            {
                LoadInvoiceItems(selectedInvoice.InvoiceNumber);

                PopupInvoiceNumberTextBlock.Text = selectedInvoice.InvoiceNumber;
                PopupInvoiceDateTextBlock.Text = selectedInvoice.InvoiceDate.ToString("yyyy-MM-dd");
                PopupSupplierNameTextBlock.Text = selectedInvoice.SupplierName;
                PopupTotalCostTextBlock.Text = selectedInvoice.TotalCost.ToString("C2");

                PopupItemsDataGrid.ItemsSource = _invoiceItems;

                IsInvoicePopupOpen = true;
            }
        }

        private void ClosePopupButton_Click(object sender, RoutedEventArgs e)
        {
            IsInvoicePopupOpen = false;
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            if (PurchaseDataGrid.SelectedItem is PurchaseInvoice selectedInvoice)
            {
                PrintInvoice(selectedInvoice.InvoiceNumber);
            }
        }

        private void PrintInvoice(string invoiceNumber)
        {
            var selectedInvoice = _purchaseInvoices?.FirstOrDefault(i => i.InvoiceNumber == invoiceNumber);
            if (selectedInvoice == null) return;

            PrintDialog printDialog = new PrintDialog();

            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = new FlowDocument();
                doc.PagePadding = new Thickness(50);
                doc.ColumnWidth = printDialog.PrintableAreaWidth;

                try
                {
                    BitmapImage logo = new BitmapImage(new Uri("pack://application:,,,/Software;component/Resources/logo.png"));
                    Image logoImage = new Image
                    {
                        Source = logo,
                        Width = 150,
                        Height = 80,
                        Stretch = Stretch.Uniform
                    };

                    BlockUIContainer logoContainer = new BlockUIContainer(logoImage);

                    Section logoSection = new Section();
                    logoSection.TextAlignment = TextAlignment.Center;
                    logoSection.Blocks.Add(logoContainer);
                    doc.Blocks.Add(logoSection);
                }
                catch (Exception)
                {
                    Paragraph companyNameAlt = new Paragraph(new Run("YourCompany"));
                    companyNameAlt.FontSize = 28;
                    companyNameAlt.FontWeight = FontWeights.Bold;
                    companyNameAlt.TextAlignment = TextAlignment.Center;
                    doc.Blocks.Add(companyNameAlt);
                }

                Paragraph companyName = new Paragraph(new Run("Your Company Name"));
                companyName.FontSize = 22;
                companyName.FontWeight = FontWeights.Bold;
                companyName.TextAlignment = TextAlignment.Center;
                doc.Blocks.Add(companyName);

                Paragraph companyAddress = new Paragraph(new Run("123 Business Street, City, Country"));
                companyAddress.FontSize = 14;
                companyAddress.TextAlignment = TextAlignment.Center;
                doc.Blocks.Add(companyAddress);

                Paragraph companyContact = new Paragraph(new Run("Tel: +1-234-567-8900 | Email: sales@yourcompany.com"));
                companyContact.FontSize = 12;
                companyContact.TextAlignment = TextAlignment.Center;
                doc.Blocks.Add(companyContact);

                Border border = new Border();
                border.BorderBrush = Brushes.Gray;
                border.BorderThickness = new Thickness(0, 1, 0, 0);
                border.Padding = new Thickness(0, 5, 0, 5);
                BlockUIContainer borderContainer = new BlockUIContainer(border);
                doc.Blocks.Add(borderContainer);

                Paragraph header = new Paragraph(new Run("PURCHASE INVOICE"));
                header.FontSize = 20;
                header.FontWeight = FontWeights.Bold;
                header.TextAlignment = TextAlignment.Center;
                doc.Blocks.Add(header);

                Table infoTable = new Table();
                infoTable.CellSpacing = 0;

                infoTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                infoTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

                TableRowGroup infoGroup = new TableRowGroup();

                TableRow infoRow = new TableRow();

                TableCell supplierCell = new TableCell();
                Paragraph supplierTitle = new Paragraph(new Bold(new Run("SUPPLIER:")));
                supplierTitle.FontSize = 12;
                supplierTitle.Margin = new Thickness(0, 0, 0, 5);

                Paragraph supplierDetails = new Paragraph();
                supplierDetails.Inlines.Add(new Run($"{selectedInvoice.SupplierName}"));
                supplierDetails.Margin = new Thickness(0);
                supplierDetails.FontSize = 12;

                supplierCell.Blocks.Add(supplierTitle);
                supplierCell.Blocks.Add(supplierDetails);

                TableCell invoiceDetailsCell = new TableCell();
                Paragraph invoiceDetailsTitle = new Paragraph();
                invoiceDetailsTitle.Inlines.Add(new Bold(new Run("DETAILS:")));
                invoiceDetailsTitle.Margin = new Thickness(0, 0, 0, 5);
                invoiceDetailsTitle.FontSize = 12;

                Paragraph invoiceNumberPara = new Paragraph();
                invoiceNumberPara.Inlines.Add(new Bold(new Run("Invoice #: ")));
                invoiceNumberPara.Inlines.Add(new Run($"{selectedInvoice.InvoiceNumber}"));
                invoiceNumberPara.Margin = new Thickness(0);
                invoiceNumberPara.FontSize = 12;

                Paragraph invoiceDatePara = new Paragraph();
                invoiceDatePara.Inlines.Add(new Bold(new Run("Date: ")));
                invoiceDatePara.Inlines.Add(new Run($"{selectedInvoice.InvoiceDate.ToString("MMMM dd, yyyy")}"));
                invoiceDatePara.Margin = new Thickness(0);
                invoiceDatePara.FontSize = 12;

                invoiceDetailsCell.Blocks.Add(invoiceDetailsTitle);
                invoiceDetailsCell.Blocks.Add(invoiceNumberPara);
                invoiceDetailsCell.Blocks.Add(invoiceDatePara);

                infoRow.Cells.Add(supplierCell);
                infoRow.Cells.Add(invoiceDetailsCell);
                infoGroup.Rows.Add(infoRow);
                infoTable.RowGroups.Add(infoGroup);

                doc.Blocks.Add(infoTable);

                doc.Blocks.Add(new Paragraph(new Run(" ")) { Margin = new Thickness(0, 10, 0, 10) });

                Table table = new Table();
                table.CellSpacing = 0;
                table.BorderBrush = Brushes.Black;
                table.BorderThickness = new Thickness(1);

                // Updated columns - removed discount, added expiry date
                table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });

                TableRowGroup headerGroup = new TableRowGroup();
                TableRow headerRow = new TableRow();
                headerRow.Background = Brushes.LightGray;

                TableCell itemNameHeader = new TableCell(new Paragraph(new Bold(new Run("Item Description"))));
                TableCell rateHeader = new TableCell(new Paragraph(new Bold(new Run("Unit Price"))));
                TableCell qtyHeader = new TableCell(new Paragraph(new Bold(new Run("Qty"))));
                TableCell expiryHeader = new TableCell(new Paragraph(new Bold(new Run("Expiry Date"))));
                TableCell totalHeader = new TableCell(new Paragraph(new Bold(new Run("Amount"))));

                itemNameHeader.TextAlignment = TextAlignment.Left;
                rateHeader.TextAlignment = TextAlignment.Left;
                qtyHeader.TextAlignment = TextAlignment.Left;
                expiryHeader.TextAlignment = TextAlignment.Left;
                totalHeader.TextAlignment = TextAlignment.Right;

                itemNameHeader.Padding = new Thickness(5);
                rateHeader.Padding = new Thickness(5);
                qtyHeader.Padding = new Thickness(5);
                expiryHeader.Padding = new Thickness(5);
                totalHeader.Padding = new Thickness(5);

                headerRow.Cells.Add(itemNameHeader);
                headerRow.Cells.Add(rateHeader);
                headerRow.Cells.Add(qtyHeader);
                headerRow.Cells.Add(expiryHeader);
                headerRow.Cells.Add(totalHeader);

                headerGroup.Rows.Add(headerRow);
                table.RowGroups.Add(headerGroup);

                TableRowGroup bodyGroup = new TableRowGroup();
                bool isAlternate = false;

                if (_invoiceItems != null && bodyGroup != null)
                {
                    foreach (var item in _invoiceItems.Where(i => i != null))
                    {
                        TableRow row = new TableRow();
                        if (isAlternate)
                            row.Background = Brushes.WhiteSmoke;

                        string itemName = item.ItemName ?? "N/A";
                        string purchaseRate = item.PurchaseRate.ToString("F2");
                        string quantity = item.Quantity.ToString();
                        string expiryDate = item.ExpiryDate.HasValue ? item.ExpiryDate.Value.ToString("yyyy-MM-dd") : "N/A";
                        string total = item.Total.ToString("F2");

                        TableCell nameCell = new TableCell(new Paragraph(new Run(itemName)));
                        TableCell rateCell = new TableCell(new Paragraph(new Run(purchaseRate)));
                        TableCell quantityCell = new TableCell(new Paragraph(new Run(quantity)));
                        TableCell expiryCell = new TableCell(new Paragraph(new Run(expiryDate)));
                        TableCell totalCell = new TableCell(new Paragraph(new Run(total)));

                        nameCell.TextAlignment = TextAlignment.Left;
                        rateCell.TextAlignment = TextAlignment.Left;
                        quantityCell.TextAlignment = TextAlignment.Left;
                        expiryCell.TextAlignment = TextAlignment.Left;
                        totalCell.TextAlignment = TextAlignment.Right;

                        nameCell.Padding = new Thickness(5);
                        rateCell.Padding = new Thickness(5);
                        quantityCell.Padding = new Thickness(5);
                        expiryCell.Padding = new Thickness(5);
                        totalCell.Padding = new Thickness(5);

                        row.Cells.Add(nameCell);
                        row.Cells.Add(rateCell);
                        row.Cells.Add(quantityCell);
                        row.Cells.Add(expiryCell);
                        row.Cells.Add(totalCell);

                        bodyGroup.Rows.Add(row);
                        isAlternate = !isAlternate;
                    }
                }
                else
                {
                    MessageBox.Show("Error: Invoice items or table body is missing!", "Null Reference Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                table.RowGroups.Add(bodyGroup);

                doc.Blocks.Add(table);

                decimal subtotal = 0;
                if (_invoiceItems != null)
                {
                    foreach (var item in _invoiceItems.Where(i => i != null))
                    {
                        subtotal += item.Total;
                    }
                }

                Table totalsTable = new Table();
                totalsTable.CellSpacing = 0;

                totalsTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                totalsTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

                TableRowGroup totalsGroup = new TableRowGroup();

                // Total row
                TableRow totalRow = new TableRow();
                totalRow.Background = Brushes.LightGray;

                TableCell totalLabelCell = new TableCell();
                Paragraph totalLabelPara = new Paragraph(new Bold(new Run("TOTAL:")));
                totalLabelPara.FontSize = 14;
                totalLabelCell.Blocks.Add(totalLabelPara);
                totalLabelCell.TextAlignment = TextAlignment.Right;
                totalLabelCell.Padding = new Thickness(5);

                TableCell totalValueCell = new TableCell();
                Paragraph totalValuePara = new Paragraph(new Bold(new Run(subtotal.ToString("F2"))));
                totalValuePara.FontSize = 14;
                totalValueCell.Blocks.Add(totalValuePara);
                totalValueCell.TextAlignment = TextAlignment.Right;
                totalValueCell.Padding = new Thickness(5);

                totalRow.Cells.Add(totalLabelCell);
                totalRow.Cells.Add(totalValueCell);
                totalsGroup.Rows.Add(totalRow);

                totalsTable.RowGroups.Add(totalsGroup);
                doc.Blocks.Add(totalsTable);

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
                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Purchase Invoice");
            }
        }

        private void DateRangeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Show or hide custom date panel based on selection
            if (DateRangeSelector.SelectedIndex == 11) // Custom Range option
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
                    case 2: // Yesterday
                        startDate = now.AddDays(-1).Date;
                        endDate = now.AddDays(-1).Date;
                        break;
                    case 3: // Last 7 Days
                        startDate = now.AddDays(-7).Date;
                        endDate = now.Date;
                        break;
                    case 4: // Last 30 Days
                        startDate = now.AddDays(-30).Date;
                        endDate = now.Date;
                        break;
                    case 5: // This Month
                        startDate = new DateTime(now.Year, now.Month, 1);
                        endDate = now.Date;
                        break;
                    case 6: // Last Month
                        startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                        endDate = new DateTime(now.Year, now.Month, 1).AddDays(-1);
                        break;
                    case 7: // This Quarter
                        int currentQuarter = (now.Month - 1) / 3 + 1;
                        startDate = new DateTime(now.Year, (currentQuarter - 1) * 3 + 1, 1);
                        endDate = now.Date;
                        break;
                    case 8: // Last Quarter
                        int lastQuarter = ((now.Month - 1) / 3);
                        if (lastQuarter == 0) // If we're in Q1, last quarter was Q4 of previous year
                        {
                            startDate = new DateTime(now.Year - 1, 10, 1);
                            endDate = new DateTime(now.Year - 1, 12, 31);
                        }
                        else
                        {
                            startDate = new DateTime(now.Year, (lastQuarter - 1) * 3 + 1, 1);
                            endDate = new DateTime(now.Year, lastQuarter * 3, 1).AddDays(-1);
                        }
                        break;
                    case 9: // This Year
                        startDate = new DateTime(now.Year, 1, 1);
                        endDate = now.Date;
                        break;
                    case 10: // Last Year
                        startDate = new DateTime(now.Year - 1, 1, 1);
                        endDate = new DateTime(now.Year - 1, 12, 31);
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

                LoadPurchaseData();
            }
        }
    }

    public class PurchaseInvoice : INotifyPropertyChanged
    {
        private string _invoiceNumber = string.Empty; // Initialize with a default value
        private DateTime _invoiceDate;
        private string _supplierName = string.Empty; // Initialize with a default value
        private decimal _totalCost;
        private decimal _totalCostOfGoodsSold;
        private decimal _profit;
        private decimal _profitPercentage;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set
            {
                if (_invoiceNumber != value)
                {
                    _invoiceNumber = value;
                    OnPropertyChanged(nameof(InvoiceNumber));
                }
            }
        }

        public DateTime InvoiceDate
        {
            get => _invoiceDate;
            set
            {
                if (_invoiceDate != value)
                {
                    _invoiceDate = value;
                    OnPropertyChanged(nameof(InvoiceDate));
                }
            }
        }

        public string SupplierName
        {
            get => _supplierName;
            set
            {
                if (_supplierName != value)
                {
                    _supplierName = value;
                    OnPropertyChanged(nameof(SupplierName));
                }
            }
        }

        public decimal TotalCost
        {
            get => _totalCost;
            set
            {
                if (_totalCost != value)
                {
                    _totalCost = value;
                    OnPropertyChanged(nameof(TotalCost));
                }
            }
        }

        public decimal TotalCostOfGoodsSold
        {
            get => _totalCostOfGoodsSold;
            set
            {
                if (_totalCostOfGoodsSold != value)
                {
                    _totalCostOfGoodsSold = value;
                    OnPropertyChanged(nameof(TotalCostOfGoodsSold));
                }
            }
        }

        public decimal Profit
        {
            get => _profit;
            set
            {
                if (_profit != value)
                {
                    _profit = value;
                    OnPropertyChanged(nameof(Profit));
                }
            }
        }

        public decimal ProfitPercentage
        {
            get => _profitPercentage;
            set
            {
                if (_profitPercentage != value)
                {
                    _profitPercentage = value;
                    OnPropertyChanged(nameof(ProfitPercentage));
                }
            }
        }
    }
}