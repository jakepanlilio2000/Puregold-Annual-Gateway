using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using LocatorAutoPrint.Helpers;
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

        public string EncodeBase64(string plainText) => Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText ?? string.Empty));

        public async Task<List<UserModel>> GetUsersAsync()
        {
            var list = new List<UserModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT username, fullname, active_locator, last_login, last_logout, ipaddress FROM PUREGOLD.dbo.tblUsers ORDER BY username";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int? activeLoc = null;
                            if (!reader.IsDBNull(reader.GetOrdinal("active_locator")))
                            {
                                activeLoc = reader.GetInt32Safe("active_locator");
                            }

                            list.Add(new UserModel
                            {
                                Username = reader.GetStringSafe("username"),
                                Fullname = reader.GetStringSafe("fullname"),
                                ActiveLocator = activeLoc,
                                LastLogin = reader.GetDateTimeSafe("last_login"),
                                LastLogout = reader.GetDateTimeSafe("last_logout"),
                                IpAddress = reader.GetStringSafe("ipaddress")
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task LogoutMobileAppAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE PUREGOLD.dbo.tblUsers SET ipaddress = NULL, active_locator = NULL WHERE username = @user";
                    cmd.Parameters.AddWithValue("@user", username.Trim());
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task AddUserAsync(string username, string rawPassword, string fullName, string storeCode)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username cannot be empty.");

            string cleanUser = username.Trim();
            string cleanFull = (fullName ?? cleanUser).Trim();
            var nameParts = cleanFull.Split(new[] { ' ' }, 2);
            string fName = nameParts[0];
            string lName = nameParts.Length > 1 ? nameParts[1] : "";

            string encodedPassword = EncodeBase64(rawPassword ?? cleanUser);
            string cleanStoreCode = (storeCode ?? string.Empty).Trim();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Insert into PUREGOLD
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "INSERT INTO PUREGOLD.dbo.tblUsers (username, password, fullname) VALUES (@user, @pass, @fname)";
                            cmd.Parameters.AddWithValue("@user", cleanUser);
                            cmd.Parameters.AddWithValue("@pass", encodedPassword);
                            cmd.Parameters.AddWithValue("@fname", cleanFull);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 2. Insert into AGING_DB
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                        INSERT INTO AGING_DB.dbo.tblUsers 
                        (uName, uPass, fName, lName, uLvl, strAcc, uStat, dept, uStr) 
                        VALUES (@user, @pass, @fName, @lName, 'icg', @storeCode, 'A', 'ICD', @storeCode)";

                            cmd.Parameters.AddWithValue("@user", cleanUser);
                            cmd.Parameters.AddWithValue("@pass", encodedPassword);
                            cmd.Parameters.AddWithValue("@fName", fName);
                            cmd.Parameters.AddWithValue("@lName", lName);
                            cmd.Parameters.AddWithValue("@storeCode", cleanStoreCode);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        try { transaction.Rollback(); } catch { }
                        ErrorLoggerService.LogException($"UserService.AddUserAsync({cleanUser})", ex);
                        throw;
                    }
                }
            }
        }

        public async Task UpdateUserAsync(string username, string rawPassword, string fullName, string storeCode)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username cannot be empty.");

            string cleanUser = username.Trim();
            string cleanFull = (fullName ?? cleanUser).Trim();
            var nameParts = cleanFull.Split(new[] { ' ' }, 2);
            string fName = nameParts[0];
            string lName = nameParts.Length > 1 ? nameParts[1] : "";

            string encodedPassword = EncodeBase64(rawPassword ?? cleanUser);
            string cleanStoreCode = (storeCode ?? string.Empty).Trim();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. Update PUREGOLD
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "UPDATE PUREGOLD.dbo.tblUsers SET password = @pass, fullname = @fname WHERE username = @user";
                            cmd.Parameters.AddWithValue("@user", cleanUser);
                            cmd.Parameters.AddWithValue("@pass", encodedPassword);
                            cmd.Parameters.AddWithValue("@fname", cleanFull);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // 2. Update AGING_DB
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;
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

                            cmd.Parameters.AddWithValue("@user", cleanUser);
                            cmd.Parameters.AddWithValue("@pass", encodedPassword);
                            cmd.Parameters.AddWithValue("@fName", fName);
                            cmd.Parameters.AddWithValue("@lName", lName);
                            cmd.Parameters.AddWithValue("@storeCode", cleanStoreCode);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        try { transaction.Rollback(); } catch { }
                        ErrorLoggerService.LogException($"UserService.UpdateUserAsync({cleanUser})", ex);
                        throw;
                    }
                }
            }
        }

        public async Task DeleteUserAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            string cleanUser = username.Trim();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "DELETE FROM PUREGOLD.dbo.tblUsers WHERE username = @user";
                            cmd.Parameters.AddWithValue("@user", cleanUser);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = "DELETE FROM AGING_DB.dbo.tblUsers WHERE uName = @user";
                            cmd.Parameters.AddWithValue("@user", cleanUser);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        try { transaction.Rollback(); } catch { }
                        ErrorLoggerService.LogException($"UserService.DeleteUserAsync({cleanUser})", ex);
                        throw;
                    }
                }
            }
        }
    }
}