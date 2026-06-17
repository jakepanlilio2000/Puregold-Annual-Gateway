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

        public async Task AddUserAsync(string username, string rawPassword, string firstName, string lastName)
        {
            string fullName = $"{firstName.Trim()} {lastName.Trim()}";
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO PUREGOLD.dbo.tblUsers (username, password, fullname) VALUES (@user, @pass, @fname)";
                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", EncodeBase64(rawPassword));
                    cmd.Parameters.AddWithValue("@fname", fullName);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task UpdateUserAsync(string username, string rawPassword, string firstName, string lastName)
        {
            string fullName = $"{firstName.Trim()} {lastName.Trim()}";
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE PUREGOLD.dbo.tblUsers SET password = @pass, fullname = @fname WHERE username = @user";
                    cmd.Parameters.AddWithValue("@user", username);
                    cmd.Parameters.AddWithValue("@pass", EncodeBase64(rawPassword));
                    cmd.Parameters.AddWithValue("@fname", fullName);
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
            }
        }
    }
}