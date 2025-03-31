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
        private static readonly string LicenseFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Software", "license.key");
        private const string LicenseSentRegistryKey = @"HKEY_CURRENT_USER\Software\SoftwareName";
        private const string? LicenseSentRegistryValue = "LicenseSent";

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => txtLicenseKey.Focus();
            Debug.WriteLine("LoginWindow initialized.");

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

            string? storedLicenseKey = Registry.GetValue(LicenseSentRegistryKey, "StoredLicenseKey", null) as string;
            if (storedLicenseKey != null && storedLicenseKey == File.ReadAllText(LicenseFilePath))
            {
                Debug.WriteLine("Valid license key found in registry. Opening main window.");
                MainWindow? mainWindow = new MainWindow();
                mainWindow.Show();
                Close();
            }
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string? enteredLicenseKey = txtLicenseKey.Text;
            Debug.WriteLine($"Entered license key: {enteredLicenseKey}");

            string? storedLicenseKey = File.ReadAllText(LicenseFilePath);
            Debug.WriteLine($"Stored license key: {storedLicenseKey}");

            if (enteredLicenseKey == storedLicenseKey)
            {
                // ...existing code...
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
            DeleteLicenseKeyFile();
            MessageBox.Show("License key file deleted. Restart the application to generate a new key.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

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
                var toAddress = new MailAddress("huzaifaakram121@gmail.com", "Huzaifa Akram");
                const string? fromPassword = "bxib vall vgnm xzxq";
                string? deviceInfo = Environment.MachineName +
                    Environment.UserName +
                    Environment.ProcessorCount;
                const string? subject = "Client is Demanding License Key";
                string? body = $"License key is: {licenseKey}\n\nDevice Info:\n{deviceInfo}";

                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                };
                using (var message = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = body
                })
                {
                    smtp.Send(message);
                    Debug.WriteLine("License key email sent successfully.");
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
