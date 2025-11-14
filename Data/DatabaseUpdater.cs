using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace HRManagementSystem.Data
{
    public static class DatabaseUpdater
    {
        public static void AddIsDeletedColumn()
        {
            try
            {
                var connectionString = "Data Source=hrmanagement.db";
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // Check if IsDeleted column already exists
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "PRAGMA table_info(Employees)";
                
                bool columnExists = false;
                using (var reader = checkCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var columnName = reader.GetString(1); // Column name is at index 1
                        if (columnName == "IsDeleted")
                        {
                            columnExists = true;
                            break;
                        }
                    }
                }

                if (!columnExists)
                {
                    // Add the IsDeleted column with default value false
                    var addColumnCmd = connection.CreateCommand();
                    addColumnCmd.CommandText = "ALTER TABLE Employees ADD COLUMN IsDeleted INTEGER NOT NULL DEFAULT 0";
                    addColumnCmd.ExecuteNonQuery();
                    
                    Console.WriteLine("? Successfully added IsDeleted column to Employees table");
                }
                else
                {
                    Console.WriteLine("? IsDeleted column already exists");
                }

                // Check if StatusChangeRequests table exists
                var checkTableCmd = connection.CreateCommand();
                checkTableCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='StatusChangeRequests'";
                var hasStatusChangeTable = checkTableCmd.ExecuteScalar() != null;

                if (!hasStatusChangeTable)
                {
                    var createTableCmd = connection.CreateCommand();
                    createTableCmd.CommandText = @"
                        CREATE TABLE StatusChangeRequests (
                            StatusChangeRequestId INTEGER PRIMARY KEY AUTOINCREMENT,
                            EmployeeId INTEGER NOT NULL,
                            FromStatus INTEGER NOT NULL,
                            ToStatus INTEGER NOT NULL,
                            Reason TEXT NOT NULL,
                            RequestedBy TEXT NOT NULL,
                            RequestedDate TEXT NOT NULL,
                            ApprovedBy TEXT,
                            ApprovedDate TEXT,
                            Status INTEGER NOT NULL DEFAULT 0,
                            ApprovalComments TEXT,
                            FOREIGN KEY (EmployeeId) REFERENCES Employees (EmployeeId) ON DELETE CASCADE
                        )";
                    createTableCmd.ExecuteNonQuery();

                    // Create indexes for better performance
                    var createIndexCmd1 = connection.CreateCommand();
                    createIndexCmd1.CommandText = "CREATE INDEX IX_StatusChangeRequests_Status ON StatusChangeRequests (Status)";
                    createIndexCmd1.ExecuteNonQuery();

                    var createIndexCmd2 = connection.CreateCommand();
                    createIndexCmd2.CommandText = "CREATE INDEX IX_StatusChangeRequests_RequestedDate ON StatusChangeRequests (RequestedDate)";
                    createIndexCmd2.ExecuteNonQuery();

                    Console.WriteLine("? StatusChangeRequests table created successfully");
                }
                else
                {
                    Console.WriteLine("? StatusChangeRequests table already exists");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Database update error: {ex.Message}");
            }
        }
    }
}