using System.Security.Claims;
using System.Text.Json.Nodes;
using InteractivesApi.Contexts;
using InteractivesApi.Hubs;
using InteractivesApi.Models.LiveSessions;
using InteractivesApi.Models.Presentations;
using InteractivesApi.Models.Responses;
using InteractivesApi.Utilities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InteractivesApi.Services
{
    public interface ILiveSessionService
    {
        Task<Response<LiveSessionDetails>> Start(StartLiveSessionRequest req, ClaimsPrincipal principal);
        Task<Response<LiveSessionDetails>> Get(string code, ClaimsPrincipal principal);
        Task<Response<PresentationDetails>> GetPresentation(string code, ClaimsPrincipal principal);
        Task<Response<object>> End(string code, ClaimsPrincipal principal);
    }

    /// <summary>
    /// Démarrer, consulter et terminer une séance en direct. Démarrer et terminer sont réservés
    /// au créateur de la présentation ; tout utilisateur connecté peut rejoindre avec le code.
    /// </summary>
    public class LiveSessionService : ILiveSessionService
    {
        private readonly MainContext _context;
        private readonly LiveSessionStore _store;
        private readonly IHubContext<LiveSessionHub> _hub;

        public LiveSessionService(
            MainContext context,
            LiveSessionStore store,
            IHubContext<LiveSessionHub> hub
        )
        {
            _context = context;
            _store = store;
            _hub = hub;
        }

        public async Task<Response<LiveSessionDetails>> Start(
            StartLiveSessionRequest req,
            ClaimsPrincipal principal
        )
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<LiveSessionDetails>(401, "Vous n'êtes pas connecté.");

            var presentation = await _context.Presentations.FirstOrDefaultAsync(p =>
                p.Id == req.PresentationId && p.ArchivedAt == null
            );
            if (presentation is null)
                return Fail<LiveSessionDetails>(404, "Présentation introuvable.");
            if (presentation.CreatorId != user.Id)
                return Fail<LiveSessionDetails>(403, "Vous n'avez pas accès à cette présentation.");

            var session = _store.StartOrResume(presentation.Id, user.Id);
            return Ok(session.ToDetails(), "Séance démarrée.");
        }

        public Task<Response<LiveSessionDetails>> Get(string code, ClaimsPrincipal principal)
        {
            if (CheckUser.GetUserFromClaim(principal, _context) is null)
                return Task.FromResult(Fail<LiveSessionDetails>(401, "Vous n'êtes pas connecté."));

            var session = _store.Find(code);
            return Task.FromResult(
                session is null
                    ? Fail<LiveSessionDetails>(404, "Séance introuvable ou terminée.")
                    : Ok(session.ToDetails(), "Séance récupérée.")
            );
        }

        /// <summary>
        /// Contenu de la présentation pour un participant, tant que la séance est active. La
        /// bonne réponse des QCM en est retirée : elle n'est envoyée qu'à la révélation.
        /// </summary>
        public async Task<Response<PresentationDetails>> GetPresentation(
            string code,
            ClaimsPrincipal principal
        )
        {
            if (CheckUser.GetUserFromClaim(principal, _context) is null)
                return Fail<PresentationDetails>(401, "Vous n'êtes pas connecté.");

            var session = _store.Find(code);
            if (session is null)
                return Fail<PresentationDetails>(404, "Séance introuvable ou terminée.");

            var presentation = await _context
                .Presentations.Where(p => p.Id == session.PresentationId && p.ArchivedAt == null)
                .Include(p => p.Slides.Where(s => s.ArchivedAt == null).OrderBy(s => s.Order))
                .ThenInclude(s => s.Elements.Where(e => e.ArchivedAt == null))
                .AsNoTracking()
                .FirstOrDefaultAsync();
            if (presentation is null)
                return Fail<PresentationDetails>(404, "Présentation introuvable.");

            var details = new PresentationDetails(presentation);
            foreach (var element in details.Slides.SelectMany(s => s.Elements))
                if (element.Type == ElementTypeEnum.Quiz)
                    element.Configuration = WithoutCorrectAnswer(element.Configuration);
            return Ok(details, "Présentation récupérée.");
        }

        public async Task<Response<object>> End(string code, ClaimsPrincipal principal)
        {
            var user = CheckUser.GetUserFromClaim(principal, _context);
            if (user is null)
                return Fail<object>(401, "Vous n'êtes pas connecté.");

            var session = _store.Find(code);
            if (session is null)
                return Fail<object>(404, "Séance introuvable ou terminée.");
            if (session.HostUserId != user.Id)
                return Fail<object>(403, "Seul l'hôte peut terminer la séance.");

            _store.End(session);
            await _hub.Clients.Group(session.Code).SendAsync("SessionEnded");
            return new Response<object> { Status = 200, Message = "Séance terminée." };
        }

        internal static string WithoutCorrectAnswer(string configuration)
        {
            var node = JsonNode.Parse(configuration) as JsonObject;
            if (node is null)
                return "{}";
            node.Remove("correct");
            return node.ToJsonString();
        }

        private static Response<T> Ok<T>(T data, string message) =>
            new() { Status = 200, Message = message, Data = data };

        private static Response<T> Fail<T>(int status, string message) =>
            new() { Status = status, Message = message };
    }
}
