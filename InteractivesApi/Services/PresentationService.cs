using System.Security.Claims;
using InteractivesApi.Contexts;
using InteractivesApi.Models.Presentations;
using InteractivesApi.Models.Responses;
using InteractivesApi.Models.Users;
using InteractivesApi.Utilities;
using Microsoft.EntityFrameworkCore;

namespace InteractivesApi.Services
{
    public interface IPresentationService
    {
        // Presentations
        Task<Response<PresentationDetails>> CreatePresentation(
            CreatePresentationRequest req,
            ClaimsPrincipal principal
        );
        Task<Response<List<PresentationSummary>>> GetMyPresentations(ClaimsPrincipal principal);
        Task<Response<PresentationDetails>> GetPresentation(
            Guid presentationId,
            ClaimsPrincipal principal
        );
        Task<Response<PresentationDetails>> UpdatePresentation(
            Guid presentationId,
            UpdatePresentationRequest req,
            ClaimsPrincipal principal
        );
        Task<Response<object>> DeletePresentation(Guid presentationId, ClaimsPrincipal principal);

        // Slides
        Task<Response<SlideDetails>> CreateSlide(
            Guid presentationId,
            CreateSlideRequest req,
            ClaimsPrincipal principal
        );
        Task<Response<List<SlideDetails>>> GetSlides(
            Guid presentationId,
            ClaimsPrincipal principal
        );
        Task<Response<SlideDetails>> GetSlide(
            Guid presentationId,
            Guid slideId,
            ClaimsPrincipal principal
        );
        Task<Response<SlideDetails>> UpdateSlide(
            Guid presentationId,
            Guid slideId,
            UpdateSlideRequest req,
            ClaimsPrincipal principal
        );
        Task<Response<object>> DeleteSlide(
            Guid presentationId,
            Guid slideId,
            ClaimsPrincipal principal
        );
        Task<Response<object>> ReorderSlides(
            Guid presentationId,
            ReorderSlidesRequest req,
            ClaimsPrincipal principal
        );

        // Elements
        Task<Response<ElementDetails>> CreateElement(
            Guid presentationId,
            Guid slideId,
            CreateElementRequest req,
            ClaimsPrincipal principal
        );
        Task<Response<List<ElementDetails>>> GetElements(
            Guid presentationId,
            Guid slideId,
            ClaimsPrincipal principal
        );
        Task<Response<ElementDetails>> GetElement(
            Guid presentationId,
            Guid slideId,
            Guid elementId,
            ClaimsPrincipal principal
        );
        Task<Response<ElementDetails>> UpdateElement(
            Guid presentationId,
            Guid slideId,
            Guid elementId,
            UpdateElementRequest req,
            ClaimsPrincipal principal
        );
        Task<Response<object>> DeleteElement(
            Guid presentationId,
            Guid slideId,
            Guid elementId,
            ClaimsPrincipal principal
        );
    }

    public class PresentationService : IPresentationService
    {
        private readonly MainContext _context;

        public PresentationService(MainContext context)
        {
            _context = context;
        }

        // ---- Presentations ----

        public async Task<Response<PresentationDetails>> CreatePresentation(
            CreatePresentationRequest req,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<PresentationDetails>(401, "Vous n'êtes pas connecté.");

            var presentation = new Presentation
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                CreatorId = user.Id,
            };

            _context.Presentations.Add(presentation);
            await _context.SaveChangesAsync();

            return new Response<PresentationDetails>
            {
                Status = 201,
                Message = "Présentation créée.",
                Data = new PresentationDetails(presentation),
            };
        }

        public async Task<Response<List<PresentationSummary>>> GetMyPresentations(
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<List<PresentationSummary>>(401, "Vous n'êtes pas connecté.");

            var rows = await _context
                .Presentations.Where(p => p.CreatorId == user.Id && p.ArchivedAt == null)
                .Select(p => new
                {
                    Presentation = p,
                    SlideCount = p.Slides.Count(s => s.ArchivedAt == null),
                })
                .ToListAsync();

            var data = rows.Select(r => new PresentationSummary(r.Presentation, r.SlideCount))
                .ToList();
            return new Response<List<PresentationSummary>>
            {
                Status = 200,
                Message = "Présentations récupérées.",
                Data = data,
                Count = data.Count,
            };
        }

        public async Task<Response<PresentationDetails>> GetPresentation(
            Guid presentationId,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<PresentationDetails>(401, "Vous n'êtes pas connecté.");

            var (presentation, error) = await LoadOwnedPresentation(
                presentationId,
                user,
                principal
            );
            if (error is not null)
                return Fail<PresentationDetails>(error.Value.Status, error.Value.Message);

            return new Response<PresentationDetails>
            {
                Status = 200,
                Message = "Présentation récupérée.",
                Data = new PresentationDetails(presentation!),
            };
        }

