using Microsoft.AspNetCore.Mvc;
using Nutriflow.Dtos;
using Nutriflow.DTOs;
using Nutriflow.Services;

namespace Nutriflow.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class ForgotPasswordController : ControllerBase
    {
        private readonly ServicioForgotPassword _servicioForgotPassword;

        // INYECCIONES DE DEPENDENCIA
        public ForgotPasswordController(ServicioForgotPassword servicioForgotPassword)
        {
            _servicioForgotPassword = servicioForgotPassword;
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            // VERIFICA QUE NO SEA NULO EL EMAIL
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { message = "El email es obligatorio" });
            }

            // LLAMA AL SERVICIO CORRESPONDIENTE
            var resultado = await _servicioForgotPassword.ForgotPassword(request);

            // RETORNA OK
            return Ok(resultado);
        }
    }
}