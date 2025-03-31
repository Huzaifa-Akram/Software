using System;
// ...existing code...
using System.Net.Http;
// ...existing code...
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace Software.Data
{
    public class DatabaseHelper
    {
        private static readonly string dbPath = "customer_management.db"; // Database file
        private static readonly string connectionString = $"Data Source={dbPath};Version=3;";

        public DatabaseHelper()
        {
            CreateDatabaseAndTables();
            //    InsertDummyData();
        }

        private void CreateDatabaseAndTables()
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                // Table creation statements (as before)
                string createCustomersTable = @"
            CREATE TABLE IF NOT EXISTS Customers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                ContactNumber TEXT NOT NULL,
                Address TEXT,
                Type TEXT NOT NULL CHECK(Type IN ('Customer', 'Supplier')),
                CompanyName TEXT
            );";

                string createItemsTable = @"
            CREATE TABLE IF NOT EXISTS Items (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                CompanyName TEXT,
                LatestPurchaseRate REAL NOT NULL,
                TotalQuantity INTEGER NOT NULL
            );";

                string createItemBatchesTable = @"
            CREATE TABLE IF NOT EXISTS ItemBatches (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ItemId INTEGER,
                PurchaseRate REAL NOT NULL,
                Quantity INTEGER NOT NULL,
                ExpiryDate TEXT,
                PurchaseDate TEXT DEFAULT (datetime('now')),
                FOREIGN KEY (ItemId) REFERENCES Items(Id)
            );";

                string createTableQuery = @"
            CREATE TABLE IF NOT EXISTS SalesInvoiceBatchDeductions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                InvoiceNumber INTEGER,
                BatchId INTEGER,
                ItemId INTEGER,
                Quantity INTEGER NOT NULL,
                PurchaseRate REAL NOT NULL,
                FOREIGN KEY (InvoiceNumber) REFERENCES Invoices(InvoiceNumber),
                FOREIGN KEY (BatchId) REFERENCES ItemBatches(Id),
                FOREIGN KEY (ItemId) REFERENCES Items(Id)
            );";

                string createInvoicesTable = @"
            CREATE TABLE IF NOT EXISTS Invoices (
                InvoiceNumber TEXT PRIMARY KEY,
                CustomerId INTEGER,
                SupplierName TEXT,
                InvoiceDate TEXT DEFAULT (datetime('now')),
                InvoiceType TEXT NOT NULL CHECK(InvoiceType IN ('P', 'S')),
                FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
            );";

                string createInvoiceItemsTable = @"
            CREATE TABLE IF NOT EXISTS InvoiceItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                InvoiceNumber TEXT,
                ItemId INTEGER,
                Rate REAL NOT NULL,
                Quantity INTEGER NOT NULL,
                DiscountPercentage REAL DEFAULT 0,
                Total REAL NOT NULL,
                FOREIGN KEY (InvoiceNumber) REFERENCES Invoices(InvoiceNumber),
                FOREIGN KEY (ItemId) REFERENCES Items(Id)
            );";

                // View creation statements
                string createInvoiceCostOfGoodsSoldView = @"
            CREATE VIEW IF NOT EXISTS InvoiceCostOfGoodsSold AS
            SELECT
                sid.InvoiceNumber,
                SUM(sid.Quantity * ib.PurchaseRate) AS TotalCostOfGoodsSold
            FROM
                SalesInvoiceBatchDeductions sid
            JOIN
                ItemBatches ib ON sid.BatchId = ib.Id
            GROUP BY
                sid.InvoiceNumber;";

                string createInvoiceRevenueView = @"
            CREATE VIEW IF NOT EXISTS InvoiceRevenue AS
            SELECT
                ii.InvoiceNumber,
                SUM(ii.Total) AS TotalRevenue
            FROM
                InvoiceItems ii
            GROUP BY
                ii.InvoiceNumber;";

                string createInvoiceProfitView = @"
            CREATE VIEW IF NOT EXISTS InvoiceProfit AS
            SELECT
                i.InvoiceNumber,
                i.InvoiceDate,
                ir.TotalRevenue,
                icogs.TotalCostOfGoodsSold,
                (ir.TotalRevenue - icogs.TotalCostOfGoodsSold) AS Profit
            FROM
                Invoices i
            JOIN
                InvoiceRevenue ir ON i.InvoiceNumber = ir.InvoiceNumber
            JOIN
                InvoiceCostOfGoodsSold icogs ON i.InvoiceNumber = icogs.InvoiceNumber
            WHERE i.InvoiceType = 'S';";

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = createCustomersTable; command.ExecuteNonQuery();
                    command.CommandText = createItemsTable; command.ExecuteNonQuery();
                    command.CommandText = createInvoicesTable; command.ExecuteNonQuery();
                    command.CommandText = createInvoiceItemsTable; command.ExecuteNonQuery();
                    command.CommandText = createItemBatchesTable; command.ExecuteNonQuery();
                    command.CommandText = createTableQuery; command.ExecuteNonQuery();

                    // Execute the view creation statements
                    command.CommandText = createInvoiceCostOfGoodsSoldView; command.ExecuteNonQuery();
                    command.CommandText = createInvoiceRevenueView; command.ExecuteNonQuery();
                    command.CommandText = createInvoiceProfitView; command.ExecuteNonQuery();
                }
            }
        }

        public void InsertDummyData()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                string insertCustomers = @"
                INSERT INTO Customers (Name, ContactNumber, Address, Type, CompanyName) VALUES
                ('Huzaifa', '1234567890', '123 Elm Street', 'Customer', NULL),
                ('Ali Raza', '0987654321', '456 Oak Avenue', 'Customer', NULL),
                ('Haris', '1112223333', '789 Pine Road', 'Supplier', 'Company A'),
                ('Sanan', '4445556666', '321 Maple Lane', 'Supplier', 'Company B');";

                // Updated to match new Items table structure
                string insertItems = @"
                INSERT INTO Items (Name, CompanyName, LatestPurchaseRate, TotalQuantity) VALUES
                ('Chocolato', 'Bisconni', 10.5, 100),
                ('DairyMilk', 'Cadbury', 20.0, 200);";

                // New insert statement for ItemBatches
                string insertItemBatches = @"
                INSERT INTO ItemBatches (ItemId, PurchaseRate, Quantity, ExpiryDate, PurchaseDate) VALUES
                (1, 10.0, 50, '2024-12-31', datetime('now', '-30 days')),
                (1, 10.5, 50, '2024-12-31', datetime('now', '-15 days')),
                (2, 19.5, 100, '2025-06-30', datetime('now', '-45 days')),
                (2, 20.0, 100, '2025-06-30', datetime('now', '-5 days'));";

                string insertInvoices = @"
                INSERT INTO Invoices (InvoiceNumber, CustomerId, SupplierName, InvoiceDate, InvoiceType) VALUES
                ('S1', 1, NULL, datetime('now', '-10 days'), 'S'),
                ('S2', 2, NULL, datetime('now', '-5 days'), 'S'),
                ('P1', NULL, 'Supplier A', datetime('now', '-30 days'), 'P'),
                ('P2', NULL, 'Supplier B', datetime('now', '-15 days'), 'P');";

                string insertInvoiceItems = @"
                INSERT INTO InvoiceItems (InvoiceNumber, ItemId, Rate, Quantity, DiscountPercentage, Total) VALUES
                ('S1', 1, 12.0, 10, 5.0, 114.0),
                ('S2', 2, 22.0, 20, 10.0, 396.0),
                ('P1', 1, 10.5, 50, 0, 525.0),
                ('P2', 2, 20.0, 100, 0, 2000.0);";

                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = insertCustomers; command.ExecuteNonQuery();
                    command.CommandText = insertItems; command.ExecuteNonQuery();
                    command.CommandText = insertItemBatches; command.ExecuteNonQuery(); // Added ItemBatches insert
                    command.CommandText = insertInvoices; command.ExecuteNonQuery();
                    command.CommandText = insertInvoiceItems; command.ExecuteNonQuery();
                }
            }
        }

        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(connectionString);
        }

        // ...existing code...
        public void ReturnInvoice(string invoiceNumber)
        {
            using (var connection = GetConnection())
            {
                connection.Open();
                var transaction = connection.BeginTransaction();

                try
                {
                    var command = connection.CreateCommand();
                    command.Transaction = transaction;

                    // Get the items in the invoice
                    command.CommandText = @"
                        SELECT ItemId, Quantity, Name
                        FROM InvoiceItems
                        JOIN Items ON InvoiceItems.ItemId = Items.Id
                        WHERE InvoiceNumber = @InvoiceNumber";
                    command.Parameters.AddWithValue("@InvoiceNumber", invoiceNumber);

                    var reader = command.ExecuteReader();
                    var items = new List<InvoiceItem>();

                    while (reader.Read())
                    {
                        items.Add(new InvoiceItem
                        {
                            ItemId = Convert.ToInt32(reader["ItemId"]),
                            Quantity = Convert.ToInt32(reader["Quantity"]),
                            ItemName = reader["Name"]?.ToString() ?? string.Empty
                        });
                    }
                    reader.Close();

                    // Update the item quantities in the database
                    foreach (var item in items)
                    {
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

        public List<ItemBatch> GetExpiryDetails()
        {
            var batches = new List<ItemBatch>();

            using (var connection = GetConnection())
            {
                connection.Open();
                string query = @"
                    SELECT i.Name AS ItemName, i.CompanyName, ib.PurchaseRate, ib.Quantity, ib.ExpiryDate,
                           CASE 
                               WHEN ib.ExpiryDate < date('now') THEN 'Expired'
                               WHEN ib.ExpiryDate < date('now', '+30 days') THEN 'Expiring Soon'
                               ELSE ''
                           END AS Alert
                    FROM ItemBatches ib
                    JOIN Items i ON ib.ItemId = i.Id
                    WHERE ib.Quantity <> 0
                    ORDER BY ib.ExpiryDate ASC";


                using (var command = new SQLiteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            batches.Add(new ItemBatch
                            {
                                ItemName = reader["ItemName"]?.ToString() ?? string.Empty,
                                CompanyName = reader["CompanyName"].ToString(),
                                PurchaseRate = Convert.ToDecimal(reader["PurchaseRate"]),
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                ExpiryDate = Convert.ToDateTime(reader["ExpiryDate"]),
                                Alert = reader["Alert"].ToString()
                            });
                        }
                    }
                }
            }

            return batches;
        }

        public void InsertNewItem(string itemName, string companyName)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string insertItemQuery = @"
                    INSERT INTO Items (Name, CompanyName, LatestPurchaseRate, TotalQuantity) 
                    VALUES (@Name, @CompanyName, 0, 0);";

                using (var command = new SQLiteCommand(insertItemQuery, connection))
                {
                    command.Parameters.AddWithValue("@Name", itemName);
                    command.Parameters.AddWithValue("@CompanyName", companyName);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
