using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nutriflow.Dtos;

namespace Nutriflow.Services
{
    public class ServicioForgotPassword
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ServicioForgotPassword> _logger;

        public ServicioForgotPassword(IConfiguration configuration, ILogger<ServicioForgotPassword> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        private static string GenerateToken(int length = 32)
        {
            var bytes = new byte[length];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public async Task<ForgotPasswordResponse> ForgotPassword(ForgotPasswordRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email))
                {
                    return new ForgotPasswordResponse
                    {
                        Success = false,
                        Message = "El email es obligatorio."
                    };
                }

                var connectionString = _configuration.GetConnectionString("SupabaseConnection");
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                var selectUser = @"SELECT id FROM usuarios WHERE LOWER(email) = LOWER(@email) LIMIT 1;";
                await using (var cmd = new NpgsqlCommand(selectUser, conn))
                {
                    cmd.Parameters.AddWithValue("email", request.Email);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        _logger.LogInformation("ForgotPassword solicitado para email no existente: {Email}", request.Email);
                        return new ForgotPasswordResponse                               
                        {
                            Success = true,
                            Message = "Si el email existe, se ha enviado un enlace para reestablecer la contraseña."
                        };
                    }

                    var userId = reader.GetGuid(reader.GetOrdinal("id"));
                    await reader.CloseAsync();

                    // generate raw token and hashed token for storage
                    var token = GenerateToken();
                    var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
                    var expiresAt = DateTime.UtcNow.AddHours(1);

                    // store hashed token in token_hash column
                    var insertReset = @"
                        INSERT INTO password_resets 
                        (id, user_id, token_hash, expires_at, used_at, request_ip, user_agent)
                        VALUES 
                        (@id, @user_id, @token_hash, @expires_at, NULL, NULL, NULL);";

                    await using (var insertCmd = new NpgsqlCommand(insertReset, conn))
                    {
                        insertCmd.Parameters.AddWithValue("id", Guid.NewGuid());
                        insertCmd.Parameters.AddWithValue("user_id", userId);
                        insertCmd.Parameters.AddWithValue("token_hash", tokenHash);
                        insertCmd.Parameters.AddWithValue("expires_at", expiresAt);
                        await insertCmd.ExecuteNonQueryAsync();
                    }

                    var frontendBase = _configuration["FrontendBaseUrl"] ?? string.Empty;
                    var resetLink = string.IsNullOrWhiteSpace(frontendBase)
                        ? $"token={Uri.EscapeDataString(token)}"
                        : $"{frontendBase.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

                    // always log server-side for testing
                    _logger.LogInformation("Password reset token generado para user {UserId}. Token (DEV): {Token}", userId, token);
                   
                    Console.WriteLine($"[DEV] Password reset token para {request.Email}: {token}");
                    Console.WriteLine($"[DEV] Reset link: {resetLink}");

                    // return raw token/reset link to client only in Development
                    var env = _configuration["ASPNETCORE_ENVIRONMENT"] ?? string.Empty;
                    var isDev = env.Equals("Development", StringComparison.OrdinalIgnoreCase);

                    return new ForgotPasswordResponse
                    {
                        Success = true,
                        Message = "Si el email existe, se ha enviado un enlace para reestablecer la contraseña.",
                        //SOLO PARA DESARROLLO EN PRODUCCION NO VA
                        Code = isDev ? token : string.Empty,//
                        ResetLink = isDev ? resetLink : string.Empty
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ForgotPassword");
                throw;
            }
        }

        public async Task<ResetPasswordResponse> ResetPassword(ResetPasswordRequest request)
        {
            try
            {
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.Token) ||
                    string.IsNullOrWhiteSpace(request.NewPassword) ||
                    string.IsNullOrWhiteSpace(request.ConfirmPassword))
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Datos inválidos."
                    };
                }

                if (request.NewPassword != request.ConfirmPassword)
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Las contraseñas no coinciden."
                    };
                }

                var connectionString = _configuration.GetConnectionString("SupabaseConnection");
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                // compare hashed incoming token with stored token_hash
                var incomingHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)));

                var selectReset = @"
                    SELECT pr.id AS reset_id, pr.user_id
                    FROM password_resets pr
                    WHERE pr.token_hash = @token_hash
                      AND pr.used = false
                      AND pr.expires_at > NOW()
                    LIMIT 1;";

                Guid resetId;
                Guid userId;

                await using (var cmd = new NpgsqlCommand(selectReset, conn))
                {
                    cmd.Parameters.AddWithValue("token_hash", incomingHash);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return new ResetPasswordResponse
                        {
                            Success = false,
                            Message = "Token inválido o expirado."
                        };
                    }

                    resetId = reader.GetGuid(reader.GetOrdinal("reset_id"));
                    userId = reader.GetGuid(reader.GetOrdinal("user_id"));
                    await reader.CloseAsync();
                }

                var updateUser = @"UPDATE usuarios SET ""contraseña"" = @password WHERE id = @userId;";
                await using (var updateCmd = new NpgsqlCommand(updateUser, conn))
                {
                    updateCmd.Parameters.AddWithValue("password", request.NewPassword);
                    updateCmd.Parameters.AddWithValue("userId", userId);
                    await updateCmd.ExecuteNonQueryAsync();
                }

                var markUsed = @"UPDATE password_resets SET used = true WHERE id = @resetId;";
                await using (var markCmd = new NpgsqlCommand(markUsed, conn))
                {
                    markCmd.Parameters.AddWithValue("resetId", resetId);
                    await markCmd.ExecuteNonQueryAsync();
                }

                return new ResetPasswordResponse
                {
                    Success = true,
                    Message = "La contraseña fue actualizada correctamente."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ResetPassword");
                throw;
            }
        }
    }
}