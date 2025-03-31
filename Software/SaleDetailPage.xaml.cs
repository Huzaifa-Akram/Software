using System;
// ...existing code...
using System.Net.Http;
// ...existing code...
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
using System.Windows.Media.Imaging;
using Software.Data;
using static Software.PurchaseInvoicePage;

namespace Software
{
    public partial class SaleDetailPage : Page, INotifyPropertyChanged
    {
        private DatabaseHelper? _databaseHelper;
        private ObservableCollection<SalesInvoice>? _salesInvoices;
        private ObservableCollection<InvoiceItem>? _invoiceItems; // Define _invoiceItems
        private ICollectionView? _salesInvoicesView;
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

        public SaleDetailPage()
        {
            InitializeComponent();
            DataContext = this; // Ensure DataContext is set
            _databaseHelper = new DatabaseHelper();
            _salesInvoices = new ObservableCollection<SalesInvoice>();
            _invoiceItems = new ObservableCollection<InvoiceItem>();

            LoadSalesData();
        }

        public class InvoiceItem
        {
            public int ItemId { get; set; }
            public required string ItemName { get; set; }
            public decimal PurchaseRate { get; set; }
            public int Quantity { get; set; }
            public decimal Total { get; set; }
            public decimal DiscountPercentage { get; set; }
            public DateTime? ExpiryDate { get; set; }
        }

