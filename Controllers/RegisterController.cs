using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Nutriflow.DTOs;
using Nutriflow.Services;

namespace Nutriflow.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class RegisterController : ControllerBase
    {
        private readonly ServicioRegister _servicioRegister;

        // INYECCIONES DE DEPENDENCIA
        public RegisterController(ServicioRegister servicioRegister)
        {
            _servicioRegister = servicioRegister;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] DTOs.RegisterRequest request)
        {
            // VERIFICA QUE NO SEAN NULOS LOS PARAMETROS
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Nombre) ||
                string.IsNullOrWhiteSpace(request.Apellido) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.Rol))
         
            {
                return BadRequest(new { message = "Todos los campos son obligatorios" });
            }

            // LLAMA AL SERVICIO CORRESPONDIENTE
            var resultado = await _servicioRegister.Register(request);

            // SI EL EMAIL YA EXISTE, ENVIA MENSAJE DE CONFLICTO
            if (resultado == null)
            {
                return Conflict(new { message = "El email ya está registrado" });
            }

            // RETORNA OK SI SALIO TODO BIEN
            return Ok(resultado);
        }
    }
}