        public async Task<Response<PresentationDetails>> UpdatePresentation(
            Guid presentationId,
            UpdatePresentationRequest req,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<PresentationDetails>(401, "Vous n'êtes pas connecté.");

            var (presentation, error) = await LoadOwnedPresentation(
                presentationId,
                user,
                principal
            );
            if (error is not null)
                return Fail<PresentationDetails>(error.Value.Status, error.Value.Message);

            presentation!.Title = req.Title;
            presentation.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            return new Response<PresentationDetails>
            {
                Status = 200,
                Message = "Présentation mise à jour.",
                Data = new PresentationDetails(presentation),
            };
        }

        public async Task<Response<object>> DeletePresentation(
            Guid presentationId,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<object>(401, "Vous n'êtes pas connecté.");

            var (presentation, error) = await LoadOwnedPresentation(
                presentationId,
                user,
                principal
            );
            if (error is not null)
                return Fail<object>(error.Value.Status, error.Value.Message);

            presentation!.ArchivedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            return new Response<object> { Status = 200, Message = "Présentation supprimée." };
        }

        // ---- Slides ----

        public async Task<Response<SlideDetails>> CreateSlide(
            Guid presentationId,
            CreateSlideRequest req,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<SlideDetails>(401, "Vous n'êtes pas connecté.");

            var (presentation, error) = await LoadOwnedPresentation(
                presentationId,
                user,
                principal
            );
            if (error is not null)
                return Fail<SlideDetails>(error.Value.Status, error.Value.Message);

            var order =
                req.Order
                ?? await _context.Slides.CountAsync(s =>
                    s.PresentationId == presentationId && s.ArchivedAt == null
                );

            var slide = new Slide
            {
                Id = Guid.NewGuid(),
                PresentationId = presentationId,
                Order = order,
                BackgroundColor = req.BackgroundColor,
            };

            _context.Slides.Add(slide);
            await _context.SaveChangesAsync();

            return new Response<SlideDetails>
            {
                Status = 201,
                Message = "Diapositive créée.",
                Data = new SlideDetails(slide, 0),
            };
        }

        public async Task<Response<List<SlideDetails>>> GetSlides(
            Guid presentationId,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<List<SlideDetails>>(401, "Vous n'êtes pas connecté.");

            var (_, error) = await LoadOwnedPresentation(presentationId, user, principal);
            if (error is not null)
                return Fail<List<SlideDetails>>(error.Value.Status, error.Value.Message);

            var rows = await _context
                .Slides.Where(s => s.PresentationId == presentationId && s.ArchivedAt == null)
                .OrderBy(s => s.Order)
                .Select(s => new
                {
                    Slide = s,
                    ElementCount = s.Elements.Count(e => e.ArchivedAt == null),
                })
                .ToListAsync();

            var data = rows.Select(r => new SlideDetails(r.Slide, r.ElementCount)).ToList();
            return new Response<List<SlideDetails>>
            {
                Status = 200,
                Message = "Diapositives récupérées.",
                Data = data,
                Count = data.Count,
            };
        }

        public async Task<Response<SlideDetails>> GetSlide(
            Guid presentationId,
            Guid slideId,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<SlideDetails>(401, "Vous n'êtes pas connecté.");

            var (_, slide, error) = await LoadOwnedSlide(presentationId, slideId, user, principal);
            if (error is not null)
                return Fail<SlideDetails>(error.Value.Status, error.Value.Message);

            var elementCount = await _context.Elements.CountAsync(e =>
                e.SlideId == slideId && e.ArchivedAt == null
            );
            return new Response<SlideDetails>
            {
                Status = 200,
                Message = "Diapositive récupérée.",
                Data = new SlideDetails(slide!, elementCount),
            };
        }

        public async Task<Response<SlideDetails>> UpdateSlide(
            Guid presentationId,
            Guid slideId,
            UpdateSlideRequest req,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<SlideDetails>(401, "Vous n'êtes pas connecté.");

            var (_, slide, error) = await LoadOwnedSlide(presentationId, slideId, user, principal);
            if (error is not null)
                return Fail<SlideDetails>(error.Value.Status, error.Value.Message);

            slide!.BackgroundColor = req.BackgroundColor;
            slide.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            var elementCount = await _context.Elements.CountAsync(e =>
                e.SlideId == slideId && e.ArchivedAt == null
            );
            return new Response<SlideDetails>
            {
                Status = 200,
                Message = "Diapositive mise à jour.",
                Data = new SlideDetails(slide, elementCount),
            };
        }

