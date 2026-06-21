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

        public async Task AddUserAsync(string username, string rawPassword, string firstName, string lastName, string storeCode)
        {
            string fullName = $"{firstName.Trim()} {lastName.Trim()}";
            string encodedPassword = EncodeBase64(rawPassword); 

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO PUREGOLD.dbo.tblUsers (username, password, fullname) VALUES (@user, @pass, @fname)";
                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", encodedPassword);
                    cmd.Parameters.AddWithValue("@fname", fullName);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO AGING_DB.dbo.tblUsers 
                        (uName, uPass, fName, lName, uLvl, strAcc, uStat, dept, uStr) 
                        VALUES (@user, @pass, @fname, @lname, 'icg', @storeCode, 'A', 'ICD', @storeCode)";

                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", encodedPassword);
                    cmd.Parameters.AddWithValue("@fname", firstName.Trim());
                    cmd.Parameters.AddWithValue("@lname", lastName.Trim());
                    cmd.Parameters.AddWithValue("@storeCode", storeCode);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task UpdateUserAsync(string username, string rawPassword, string firstName, string lastName, string storeCode)
        {
            string fullName = $"{firstName.Trim()} {lastName.Trim()}";
            string encodedPassword = EncodeBase64(rawPassword); // Encode once

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE PUREGOLD.dbo.tblUsers SET password = @pass, fullname = @fname WHERE username = @user";
                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", encodedPassword);
                    cmd.Parameters.AddWithValue("@fname", fullName);
                    await cmd.ExecuteNonQueryAsync();
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        IF EXISTS (SELECT 1 FROM AGING_DB.dbo.tblUsers WHERE uName = @user)
                        BEGIN
                            UPDATE AGING_DB.dbo.tblUsers 
                            SET uPass = @pass, fName = @fname, lName = @lname 
                            WHERE uName = @user
                        END
                        ELSE
                        BEGIN
                            INSERT INTO AGING_DB.dbo.tblUsers 
                            (uName, uPass, fName, lName, uLvl, strAcc, uStat, dept, uStr) 
                            VALUES (@user, @pass, @fname, @lname, 'icg', @storeCode, 'A', 'ICD', @storeCode)
                        END";

                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", encodedPassword);
                    cmd.Parameters.AddWithValue("@fname", firstName.Trim());
                    cmd.Parameters.AddWithValue("@lname", lastName.Trim());
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