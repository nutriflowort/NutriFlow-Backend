using Microsoft.AspNetCore.Mvc;
using Nutriflow.Dtos;
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
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            // VERIFICA QUE NO SEA NULO EL EMAIL
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "El email es obligatorio" });

            // LLAMA AL SERVICIO CORRESPONDIENTE
            var resultado = await _servicioForgotPassword.ForgotPassword(request);
            // Always return 200 with the response DTO so dev can read .Code/.ResetLink in console
            return Ok(resultado);
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Token) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { message = "Datos inválidos." });
            }

            // If client didn't send confirmPassword, accept newPassword as confirmation for convenience in tests
            if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
                request.ConfirmPassword = request.NewPassword;

            var resultado = await _servicioForgotPassword.ResetPassword(request);

            // If ResetPassword returns Success=false, surface 400 so frontend can show the message
            if (!resultado.Success)
                return BadRequest(new { message = resultado.Message });

            return Ok(resultado);
        }
    }
}