        public async Task<Response<object>> DeleteSlide(
            Guid presentationId,
            Guid slideId,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<object>(401, "Vous n'êtes pas connecté.");

            var (_, slide, error) = await LoadOwnedSlide(presentationId, slideId, user, principal);
            if (error is not null)
                return Fail<object>(error.Value.Status, error.Value.Message);

            slide!.ArchivedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            return new Response<object> { Status = 200, Message = "Diapositive supprimée." };
        }

        public async Task<Response<object>> ReorderSlides(
            Guid presentationId,
            ReorderSlidesRequest req,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<object>(401, "Vous n'êtes pas connecté.");

            var (_, error) = await LoadOwnedPresentation(presentationId, user, principal);
            if (error is not null)
                return Fail<object>(error.Value.Status, error.Value.Message);

            var slides = await _context
                .Slides.Where(s => s.PresentationId == presentationId && s.ArchivedAt == null)
                .ToListAsync();

            var currentIds = slides.Select(s => s.Id).ToHashSet();
            var submittedIds = req.SlideIds;
            if (
                submittedIds.Count != currentIds.Count
                || submittedIds.Distinct().Count() != submittedIds.Count
                || !submittedIds.All(currentIds.Contains)
            )
                return Fail<object>(400, "Liste de diapositives invalide.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var byId = slides.ToDictionary(s => s.Id);
                for (var i = 0; i < submittedIds.Count; i++)
                {
                    byId[submittedIds[i]].Order = i;
                    byId[submittedIds[i]].UpdatedAt = DateTimeOffset.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }

            return new Response<object>
            {
                Status = 200,
                Message = "Ordre des diapositives mis à jour.",
            };
        }

        // ---- Elements ----

        public async Task<Response<ElementDetails>> CreateElement(
            Guid presentationId,
            Guid slideId,
            CreateElementRequest req,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<ElementDetails>(401, "Vous n'êtes pas connecté.");

            var (_, _, error) = await LoadOwnedSlide(presentationId, slideId, user, principal);
            if (error is not null)
                return Fail<ElementDetails>(error.Value.Status, error.Value.Message);

            var configError = ElementConfigurationValidator.Validate(req.Type, req.Configuration);
            if (configError is not null)
                return Fail<ElementDetails>(400, configError);

            var currentMaxZIndex = await _context
                .Elements.Where(e => e.SlideId == slideId && e.ArchivedAt == null)
                .Select(e => (int?)e.ZIndex)
                .MaxAsync();
            var zIndex = req.ZIndex ?? (currentMaxZIndex + 1 ?? 0);

            var element = new Element
            {
                Id = Guid.NewGuid(),
                SlideId = slideId,
                Type = req.Type,
                X = req.X,
                Y = req.Y,
                Width = req.Width,
                Height = req.Height,
                Rotation = req.Rotation,
                ZIndex = zIndex,
                Locked = req.Locked,
                Visible = req.Visible,
                Configuration = req.Configuration,
            };

            _context.Elements.Add(element);
            await _context.SaveChangesAsync();

            return new Response<ElementDetails>
            {
                Status = 201,
                Message = "Élément créé.",
                Data = new ElementDetails(element),
            };
        }

        public async Task<Response<List<ElementDetails>>> GetElements(
            Guid presentationId,
            Guid slideId,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<List<ElementDetails>>(401, "Vous n'êtes pas connecté.");

            var (_, _, error) = await LoadOwnedSlide(presentationId, slideId, user, principal);
            if (error is not null)
                return Fail<List<ElementDetails>>(error.Value.Status, error.Value.Message);

            var elements = await _context
                .Elements.Where(e => e.SlideId == slideId && e.ArchivedAt == null)
                .OrderBy(e => e.ZIndex)
                .ToListAsync();

            var data = elements.Select(e => new ElementDetails(e)).ToList();
            return new Response<List<ElementDetails>>
            {
                Status = 200,
                Message = "Éléments récupérés.",
                Data = data,
                Count = data.Count,
            };
        }

        public async Task<Response<ElementDetails>> GetElement(
            Guid presentationId,
            Guid slideId,
            Guid elementId,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<ElementDetails>(401, "Vous n'êtes pas connecté.");

            var (_, _, element, error) = await LoadOwnedElement(
                presentationId,
                slideId,
                elementId,
                user,
                principal
            );
            if (error is not null)
                return Fail<ElementDetails>(error.Value.Status, error.Value.Message);

            return new Response<ElementDetails>
            {
                Status = 200,
                Message = "Élément récupéré.",
                Data = new ElementDetails(element!),
            };
        }

        public async Task<Response<ElementDetails>> UpdateElement(
            Guid presentationId,
            Guid slideId,
            Guid elementId,
            UpdateElementRequest req,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<ElementDetails>(401, "Vous n'êtes pas connecté.");

            var (_, _, element, error) = await LoadOwnedElement(
                presentationId,
                slideId,
                elementId,
                user,
                principal
            );
            if (error is not null)
                return Fail<ElementDetails>(error.Value.Status, error.Value.Message);

            var configError = ElementConfigurationValidator.Validate(
                element!.Type,
                req.Configuration
            );
            if (configError is not null)
                return Fail<ElementDetails>(400, configError);

            element.X = req.X;
            element.Y = req.Y;
            element.Width = req.Width;
            element.Height = req.Height;
            element.Rotation = req.Rotation;
            element.ZIndex = req.ZIndex;
            element.Locked = req.Locked;
            element.Visible = req.Visible;
            element.Configuration = req.Configuration;
            element.UpdatedAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync();

            return new Response<ElementDetails>
            {
                Status = 200,
                Message = "Élément mis à jour.",
                Data = new ElementDetails(element),
            };
        }

        public async Task<Response<object>> DeleteElement(
            Guid presentationId,
            Guid slideId,
            Guid elementId,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<object>(401, "Vous n'êtes pas connecté.");

            var (_, _, element, error) = await LoadOwnedElement(
                presentationId,
                slideId,
                elementId,
                user,
                principal
            );
            if (error is not null)
                return Fail<object>(error.Value.Status, error.Value.Message);

            element!.ArchivedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();

            return new Response<object> { Status = 200, Message = "Élément supprimé." };
        }

        // ---- Ownership helpers ----

        private static bool IsAdmin(ClaimsPrincipal principal) =>
            principal.IsInRole(HardCode.ROLE_NAME_ADMIN)
            || principal.IsInRole(HardCode.ROLE_NAME_SUPER_ADMIN);

        private async Task<(
            Presentation? Presentation,
            (int Status, string Message)? Error
        )> LoadOwnedPresentation(Guid presentationId, UserApp user, ClaimsPrincipal principal)
        {
            var presentation = await _context
                .Presentations.Where(p => p.Id == presentationId && p.ArchivedAt == null)
                // Suppression logique : les diapositives et éléments archivés ne sont jamais renvoyés.
                .Include(p => p.Slides.Where(s => s.ArchivedAt == null).OrderBy(s => s.Order))
                .ThenInclude(s => s.Elements.Where(e => e.ArchivedAt == null))
                .FirstOrDefaultAsync();

            if (presentation is null)
                return (null, (404, "Présentation introuvable."));

            if (presentation.CreatorId != user.Id && !IsAdmin(principal))
                return (null, (403, "Vous n'avez pas accès à cette présentation."));

            return (presentation, null);
        }

        /// <summary>
        /// Also checks <paramref name="slideId"/> actually belongs to <paramref name="presentationId"/>
        /// (IDOR guard) — a mismatch fails 404, not 403, to avoid confirming the other slide exists.
        /// </summary>
        private async Task<(
            Presentation? Presentation,
            Slide? Slide,
            (int Status, string Message)? Error
        )> LoadOwnedSlide(
            Guid presentationId,
            Guid slideId,
            UserApp user,
            ClaimsPrincipal principal
        )
        {
            var (presentation, error) = await LoadOwnedPresentation(
                presentationId,
                user,
                principal
            );
            if (error is not null)
                return (null, null, error);

            var slide = await _context.Slides.FirstOrDefaultAsync(s =>
                s.Id == slideId && s.PresentationId == presentationId && s.ArchivedAt == null
            );
            if (slide is null)
                return (null, null, (404, "Diapositive introuvable."));

            return (presentation, slide, null);
        }

        /// <summary>Same IDOR guard as <see cref="LoadOwnedSlide"/>, one level deeper.</summary>
        private async Task<(
            Presentation? Presentation,
            Slide? Slide,
            Element? Element,
            (int Status, string Message)? Error
        )> LoadOwnedElement(
            Guid presentationId,
            Guid slideId,
            Guid elementId,
            UserApp user,
            ClaimsPrincipal principal
        )
        {
            var (presentation, slide, error) = await LoadOwnedSlide(
                presentationId,
                slideId,
                user,
                principal
            );
            if (error is not null)
                return (null, null, null, error);

            var element = await _context.Elements.FirstOrDefaultAsync(e =>
                e.Id == elementId && e.SlideId == slideId && e.ArchivedAt == null
            );
            if (element is null)
                return (null, null, null, (404, "Élément introuvable."));

            return (presentation, slide, element, null);
        }

        private static Response<T> Fail<T>(int status, string message) =>
            new() { Status = status, Message = message };
    }
}
