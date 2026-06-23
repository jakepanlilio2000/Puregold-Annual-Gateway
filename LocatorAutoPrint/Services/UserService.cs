using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using LocatorAutoPrint.Models;

namespace LocatorAutoPrint.Services
{
    public class UserService
    {
        private readonly string _connectionString;

        public UserService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public string EncodeBase64(string plainText) => Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));

        public async Task<List<UserModel>> GetUsersAsync()
        {
            var list = new List<UserModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT username, fullname, active_locator, last_login, last_logout, ipaddress FROM PUREGOLD.dbo.tblUsers";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new UserModel
                            {
                                Username = reader["username"].ToString(),
                                Fullname = reader["fullname"].ToString(),
                                ActiveLocator = reader["active_locator"] as int?,
                                LastLogin = reader["last_login"] as DateTime?,
                                LastLogout = reader["last_logout"] as DateTime?,
                                IpAddress = reader["ipaddress"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task LogoutMobileAppAsync(string username)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE PUREGOLD.dbo.tblUsers SET ipaddress = NULL, active_locator = NULL WHERE username = @user";
                    cmd.Parameters.AddWithValue("@user", username);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // Replace your existing AddUserAsync and UpdateUserAsync methods with these:

        public async Task AddUserAsync(string username, string rawPassword, string fullName, string storeCode)
        {
            // Automatically split the full name for AGING_DB
            var nameParts = fullName.Trim().Split(new[] { ' ' }, 2);
            string fName = nameParts[0];
            string lName = nameParts.Length > 1 ? nameParts[1] : "";

            string encodedPassword = EncodeBase64(rawPassword);

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 1. Insert into PUREGOLD
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO PUREGOLD.dbo.tblUsers (username, password, fullname) VALUES (@user, @pass, @fname)";
                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", encodedPassword);
                    cmd.Parameters.AddWithValue("@fname", fullName.Trim());
                    await cmd.ExecuteNonQueryAsync();
                }

                // 2. Insert into AGING_DB
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                INSERT INTO AGING_DB.dbo.tblUsers 
                (uName, uPass, fName, lName, uLvl, strAcc, uStat, dept, uStr) 
                VALUES (@user, @pass, @fName, @lName, 'icg', @storeCode, 'A', 'ICD', @storeCode)";

                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", encodedPassword);
                    cmd.Parameters.AddWithValue("@fName", fName);
                    cmd.Parameters.AddWithValue("@lName", lName);
                    cmd.Parameters.AddWithValue("@storeCode", storeCode);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task UpdateUserAsync(string username, string rawPassword, string fullName, string storeCode)
        {
            var nameParts = fullName.Trim().Split(new[] { ' ' }, 2);
            string fName = nameParts[0];
            string lName = nameParts.Length > 1 ? nameParts[1] : "";

            string encodedPassword = EncodeBase64(rawPassword);

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // 1. Update PUREGOLD
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE PUREGOLD.dbo.tblUsers SET password = @pass, fullname = @fname WHERE username = @user";
                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", encodedPassword);
                    cmd.Parameters.AddWithValue("@fname", fullName.Trim());
                    await cmd.ExecuteNonQueryAsync();
                }

                // 2. Update AGING_DB
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                IF EXISTS (SELECT 1 FROM AGING_DB.dbo.tblUsers WHERE uName = @user)
                BEGIN
                    UPDATE AGING_DB.dbo.tblUsers 
                    SET uPass = @pass, fName = @fName, lName = @lName 
                    WHERE uName = @user
                END
                ELSE
                BEGIN
                    INSERT INTO AGING_DB.dbo.tblUsers 
                    (uName, uPass, fName, lName, uLvl, strAcc, uStat, dept, uStr) 
                    VALUES (@user, @pass, @fName, @lName, 'icg', @storeCode, 'A', 'ICD', @storeCode)
                END";

                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", encodedPassword);
                    cmd.Parameters.AddWithValue("@fName", fName);
                    cmd.Parameters.AddWithValue("@lName", lName);
                    cmd.Parameters.AddWithValue("@storeCode", storeCode);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }


        public async Task DeleteUserAsync(string username)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM PUREGOLD.dbo.tblUsers WHERE username = @user";
                    cmd.Parameters.AddWithValue("@user", username);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM AGING_DB.dbo.tblUsers WHERE uName = @user";
                    cmd.Parameters.AddWithValue("@user", username);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}