        private void LoadSalesData()
        {
            try
            {
                if (_salesInvoices == null)
                {
                    _salesInvoices = new ObservableCollection<SalesInvoice>();
                }

                _salesInvoices.Clear();

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
                            c.Name AS CustomerName,
                            ir.TotalRevenue,
                            icogs.TotalCostOfGoodsSold,
                            (ir.TotalRevenue - icogs.TotalCostOfGoodsSold) AS Profit
                        FROM 
                            Invoices i
                        LEFT JOIN 
                            Customers c ON i.CustomerId = c.Id
                        JOIN 
                            InvoiceRevenue ir ON i.InvoiceNumber = ir.InvoiceNumber
                        JOIN 
                            InvoiceCostOfGoodsSold icogs ON i.InvoiceNumber = icogs.InvoiceNumber
                        WHERE 
                            i.InvoiceType = 'S'
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
                            c.Name AS CustomerName,
                            ir.TotalRevenue,
                            icogs.TotalCostOfGoodsSold,
                            (ir.TotalRevenue - icogs.TotalCostOfGoodsSold) AS Profit
                        FROM 
                            Invoices i
                        LEFT JOIN 
                            Customers c ON i.CustomerId = c.Id
                        JOIN 
                            InvoiceRevenue ir ON i.InvoiceNumber = ir.InvoiceNumber
                        JOIN 
                            InvoiceCostOfGoodsSold icogs ON i.InvoiceNumber = icogs.InvoiceNumber
                        WHERE 
                            i.InvoiceType = 'S' 
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
                                var invoice = new SalesInvoice
                                {
                                    InvoiceDate = DateTime.Parse(reader["InvoiceDate"]?.ToString() ?? throw new InvalidOperationException("InvoiceDate is null")),
                                    CustomerName = reader["CustomerName"]?.ToString() ?? "Unknown",
                                    TotalRevenue = Convert.ToDecimal(reader["TotalRevenue"]),
                                    TotalCostOfGoodsSold = Convert.ToDecimal(reader["TotalCostOfGoodsSold"]),
                                    InvoiceNumber = reader["InvoiceNumber"]?.ToString() ?? string.Empty,
                                    Profit = Convert.ToDecimal(reader["Profit"])
                                };

                                if (invoice.TotalRevenue > 0)
                                {
                                    invoice.ProfitPercentage = invoice.Profit / invoice.TotalRevenue;
                                }

                                _salesInvoices.Add(invoice);
                            }
                        }
                    }
                }

                SalesDataGrid.ItemsSource = _salesInvoices;
                _salesInvoicesView = CollectionViewSource.GetDefaultView(_salesInvoices);

                if (ResultCountTextBlock != null)
                {
                    ResultCountTextBlock.Text = $"Showing {_salesInvoices.Count} results";
                }

                UpdateSummary();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading sales data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
                    ii.Quantity,
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
                                Quantity = Convert.ToInt32(reader["Quantity"]),
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
            decimal totalSales = 0;
            decimal totalProfit = 0;

            if (_salesInvoicesView != null)
            {
                foreach (SalesInvoice invoice in _salesInvoicesView)
                {
                    totalSales += invoice.TotalRevenue;
                    totalProfit += invoice.Profit;
                }
            }

            if (TotalSalesTextBlock != null)
            {
                TotalSalesTextBlock.Text = totalSales.ToString("C2");
            }
            if (TotalProfitTextBlock != null)
            {
                TotalProfitTextBlock.Text = totalProfit.ToString("C2");
            }
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSalesData();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_salesInvoicesView != null)
            {
                string searchText = SearchTextBox.Text.ToLower();

                if (string.IsNullOrEmpty(searchText))
                {
                    _salesInvoicesView.Filter = null;
                }
                else
                {
                    _salesInvoicesView.Filter = o =>
                    {
                        var invoice = o as SalesInvoice;
                        return invoice != null &&
                               (invoice.InvoiceNumber.ToLower().Contains(searchText) ||
                                invoice.CustomerName.ToLower().Contains(searchText));
                    };
                }

                int filteredCount = _salesInvoicesView.Cast<object>().Count();
                if (ResultCountTextBlock != null)
                {
                    ResultCountTextBlock.Text = $"Showing {filteredCount} results";
                }

                UpdateSummary();
            }
        }

        private void SalesDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SalesDataGrid.SelectedItem is SalesInvoice selectedInvoice)
            {
                LoadInvoiceItems(selectedInvoice.InvoiceNumber);

                PopupInvoiceNumberTextBlock.Text = selectedInvoice.InvoiceNumber;
                PopupInvoiceDateTextBlock.Text = selectedInvoice.InvoiceDate.ToString("yyyy-MM-dd");
                PopupCustomerNameTextBlock.Text = selectedInvoice.CustomerName;
                PopupTotalRevenueTextBlock.Text = selectedInvoice.TotalRevenue.ToString("C2");
                PopupTotalCostTextBlock.Text = selectedInvoice.TotalCostOfGoodsSold.ToString("C2");
                PopupProfitTextBlock.Text = selectedInvoice.Profit.ToString("C2");

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
            if (SalesDataGrid.SelectedItem is SalesInvoice selectedInvoice)
            {
                PrintInvoice(selectedInvoice.InvoiceNumber);
            }
        }

        private void PrintInvoice(string invoiceNumber)
        {
            var selectedInvoice = _salesInvoices?.FirstOrDefault(i => i.InvoiceNumber == invoiceNumber);
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

                Paragraph header = new Paragraph(new Run($"INVOICE"));
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

                TableCell customerCell = new TableCell();
                Paragraph customerTitle = new Paragraph(new Bold(new Run("BILL TO:")));
                customerTitle.FontSize = 12;
                customerTitle.Margin = new Thickness(0, 0, 0, 5);

                Paragraph customerDetails = new Paragraph();
                customerDetails.Inlines.Add(new Run($"{selectedInvoice.CustomerName}"));
                customerDetails.Margin = new Thickness(0);
                customerDetails.FontSize = 12;

                customerCell.Blocks.Add(customerTitle);
                customerCell.Blocks.Add(customerDetails);

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

                infoRow.Cells.Add(customerCell);
                infoRow.Cells.Add(invoiceDetailsCell);
                infoGroup.Rows.Add(infoRow);
                infoTable.RowGroups.Add(infoGroup);

                doc.Blocks.Add(infoTable);

                doc.Blocks.Add(new Paragraph(new Run(" ")) { Margin = new Thickness(0, 10, 0, 10) });

                Table table = new Table();
                table.CellSpacing = 0;
                table.BorderBrush = Brushes.Black;
                table.BorderThickness = new Thickness(1);

                table.Columns.Add(new TableColumn { Width = new GridLength(3, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });

                TableRowGroup headerGroup = new TableRowGroup();
                TableRow headerRow = new TableRow();
                headerRow.Background = Brushes.LightGray;

                TableCell itemNameHeader = new TableCell(new Paragraph(new Bold(new Run("Item Description"))));
                TableCell rateHeader = new TableCell(new Paragraph(new Bold(new Run("Unit Price"))));
                TableCell qtyHeader = new TableCell(new Paragraph(new Bold(new Run("Qty"))));
                TableCell discountHeader = new TableCell(new Paragraph(new Bold(new Run("Discount"))));
                TableCell totalHeader = new TableCell(new Paragraph(new Bold(new Run("Amount"))));

                itemNameHeader.TextAlignment = TextAlignment.Left;
                rateHeader.TextAlignment = TextAlignment.Left;
                qtyHeader.TextAlignment = TextAlignment.Left;
                discountHeader.TextAlignment = TextAlignment.Left;
                totalHeader.TextAlignment = TextAlignment.Right;

                itemNameHeader.Padding = new Thickness(5);
                rateHeader.Padding = new Thickness(5);
                qtyHeader.Padding = new Thickness(5);
                discountHeader.Padding = new Thickness(5);
                totalHeader.Padding = new Thickness(5);

                headerRow.Cells.Add(itemNameHeader);
                headerRow.Cells.Add(rateHeader);
                headerRow.Cells.Add(qtyHeader);
                headerRow.Cells.Add(discountHeader);
                headerRow.Cells.Add(totalHeader);

                headerGroup.Rows.Add(headerRow);
                table.RowGroups.Add(headerGroup);

                TableRowGroup bodyGroup = new TableRowGroup();
                bool isAlternate = false;

                if (_invoiceItems != null && bodyGroup != null) // Ensure objects are not null
                {
                    foreach (var item in _invoiceItems.Where(i => i != null)) // Skip null items
                    {
                        TableRow row = new TableRow();
                        if (isAlternate)
                            row.Background = Brushes.WhiteSmoke;

                        // Ensure properties are not null before using them
                        string itemName = item.ItemName ?? "N/A";
                        string purchaseRate = item.PurchaseRate.ToString("F2");
                        string quantity = item.Quantity.ToString();
                        string discountPercentage = item.DiscountPercentage.ToString("F2") + "%";
                        string total = item.Total.ToString("F2");

                        TableCell nameCell = new TableCell(new Paragraph(new Run(itemName)));
                        TableCell rateCell = new TableCell(new Paragraph(new Run(purchaseRate)));
                        TableCell quantityCell = new TableCell(new Paragraph(new Run(quantity)));
                        TableCell discountCell = new TableCell(new Paragraph(new Run(discountPercentage)));
                        TableCell totalCell = new TableCell(new Paragraph(new Run(total)));

                        nameCell.TextAlignment = TextAlignment.Left;
                        rateCell.TextAlignment = TextAlignment.Left;
                        quantityCell.TextAlignment = TextAlignment.Left;
                        discountCell.TextAlignment = TextAlignment.Left;
                        totalCell.TextAlignment = TextAlignment.Right;

                        nameCell.Padding = new Thickness(5);
                        rateCell.Padding = new Thickness(5);
                        quantityCell.Padding = new Thickness(5);
                        discountCell.Padding = new Thickness(5);
                        totalCell.Padding = new Thickness(5);

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
                    MessageBox.Show("Error: Invoice items or table body is missing!", "Null Reference Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }


                table.RowGroups.Add(bodyGroup);

                doc.Blocks.Add(table);

                decimal subtotal = 0;
                decimal totalDiscount = 0;
                if (_invoiceItems != null) // Ensure the list is not null
                {
                    foreach (var item in _invoiceItems.Where(i => i != null)) // Skip null items
                    {
                        subtotal += item.PurchaseRate * item.Quantity;
                    }
                }
                else
                {
                    MessageBox.Show("Error: Invoice items list is null!", "Null Reference Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }


                Table totalsTable = new Table();
                totalsTable.CellSpacing = 0;

                totalsTable.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
                totalsTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

                TableRowGroup totalsGroup = new TableRowGroup();

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

                TableRow totalRow = new TableRow();
                totalRow.Background = Brushes.LightGray;

                TableCell totalLabelCell = new TableCell();
                Paragraph totalLabelPara = new Paragraph(new Bold(new Run("TOTAL:")));
                totalLabelPara.FontSize = 14;
                totalLabelCell.Blocks.Add(totalLabelPara);
                totalLabelCell.TextAlignment = TextAlignment.Right;
                totalLabelCell.Padding = new Thickness(5);

                TableCell totalValueCell = new TableCell();
                Paragraph totalValuePara = new Paragraph(new Bold(new Run(subtotal.ToString("F2")))); // Use subtotal for total value
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
                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "Invoice");
            }
        }
        
        private void DateRangeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Show or hide custom date panel based on selection
            if (DateRangeSelector.SelectedIndex == 11) // Custom Range option (now index 11)
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

                LoadSalesData();
            }
        }
    }

    public class SalesInvoice : INotifyPropertyChanged
    {
        private string _invoiceNumber = string.Empty; // Initialize with a default value
        private DateTime _invoiceDate;
        private string _customerName = string.Empty; // Initialize with a default value
        private decimal _totalRevenue;
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

        public string CustomerName
        {
            get => _customerName;
            set
            {
                if (_customerName != value)
                {
                    _customerName = value;
                    OnPropertyChanged(nameof(CustomerName));
                }
            }
        }

        public decimal TotalRevenue
        {
            get => _totalRevenue;
            set
            {
                if (_totalRevenue != value)
                {
                    _totalRevenue = value;
                    OnPropertyChanged(nameof(TotalRevenue));
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
