using Microsoft.AspNetCore.Identity.Data;
using Npgsql;
using Nutriflow.DTOs;

namespace Nutriflow.Services
{
    public class ServicioRegister
    {
        private readonly IConfiguration _configuration;

        // INYECCIONES DE DEPENDENCIA
        public ServicioRegister(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<RegisterResponse?> Register(DTOs.RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Nombre) ||
                    string.IsNullOrWhiteSpace(request.Apellido) ||
                    string.IsNullOrWhiteSpace(request.Email) ||
                    string.IsNullOrWhiteSpace(request.Password) ||
                    string.IsNullOrWhiteSpace(request.Rol))
                {
                    return null;
                }

                var connectionString = _configuration.GetConnectionString("SupabaseConnection");

                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                //USAMOS TRANSACCIÓN PARA ASEGURAR TODO O NADA
                await using var tx = await conn.BeginTransactionAsync();

                // INSERTA EL USUARIO (EMAIL DEBE SER UNIQUE EN DB)
                var insertUsuarioQuery = @"
                                    INSERT INTO usuarios (nombre, apellido, email, ""contraseña"")
                                    VALUES (@nombre, @apellido, @email, @password)
                                    RETURNING id, nombre, email;";

                await using var insertCmd = new NpgsqlCommand(insertUsuarioQuery, conn, tx);
                insertCmd.Parameters.AddWithValue("nombre", request.Nombre);
                insertCmd.Parameters.AddWithValue("apellido", request.Apellido);
                insertCmd.Parameters.AddWithValue("email", request.Email);
                insertCmd.Parameters.AddWithValue("password", request.Password);

                await using var reader = await insertCmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    await tx.RollbackAsync();
                    return null;
                }

                var usuarioId = reader.GetGuid(reader.GetOrdinal("id"));

                var user = new UserDto
                {
                    Id = usuarioId,
                    Nombre = reader["nombre"]?.ToString() ?? string.Empty,
                    Email = reader["email"]?.ToString() ?? string.Empty
                };

                await reader.CloseAsync();

                // SEGÚN EL ROL, INSERTA EN TABLA HIJA (HERENCIA)
                if (request.Rol.ToLower() == "paciente")
                {
                    var insertPaciente = @"INSERT INTO pacientes (id) VALUES (@id);";

                    await using var cmd = new NpgsqlCommand(insertPaciente, conn, tx);
                    cmd.Parameters.AddWithValue("id", usuarioId);
                    await cmd.ExecuteNonQueryAsync();
                }
                else if (request.Rol.ToLower() == "nutricionista")
                {
                    var insertNutricionista = @"INSERT INTO nutricionistas (id) VALUES (@id);";

                    await using var cmd = new NpgsqlCommand(insertNutricionista, conn, tx);
                    cmd.Parameters.AddWithValue("id", usuarioId);
                    await cmd.ExecuteNonQueryAsync();
                }
                else
                {
                    await tx.RollbackAsync();
                    return null;
                }

                //CONFIRMA TODO
                await tx.CommitAsync();

                return new RegisterResponse
                {
                    Message = "Registro correcto",
                    User = user
                };
            }
            catch (PostgresException ex) when (ex.SqlState == "23505") // UNIQUE VIOLATION
            {
                //EMAIL DUPLICADO
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR REGISTER:");
                Console.WriteLine(ex.ToString());
                throw;
            }
        }


    }
}