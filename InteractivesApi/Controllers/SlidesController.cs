using InteractivesApi.Models.Presentations;
using InteractivesApi.Models.Responses;
using InteractivesApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InteractivesApi.Controllers
{
    [Produces("application/json")]
    [Route("presentations/{presentationId:guid}/slides")]
    [ApiController]
    [Authorize]
    public class SlidesController : ControllerBase
    {
        private readonly IPresentationService _presentationService;

        public SlidesController(IPresentationService presentationService)
        {
            _presentationService = presentationService;
        }

        [HttpGet]
        public async Task<ActionResult<Response<List<SlideDetails>>>> GetSlides(Guid presentationId)
        {
            var result = await _presentationService.GetSlides(presentationId, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpGet("{slideId:guid}")]
        public async Task<ActionResult<Response<SlideDetails>>> GetSlide(Guid presentationId, Guid slideId)
        {
            var result = await _presentationService.GetSlide(presentationId, slideId, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpPost]
        public async Task<ActionResult<Response<SlideDetails>>> CreateSlide(Guid presentationId, [FromBody] CreateSlideRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new Response<object> { Status = 400, Message = "Problème de validation." });

            var result = await _presentationService.CreateSlide(presentationId, model, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpPut("{slideId:guid}")]
        public async Task<ActionResult<Response<SlideDetails>>> UpdateSlide(Guid presentationId, Guid slideId, [FromBody] UpdateSlideRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new Response<object> { Status = 400, Message = "Problème de validation." });

            var result = await _presentationService.UpdateSlide(presentationId, slideId, model, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpDelete("{slideId:guid}")]
        public async Task<ActionResult<Response<object>>> DeleteSlide(Guid presentationId, Guid slideId)
        {
            var result = await _presentationService.DeleteSlide(presentationId, slideId, HttpContext.User);
            return StatusCode(result.Status, result);
        }

        [HttpPost("reorder")]
        public async Task<ActionResult<Response<object>>> ReorderSlides(Guid presentationId, [FromBody] ReorderSlidesRequest model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new Response<object> { Status = 400, Message = "Problème de validation." });

            var result = await _presentationService.ReorderSlides(presentationId, model, HttpContext.User);
            return StatusCode(result.Status, result);
        }
    }
}
