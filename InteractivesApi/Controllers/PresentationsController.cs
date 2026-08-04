using InteractivesApi.Models.Presentations;
using InteractivesApi.Models.Responses;
using InteractivesApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InteractivesApi.Controllers
{
    [Produces("application/json")]
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class PresentationsController : ControllerBase
    {
        private readonly IPresentationService _presentationService;

        public PresentationsController(IPresentationService presentationService)
        {
            _presentationService = presentationService;
        }

        [HttpGet]
        public async Task<ActionResult<Response<List<PresentationSummary>>>> GetMyPresentations()
        {
            var result = await _presentationService.GetMyPresentations(HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpGet("{presentationId:guid}")]
        public async Task<ActionResult<Response<PresentationDetails>>> GetPresentation(Guid presentationId)
        {
            var result = await _presentationService.GetPresentation(presentationId, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpPost]
        public async Task<ActionResult<Response<PresentationDetails>>> CreatePresentation([FromBody] CreatePresentationRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new Response<object> { Status = 400, Message = "Problème de validation." });

            var result = await _presentationService.CreatePresentation(model, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpPut("{presentationId:guid}")]
        public async Task<ActionResult<Response<PresentationDetails>>> UpdatePresentation(Guid presentationId, [FromBody] UpdatePresentationRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new Response<object> { Status = 400, Message = "Problème de validation." });

            var result = await _presentationService.UpdatePresentation(presentationId, model, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpDelete("{presentationId:guid}")]
        public async Task<ActionResult<Response<object>>> DeletePresentation(Guid presentationId)
        {
            var result = await _presentationService.DeletePresentation(presentationId, HttpContext.User);
            return StatusCode(result.Status, result);
        }
    }
}
