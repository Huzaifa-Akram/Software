using System;
// ...existing code...
using System.Net.Http;
// ...existing code...
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Software.Data;

namespace Software
{
    public partial class ReturnInvoicePage : Page
    {
        private DatabaseHelper _databaseHelper;

        public ReturnInvoicePage()
        {
            InitializeComponent();
            _databaseHelper = new DatabaseHelper();
            LoadInvoices();
        }

        private void LoadInvoices()
        {
            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT i.InvoiceNumber, i.InvoiceDate, c.Name AS CustomerName, SUM(ii.Total) AS TotalAmount
                    FROM Invoices i
                    JOIN InvoiceItems ii ON i.InvoiceNumber = ii.InvoiceNumber
                    LEFT JOIN Customers c ON i.CustomerId = c.Id
                    WHERE i.InvoiceType = 'S'
                    GROUP BY i.InvoiceNumber, i.InvoiceDate, c.Name";

                var reader = command.ExecuteReader();
                var invoices = new List<Invoice>();

                while (reader.Read())
                {
                    invoices.Add(new Invoice
                    {
                        InvoiceNumber = reader["InvoiceNumber"]?.ToString() ?? string.Empty,
                        InvoiceDate = reader["InvoiceDate"] != DBNull.Value ? DateTime.Parse(reader["InvoiceDate"]?.ToString() ?? string.Empty) : DateTime.MinValue,
                        CustomerName = reader["CustomerName"]?.ToString(),
                        TotalAmount = reader["TotalAmount"] != DBNull.Value ? Convert.ToDecimal(reader["TotalAmount"]) : 0
                    });

                }

                ReturnInvoicesDataGrid.ItemsSource = invoices;
            }
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedInvoice = (Invoice)ReturnInvoicesDataGrid.SelectedItem;
            if (selectedInvoice != null)
            {
                ReturnInvoice(selectedInvoice.InvoiceNumber);
                LoadInvoices();
                MessageBox.Show("Invoice returned successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Please select an invoice to return.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ReturnInvoice(string invoiceNumber)
        {
            using (var connection = _databaseHelper.GetConnection())
            {
                connection.Open();
                var transaction = connection.BeginTransaction();

                try
                {
                    var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    // Get the items in the invoice
                    command.CommandText = @"
                        SELECT ItemId, Quantity
                        FROM InvoiceItems
                        WHERE InvoiceNumber = @InvoiceNumber";
                    command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);

                    var reader = command.ExecuteReader();
                    var items = new List<InvoiceItem>();

                    while (reader.Read())
                    {
                        // Add the required 'ItemName' property to the InvoiceItem object initializer
                        items.Add(new InvoiceItem
                        {
                            ItemId = Convert.ToInt32(reader["ItemId"]),
                            ItemName = string.Empty, // Set a default value for ItemName
                            Quantity = Convert.ToInt32(reader["Quantity"])
                        });
                        
                    }
                    reader.Close();

                    // Get the batch deductions for the invoice
                    command.CommandText = @"
                        SELECT BatchId, ItemId, Quantity
                        FROM SalesInvoiceBatchDeductions
                        WHERE InvoiceNumber = @InvoiceNumber";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);

                    var batchReader = command.ExecuteReader();
                    var batchDeductions = new List<BatchDeduction>();

                    while (batchReader.Read())
                    {
                        batchDeductions.Add(new BatchDeduction
                        {
                            BatchId = Convert.ToInt32(batchReader["BatchId"]),
                            ItemId = Convert.ToInt32(batchReader["ItemId"]),
                            Quantity = Convert.ToInt32(batchReader["Quantity"])
                        });
                    }
                    batchReader.Close();

                    // Update the item quantities in the database
                    foreach (var item in items)
                    {
                        // Get the batch deductions for the item
                        var itemBatchDeductions = batchDeductions.Where(b => b.ItemId == item.ItemId).ToList();

                        foreach (var batchDeduction in itemBatchDeductions)
                        {
                            command.CommandText = @"
                                UPDATE ItemBatches
                                SET Quantity = Quantity + @Quantity
                                WHERE Id = @BatchId";
                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("@Quantity", batchDeduction.Quantity);
                            command.Parameters.AddWithValue("@BatchId", batchDeduction.BatchId);
                            command.ExecuteNonQuery();
                        }

                        command.CommandText = @"
                            UPDATE Items
                            SET TotalQuantity = TotalQuantity + @Quantity
                            WHERE Id = @ItemId";
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@Quantity", item.Quantity);
                        command.Parameters.AddWithValue("@ItemId", item.ItemId);
                        command.ExecuteNonQuery();
                    }

                    // Delete the invoice items
                    command.CommandText = @"
                        DELETE FROM InvoiceItems
                        WHERE InvoiceNumber = @InvoiceNumber";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);
                    command.ExecuteNonQuery();

                    // Delete the batch deductions
                    command.CommandText = @"
                        DELETE FROM SalesInvoiceBatchDeductions
                        WHERE InvoiceNumber = @InvoiceNumber";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);
                    command.ExecuteNonQuery();

                    // Delete the invoice
                    command.CommandText = @"
                        DELETE FROM Invoices
                        WHERE InvoiceNumber = @InvoiceNumber";
                    command.Parameters.Clear();
                    command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);
                    command.ExecuteNonQuery();

                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private class BatchDeduction
        {
            public int BatchId { get; set; }
            public int ItemId { get; set; }
            public int Quantity { get; set; }
        }
    }

    public class Invoice
    {
        public required string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string? CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
    }

   
}
