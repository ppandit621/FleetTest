using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
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
using System.Configuration;
using System.Data.SQLite;
using System.Timers;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace SyncData2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Timers.Timer syncTimer;
        string sqlconnectionString = ConfigurationManager.ConnectionStrings["SQLDefaultConnection"].ConnectionString;
        string sqliteconnectionString = ConfigurationManager.ConnectionStrings["SQLiteDefaultConnection"].ConnectionString;
        public MainWindow()
        {
            InitializeComponent();
        }
        public void ManualFetchButton_Click(object sender, RoutedEventArgs e)
        {
            //string connectionString = @"Server=.;Database=TEST;Integrated Security=True;";
            string sqlconnectionString = ConfigurationManager.ConnectionStrings["SQLDefaultConnection"].ConnectionString;
            //string sqliteconnectionString = ConfigurationManager.ConnectionStrings["SQLiteDefaultConnection"].ConnectionString;
            using (SqlConnection connection = new SqlConnection(sqlconnectionString))
            {
                try
                {
                    connection.Open();
                    MessageBox.Show("Connection successful!");
                    SqlCommand command = new SqlCommand(@"SELECT CUSTOMER.Name, CUSTOMER.Email, CUSTOMER.Phone, LOCATION.Address 
                                                         FROM dbo.CUSTOMER 
                                                         INNER JOIN dbo.LOCATION ON CUSTOMER.CustomerID = LOCATION.CustomerID",
                                                         connection);

                    SqlDataAdapter dataAdapter = new SqlDataAdapter(command);
                    DataTable dataTable = new DataTable();
                    dataAdapter.Fill(dataTable);

                    // Bind the DataTable to the DataGrid
                    CustomerGrid.ItemsSource = dataTable.DefaultView;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occurred: " + ex.Message);
                }
            }
        }
        public void StartSyncButton_Click(object sender, RoutedEventArgs e)
        {
            
            if (int.TryParse(IntervalTextBox.Text, out int interval) && interval > 0)
            {
                
                syncTimer = new System.Timers.Timer(interval * 1000);
                syncTimer.Elapsed += SyncData;
                syncTimer.AutoReset = true;
                syncTimer.Enabled = true;
                MessageBox.Show($"Sync started every {interval} seconds.");
            }
            else
            {
                MessageBox.Show("Please enter a valid positive integer for the interval.");
            }

            string sqliteconnectionString = ConfigurationManager.ConnectionStrings["SQLiteDefaultConnection"].ConnectionString;
            MessageBox.Show(sqliteconnectionString);

            using (var connection = new SQLiteConnection(sqliteconnectionString))
            {
                try
                {
                    connection.Open();
                    string query = @"SELECT CUSTOMER.CustomerID, CUSTOMER.Name, CUSTOMER.Email, CUSTOMER.Phone, LOCATION.Address
                             FROM CUSTOMER 
                             INNER JOIN LOCATION ON CUSTOMER.CustomerID = LOCATION.CustomerID
                             ORDER BY CUSTOMER.CustomerID"; 

                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        
                        var customerData = new Dictionary<string, CustomerInfo>();

                        while (reader.Read())
                        {
                            var customerId = reader["CustomerID"].ToString();
                            var name = reader["Name"].ToString();
                            var email = reader["Email"].ToString();
                            var phone = reader["Phone"].ToString();
                            var address = reader["Address"].ToString();

                            if (!customerData.ContainsKey(customerId))
                            {
                                
                                customerData[customerId] = new CustomerInfo
                                {
                                    CustomerID = customerId,
                                    Name = name,
                                    Email = email,
                                    Phone = phone,
                                    Locations = new List<string>() { address }
                                };
                            }
                            else
                            {
                                
                                customerData[customerId].Locations.Add(address);
                            }
                        }

                        
                        DataTable dataTable = new DataTable();
                        dataTable.Columns.Add("CustomerID");
                        dataTable.Columns.Add("Name");
                        dataTable.Columns.Add("Email");
                        dataTable.Columns.Add("Phone");
                        dataTable.Columns.Add("Locations");

                        foreach (var customer in customerData.Values)
                        {
                            var row = dataTable.NewRow();
                            row["CustomerID"] = customer.CustomerID;
                            row["Name"] = customer.Name;
                            row["Email"] = customer.Email;
                            row["Phone"] = customer.Phone;
                            row["Locations"] = string.Join(", ", customer.Locations);
                            dataTable.Rows.Add(row);
                        }

                        
                        CustomerGrid.ItemsSource = dataTable.DefaultView;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occurred: " + ex.Message);
                }
            }
        }

        
        public class CustomerInfo
        {
            public string CustomerID { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public List<string> Locations { get; set; }
        }

        public void SyncData(object sender, ElapsedEventArgs e)
        {
            try
            {
                using (SqlConnection mssqlConnection = new SqlConnection(sqlconnectionString))
                {
                    mssqlConnection.Open();
                    string query = @"SELECT CUSTOMER.CustomerID, CUSTOMER.Name, CUSTOMER.Email, CUSTOMER.Phone, LOCATION.Address 
                             FROM dbo.CUSTOMER
                             INNER JOIN dbo.LOCATION ON CUSTOMER.CustomerID = LOCATION.CustomerID
                             ORDER BY CUSTOMER.CustomerID"; 

                    using (SqlCommand command = new SqlCommand(query, mssqlConnection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        using (SQLiteConnection sqliteConnection = new SQLiteConnection(sqliteconnectionString))
                        {
                            sqliteConnection.Open();
                            using (SQLiteTransaction transaction = sqliteConnection.BeginTransaction())
                            {
                                string currentCustomerId = null;
                                string name = null, email = null, phone = null;
                                List<string> locations = new List<string>();

                                while (reader.Read())
                                {
                                    var customerId = reader["CustomerID"].ToString();
                                    var location = reader["Address"].ToString();

                                    
                                    if (currentCustomerId != null && currentCustomerId != customerId)
                                    {
                                        
                                        InsertOrUpdateCustomerAndLocations(currentCustomerId, name, email, phone, locations, sqliteConnection);

                                        
                                        locations.Clear();
                                    }

                                    
                                    currentCustomerId = customerId;
                                    name = reader["Name"].ToString();
                                    email = reader["Email"].ToString();
                                    phone = reader["Phone"].ToString();
                                    locations.Add(location);
                                }

                                

                                if (currentCustomerId != null)
                                {
                                    InsertOrUpdateCustomerAndLocations(currentCustomerId, name, email, phone, locations, sqliteConnection);
                                }
                                MessageBox.Show("I am here to commit:");
                                transaction.Commit();
                            }
                        }
                    }
                }

                Console.WriteLine($"Data synced at {DateTime.Now}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }
        public void InsertOrUpdateCustomerAndLocations(string customerId, string name, string email, string phone, List<string> locations, SQLiteConnection sqliteConnection)
        {
            // Check if the customer exists
            using (SQLiteCommand checkCmd = new SQLiteCommand(@"SELECT * FROM CUSTOMER WHERE CustomerID = @CustomerID", sqliteConnection))
            {
                checkCmd.Parameters.AddWithValue("@CustomerID", customerId);
                using (SQLiteDataReader sqliteReader = checkCmd.ExecuteReader())
                {
                    bool recordExists = sqliteReader.Read();

                    if (recordExists)
                    {
                        
                        TrackChanges(sqliteReader, "Name", name, customerId, sqliteConnection);
                        //TrackChanges(sqliteReader, "Email", email, customerId, sqliteConnection);
                        //TrackChanges(sqliteReader, "Phone", phone, customerId, sqliteConnection);

                        using (SQLiteCommand updateCmd = new SQLiteCommand(
                            @"UPDATE CUSTOMER SET Name = @Name, Email = @Email, Phone = @Phone WHERE CustomerID = @CustomerID", sqliteConnection))
                        {
                            updateCmd.Parameters.AddWithValue("@Name", name);
                            updateCmd.Parameters.AddWithValue("@Email", email);
                            updateCmd.Parameters.AddWithValue("@Phone", phone);
                            updateCmd.Parameters.AddWithValue("@CustomerID", customerId);
                            updateCmd.ExecuteNonQuery();
                        }

                        
                        //foreach (var location in locations)
                        //{
                        //    TrackChanges(sqliteReader, "Address", location, customerId, sqliteConnection);

                        //    using (SQLiteCommand updateLocCmd = new SQLiteCommand(
                        //        @"UPDATE LOCATION SET Address = @Address WHERE CustomerID = @CustomerID AND LocationID = @LocationID", sqliteConnection))
                        //    {
                        //        updateLocCmd.Parameters.AddWithValue("@Address", location);
                        //        updateLocCmd.Parameters.AddWithValue("@CustomerID", customerId);
                        //        updateLocCmd.ExecuteNonQuery();
                        //    }
                        //}
                    }
                    else
                    {
                        
                        using (SQLiteCommand insertCmd = new SQLiteCommand(
                            @"INSERT INTO CUSTOMER (CustomerID, Name, Email, Phone) VALUES (@CustomerID, @Name, @Email, @Phone)", sqliteConnection))
                        {
                            insertCmd.Parameters.AddWithValue("@CustomerID", customerId);
                            insertCmd.Parameters.AddWithValue("@Name", name);
                            insertCmd.Parameters.AddWithValue("@Email", email);
                            insertCmd.Parameters.AddWithValue("@Phone", phone);
                            insertCmd.ExecuteNonQuery();
                        }

                        
                        foreach (var location in locations)
                        {
                            using (SQLiteCommand insertLocCmd = new SQLiteCommand(
                                @"INSERT INTO LOCATION (CustomerID, Address) VALUES (@CustomerID, @Address)", sqliteConnection))
                            {
                                insertLocCmd.Parameters.AddWithValue("@CustomerID", customerId);
                                insertLocCmd.Parameters.AddWithValue("@Address", location);
                                insertLocCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
        }

        public void TrackChanges(SQLiteDataReader sqliteReader, string columnName, string newValue, string customerId, SQLiteConnection sqliteConnection)
        {
            try
            {
                int columnIndex = sqliteReader.GetOrdinal(columnName);
                string oldValue = sqliteReader.IsDBNull(columnIndex) ? null : sqliteReader[columnIndex].ToString();

                if (oldValue != newValue)
                {
                    using (SQLiteCommand logCmd = new SQLiteCommand(
                        @"INSERT INTO SyncLog (CustomerID, ColumnChanged, PreviousValue, NewValue, Timestamp) 
                  VALUES (@CustomerID, @ColumnChanged, @PreviousValue, @NewValue, @Timestamp)", sqliteConnection))
                    {
                        logCmd.Parameters.AddWithValue("@CustomerID", customerId);
                        logCmd.Parameters.AddWithValue("@ColumnChanged", columnName);
                        logCmd.Parameters.AddWithValue("@PreviousValue", oldValue ?? "NULL");
                        logCmd.Parameters.AddWithValue("@NewValue", newValue ?? "NULL");
                        logCmd.Parameters.AddWithValue("@Timestamp", DateTime.Now);
                        logCmd.ExecuteNonQuery();
                    }
                }
            }
            catch (IndexOutOfRangeException ex)
            {
                MessageBox.Show($"Column '{columnName}' not found in the reader: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during tracking changes: {ex.Message}");
            }
        }

    }
}
