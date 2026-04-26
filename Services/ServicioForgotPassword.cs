using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Nutriflow.Dtos;

namespace Nutriflow.Services
{
    public class ServicioForgotPassword
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ServicioForgotPassword> _logger;
        private readonly EmailService _emailService;

        public ServicioForgotPassword(
            IConfiguration configuration,
            ILogger<ServicioForgotPassword> logger,
            EmailService emailService)
        {
            _configuration = configuration;
            _logger = logger;
            _emailService = emailService;
        }

        private static string GenerateToken()
        {
            return RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        }

        public async Task<ForgotPasswordResponse> ForgotPassword(ForgotPasswordRequest request)
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

            await using var cmd = new NpgsqlCommand(selectUser, conn);
            cmd.Parameters.AddWithValue("email", request.Email);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return new ForgotPasswordResponse
                {
                    Success = true,
                    Message = "Si el email existe, se ha enviado un enlace para restablecer la contraseña."
                };
            }

            var userId = reader.GetGuid(reader.GetOrdinal("id"));
            await reader.CloseAsync();

            var token = GenerateToken();
            var tokenHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(token))
            );

            var expiresAt = DateTime.UtcNow.AddHours(1);

            var insertReset = @"
                INSERT INTO password_resets 
                (id, user_id, token_hash, expires_at, used_at, request_ip, user_agent)
                VALUES 
                (@id, @user_id, @token_hash, @expires_at, NULL, NULL, NULL);";

            await using var insertCmd = new NpgsqlCommand(insertReset, conn);
            insertCmd.Parameters.AddWithValue("id", Guid.NewGuid());
            insertCmd.Parameters.AddWithValue("user_id", userId);
            insertCmd.Parameters.AddWithValue("token_hash", tokenHash);
            insertCmd.Parameters.AddWithValue("expires_at", expiresAt);

            await insertCmd.ExecuteNonQueryAsync();

            var frontendBase = _configuration["FrontendBaseUrl"] ?? "http://localhost:8081";

            var resetLink = $"{frontendBase.TrimEnd('/')}/auth/reset-password?email={Uri.EscapeDataString(request.Email)}";

            await _emailService.EnviarRecuperacionPassword(request.Email, token, resetLink);

            return new ForgotPasswordResponse
            {
                Success = true,
                Message = "Si el email existe, se ha enviado un enlace para restablecer la contraseña."
            };
        }

        public async Task<ResetPasswordResponse> ResetPassword(ResetPasswordRequest request)
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

            var incomingHash = Convert.ToBase64String(
                SHA256.HashData(Encoding.UTF8.GetBytes(request.Token))
            );

            var selectReset = @"
                SELECT pr.id AS reset_id, pr.user_id
                FROM password_resets pr
                WHERE pr.token_hash = @token_hash
                  AND pr.used_at IS NULL
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

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            var updateUser = @"UPDATE usuarios SET ""contraseña"" = @password WHERE id = @userId;";

            await using (var updateCmd = new NpgsqlCommand(updateUser, conn))
            {
                updateCmd.Parameters.AddWithValue("password", passwordHash);
                updateCmd.Parameters.AddWithValue("userId", userId);

                await updateCmd.ExecuteNonQueryAsync();
            }

            var markUsed = @"UPDATE password_resets SET used_at = NOW() WHERE id = @resetId;";

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
    }
}