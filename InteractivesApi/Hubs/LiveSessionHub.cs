using InteractivesApi.Contexts;
using InteractivesApi.Services;
using InteractivesApi.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InteractivesApi.Hubs
{
    /// <summary>
    /// Diffusion d'une séance en direct. Groupe <c>{code}</c> : tous les connectés ;
    /// groupe <c>host-{code}</c> : l'hôte seul. Événements envoyés aux clients :
    /// <c>SlideChanged(int)</c>, <c>ParticipantCountChanged(int)</c>, <c>SessionEnded()</c>.
    /// </summary>
    [Authorize]
    public class LiveSessionHub : Hub
    {
        public const string Path = "/hubs/live-session";

        private readonly LiveSessionStore _store;
        private readonly MainContext _context;

        public LiveSessionHub(LiveSessionStore store, MainContext context)
        {
            _store = store;
            _context = context;
        }

        public static string HostGroup(string code) => $"host-{code}";

        public async Task<int> JoinAsHost(string code)
        {
            var session = RequireSession(code);
            if (session.HostUserId != RequireUserId())
                throw new HubException("Seul l'hôte peut piloter cette séance.");

            await Groups.AddToGroupAsync(Context.ConnectionId, session.Code);
            await Groups.AddToGroupAsync(Context.ConnectionId, HostGroup(session.Code));
            return session.ParticipantCount;
        }

        /// <returns>L'index de la diapositive courante.</returns>
        public async Task<int> Join(string code)
        {
            var session = RequireSession(code);
            var userId = RequireUserId();

            await Groups.AddToGroupAsync(Context.ConnectionId, session.Code);
            if (userId != session.HostUserId)
            {
                var count = session.AddParticipant(Context.ConnectionId, userId);
                await Clients
                    .Group(HostGroup(session.Code))
                    .SendAsync("ParticipantCountChanged", count);
            }
            return session.CurrentSlideIndex;
        }

        public async Task GoToSlide(string code, int index)
        {
            var session = RequireSession(code);
            if (session.HostUserId != RequireUserId())
                throw new HubException("Seul l'hôte peut changer de diapositive.");

            session.GoToSlide(index);
            await Clients.Group(session.Code).SendAsync("SlideChanged", session.CurrentSlideIndex);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            foreach (var session in _store.All())
            {
                var count = session.RemoveParticipant(Context.ConnectionId);
                if (count is not null)
                    await Clients
                        .Group(HostGroup(session.Code))
                        .SendAsync("ParticipantCountChanged", count.Value);
            }
            await base.OnDisconnectedAsync(exception);
        }

        private LiveSession RequireSession(string code) =>
            _store.Find(code) ?? throw new HubException("Séance introuvable ou terminée.");

        private Guid RequireUserId() =>
            CheckUser.GetUserFromClaim(Context.User!, _context)?.Id
            ?? throw new HubException("Vous n'êtes pas connecté.");
    }
}
