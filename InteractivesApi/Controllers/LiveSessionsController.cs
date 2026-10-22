using InteractivesApi.Models.LiveSessions;
using InteractivesApi.Models.Presentations;
using InteractivesApi.Models.Responses;
using InteractivesApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InteractivesApi.Controllers
{
    [Produces("application/json")]
    [Route("live-sessions")]
    [ApiController]
    [Authorize]
    public class LiveSessionsController : ControllerBase
    {
        private readonly ILiveSessionService _liveSessions;

        public LiveSessionsController(ILiveSessionService liveSessions)
        {
            _liveSessions = liveSessions;
        }

        [HttpPost]
        public async Task<ActionResult<Response<LiveSessionDetails>>> Start(
            [FromBody] StartLiveSessionRequest model
        )
        {
            if (!ModelState.IsValid)
                return BadRequest(
                    new Response<object> { Status = 400, Message = "Problème de validation." }
                );

            var result = await _liveSessions.Start(model, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpGet("{code}")]
        public async Task<ActionResult<Response<LiveSessionDetails>>> Get(string code)
        {
            var result = await _liveSessions.Get(code, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpGet("{code}/presentation")]
        public async Task<ActionResult<Response<PresentationDetails>>> GetPresentation(string code)
        {
            var result = await _liveSessions.GetPresentation(code, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpDelete("{code}")]
        public async Task<ActionResult<Response<object>>> End(string code)
        {
            var result = await _liveSessions.End(code, HttpContext.User);
            return StatusCode(result.Status, result);
        }
    }
}
