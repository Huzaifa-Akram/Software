using System;
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
using System.Net.Http;

namespace Software
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Navigate to Sale Invoice page by default
            MainFrame.Navigate(new SaleInvoicePage());
        }

        private void btnSaleInvoice_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SaleInvoicePage());
        }

        private void btnPurchaseInvoice_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PurchaseInvoicePage());
        }

        private void btnSalesDetail_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new SaleDetailPage());
        }

        private void btnCustomersDetail_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new CustomersDetailPage());
        }

        private void btnReturnInvoice_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ReturnInvoicePage());
        }

        private void btnExpiryDetails_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ExpiryDetailsPage());
        }

        private void btnStockDetail_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new StockDetailPage());
        }

        private void btnPurchaseDetail_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new PurchaseDetailPage());
        }
    }
}
