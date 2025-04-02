using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;

namespace Software
{
    public partial class LoginWindow : Window
    {
        // Add "Test" suffix to create new paths for testing
        private static readonly string LicenseFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Software", "licenseTest.key");
        private const string LicenseSentRegistryKey = @"HKEY_CURRENT_USER\Software\SoftwareNameTest";
        private const string? LicenseSentRegistryValue = "LicenseSent";

        // Flag to enable/disable test mode (set to true for testing)
        private const bool TestMode = false;

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => txtLicenseKey.Focus();
            Debug.WriteLine("LoginWindow initialized.");

            // Reset existing licenses if in test mode
            if (TestMode)
            {
                ResetLicenseData();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(LicenseFilePath) ?? throw new InvalidOperationException("License file path is invalid."));
            Debug.WriteLine("License directory ensured.");

            if (!File.Exists(LicenseFilePath))
            {
                Debug.WriteLine("License key file not found. Generating new license key.");
                string? licenseKey = GenerateLicenseKey();
                File.WriteAllText(LicenseFilePath, licenseKey);
                Debug.WriteLine($"License key generated and saved: {licenseKey}");

                object? licenseSent = Registry.GetValue(LicenseSentRegistryKey, LicenseSentRegistryValue, null);
                if (licenseSent == null)
                {
                    Debug.WriteLine("License key has not been sent. Sending email.");
                    SendLicenseKeyToEmail(licenseKey);
                    Registry.SetValue(LicenseSentRegistryKey, LicenseSentRegistryValue, true);
                    Registry.SetValue(LicenseSentRegistryKey, "StoredLicenseKey", licenseKey);
                    Debug.WriteLine("License key sent and registry updated.");
                }
                else
                {
                    Debug.WriteLine("License key has already been sent.");
                }
            }
            else
            {
                Debug.WriteLine("License key file found.");
            }
        }

        // New method to completely reset license data
        public void ResetLicenseData()
        {
            // Delete license file
            DeleteLicenseKeyFile();

            // Delete registry keys
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\SoftwareNameTest", false);
                Debug.WriteLine("Registry keys deleted.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete registry keys: {ex.Message}");
            }
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string? enteredLicenseKey = txtLicenseKey.Text;
            Debug.WriteLine($"Entered license key: {enteredLicenseKey}");

            if (!File.Exists(LicenseFilePath))
            {
                MessageBox.Show("License key file not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string? storedLicenseKey = File.ReadAllText(LicenseFilePath);
            Debug.WriteLine($"Stored license key: {storedLicenseKey}");

            if (enteredLicenseKey == storedLicenseKey)
            {
                // Store the key in registry for auto-login next time
                Registry.SetValue(LicenseSentRegistryKey, "StoredLicenseKey", storedLicenseKey);

                MainWindow? mainWindow = new MainWindow();
                mainWindow.Show();
                Close();
            }
            else
            {
                MessageBox.Show("Invalid license key.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void DeleteLicenseKeyFile()
        {
            if (File.Exists(LicenseFilePath))
            {
                File.Delete(LicenseFilePath);
                Debug.WriteLine("License key file deleted.");
            }
            else
            {
                Debug.WriteLine("License key file does not exist.");
            }
        }

        private void btnDeleteLicenseKey_Click(object sender, RoutedEventArgs e)
        {
            ResetLicenseData();
            MessageBox.Show("License data reset. Restart the application to generate a new key.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // The rest of your code remains unchanged
        private string GenerateLicenseKey()
        {
            using (var sha256 = SHA256.Create())
            {
                string? deviceInfo = Environment.MachineName +
                                    Environment.UserName +
                                    Environment.ProcessorCount;

                byte[]? hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(deviceInfo));
                string? licenseKey = BitConverter.ToString(hash).Replace("-", "");

                licenseKey = licenseKey.Substring(0, 20);

                string? formattedKey = FormatLicenseKey(licenseKey);

                Debug.WriteLine($"Generated license key: {formattedKey}");
                return formattedKey;
            }
        }

        private string FormatLicenseKey(string key)
        {
            return string.Join("-", Enumerable.Range(0, key.Length / 5)
                .Select(i => key.Substring(i * 5, Math.Min(5, key.Length - i * 5))));
        }

        private void SendLicenseKeyToEmail(string licenseKey)
        {
            try
            {
                var fromAddress = new MailAddress("clientsoftware3@gmail.com", "Software");
                var primaryRecipient = new MailAddress("clientsoftware3@gmail.com", "Software");
                var secondaryRecipient = new MailAddress("huzaifaakram121@gmail.com", "Huzaifa Akram");
                const string? fromPassword = "tasn utdh jczo roah";

                // Collect device information in a more structured way
                string machineName = Environment.MachineName;
                string userName = Environment.UserName;
                string processorCount = Environment.ProcessorCount.ToString();
                string osVersion = Environment.OSVersion.ToString();
                string dotNetVersion = Environment.Version.ToString();

                const string? subject = "Client License Key Request";

                // Create an HTML formatted email body
                string? htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333333;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            border: 1px solid #dddddd;
            border-radius: 5px;
        }}
        h1 {{
            color: #0066cc;
            font-size: 24px;
            margin-bottom: 20px;
            border-bottom: 1px solid #eeeeee;
            padding-bottom: 10px;
        }}
        h2 {{
            color: #444444;
            font-size: 18px;
            margin-top: 20px;
            margin-bottom: 10px;
        }}
        .key-section {{
            background-color: #f9f9f9;
            padding: 15px;
            border-radius: 5px;
            margin: 15px 0;
            border-left: 4px solid #0066cc;
        }}
        .info-table {{
            width: 100%;
            border-collapse: collapse;
        }}
        .info-table td {{
            padding: 8px;
            border-bottom: 1px solid #eeeeee;
        }}
        .info-table td:first-child {{
            font-weight: bold;
            width: 40%;
        }}
        .footer {{
            margin-top: 30px;
            font-size: 12px;
            color: #777777;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>License Key Information</h1>
        
        <div class='key-section'>
            <h2>License Key</h2>
            <p style='font-size: 16px; font-weight: bold;'>{licenseKey}</p>
        </div>
        
        <h2>Client Information</h2>
        <table class='info-table'>
            <tr>
                <td>Machine Name:</td>
                <td>{machineName}</td>
            </tr>
            <tr>
                <td>User Name:</td>
                <td>{userName}</td>
            </tr>
            <tr>
                <td>Processor Count:</td>
                <td>{processorCount}</td>
            </tr>
            <tr>
                <td>OS Version:</td>
                <td>{osVersion}</td>
            </tr>
            <tr>
                <td>Framework Version:</td>
                <td>{dotNetVersion}</td>
            </tr>
            <tr>
                <td>Request Date:</td>
                <td>{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}</td>
            </tr>
        </table>
        
        <div class='footer'>
            <p>This is an automated email from the Software licensing system.</p>
            <p>Huzaifa Adil Akram <a href='https://github.com/Huzaifa-Akram'>GITHUB</a></p>
        </div>
    </div>
</body>
</html>";

                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                };

                // Create a message that will be sent to both recipients
                using (var message = new MailMessage()
                {
                    From = fromAddress,
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true // Enable HTML formatting
                })
                {
                    // Add both recipients
                    message.To.Add(primaryRecipient);
                    message.To.Add(secondaryRecipient);

                    // Send the email
                    smtp.Send(message);
                    Debug.WriteLine("License key email sent successfully to both recipients.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send license key: {ex.Message}");
                MessageBox.Show($"Failed to send license key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
