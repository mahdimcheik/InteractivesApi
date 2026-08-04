using InteractivesApi.Models.Presentations;
using InteractivesApi.Models.Responses;
using InteractivesApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InteractivesApi.Controllers
{
    [Produces("application/json")]
    [Route("presentations/{presentationId:guid}/slides/{slideId:guid}/elements")]
    [ApiController]
    [Authorize]
    public class ElementsController : ControllerBase
    {
        private readonly IPresentationService _presentationService;

        public ElementsController(IPresentationService presentationService)
        {
            _presentationService = presentationService;
        }

        [HttpGet]
        public async Task<ActionResult<Response<List<ElementDetails>>>> GetElements(Guid presentationId, Guid slideId)
        {
            var result = await _presentationService.GetElements(presentationId, slideId, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpGet("{elementId:guid}")]
        public async Task<ActionResult<Response<ElementDetails>>> GetElement(Guid presentationId, Guid slideId, Guid elementId)
        {
            var result = await _presentationService.GetElement(presentationId, slideId, elementId, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpPost]
        public async Task<ActionResult<Response<ElementDetails>>> CreateElement(Guid presentationId, Guid slideId, [FromBody] CreateElementRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new Response<object> { Status = 400, Message = "Problème de validation." });

            var result = await _presentationService.CreateElement(presentationId, slideId, model, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpPut("{elementId:guid}")]
        public async Task<ActionResult<Response<ElementDetails>>> UpdateElement(Guid presentationId, Guid slideId, Guid elementId, [FromBody] UpdateElementRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new Response<object> { Status = 400, Message = "Problème de validation." });

            var result = await _presentationService.UpdateElement(presentationId, slideId, elementId, model, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpDelete("{elementId:guid}")]
        public async Task<ActionResult<Response<object>>> DeleteElement(Guid presentationId, Guid slideId, Guid elementId)
        {
            var result = await _presentationService.DeleteElement(presentationId, slideId, elementId, HttpContext.User);
            return StatusCode(result.Status, result);
        }
    }
}
