using System.Text.Json;
using InteractivesApi.Contexts;
using InteractivesApi.Models.LiveSessions;
using InteractivesApi.Models.Users;
using InteractivesApi.Services;
using InteractivesApi.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InteractivesApi.Hubs
{
    /// <summary>
    /// Diffusion d'une séance en direct. Groupe <c>{code}</c> : tous les connectés ;
    /// groupe <c>host-{code}</c> : l'hôte seul. Événements envoyés aux clients :
    /// <c>SlideChanged(int)</c>, <c>SessionEnded()</c>, <c>QuizStateChanged(ParticipantQuizState)</c>
    /// (sans réponse personnelle ; répartition et bonne réponse seulement après révélation) ;
    /// à l'hôte seul : <c>ParticipantCountChanged(int)</c>, <c>QuizResultsChanged(HostQuizState)</c>
    /// (réponses nominatives).
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

        public async Task<HostJoinResult> JoinAsHost(string code)
        {
            var session = RequireHostSession(code);
            await Groups.AddToGroupAsync(Context.ConnectionId, session.Code);
            await Groups.AddToGroupAsync(Context.ConnectionId, HostGroup(session.Code));
            return new HostJoinResult
            {
                ParticipantCount = session.ParticipantCount,
                Quizzes = session.MapQuizzes(q => q.ForHost()),
            };
        }

        /// <returns>La diapositive courante et l'état des QCM déjà lancés, réponses du participant comprises.</returns>
        public async Task<ParticipantJoinResult> Join(string code)
        {
            var session = RequireSession(code);
            var user = RequireUser();

            await Groups.AddToGroupAsync(Context.ConnectionId, session.Code);
            if (user.Id != session.HostUserId)
            {
                var count = session.AddParticipant(Context.ConnectionId, user.Id);
                await Clients
                    .Group(HostGroup(session.Code))
                    .SendAsync("ParticipantCountChanged", count);
            }
            return new ParticipantJoinResult
            {
                CurrentSlideIndex = session.CurrentSlideIndex,
                Quizzes = session.MapQuizzes(q => q.ForParticipant(user.Id)),
            };
        }

        public async Task GoToSlide(string code, int index)
        {
            var session = RequireHostSession(code);
            session.GoToSlide(index);
            await Clients.Group(session.Code).SendAsync("SlideChanged", session.CurrentSlideIndex);
        }

        public async Task OpenQuiz(string code, Guid elementId)
        {
            var session = RequireHostSession(code);
            var (answerCount, correct) = await LoadQuiz(session, elementId);
            await Transition(
                session,
                elementId,
                run =>
                {
                    if (run.Status == QuizStatusEnum.Revealed)
                        throw new HubException("La réponse a déjà été révélée.");
                    run.Status = QuizStatusEnum.Open;
                },
                () => new QuizRun { ElementId = elementId, AnswerCount = answerCount, Correct = correct }
            );
        }

        public async Task CloseQuiz(string code, Guid elementId)
        {
            var session = RequireHostSession(code);
            await Transition(
                session,
                elementId,
                run =>
                {
                    if (run.Status == QuizStatusEnum.Open)
                        run.Status = QuizStatusEnum.Closed;
                }
            );
        }

        /// <summary>Révèle la bonne réponse ; un vote encore ouvert est fermé.</summary>
        public async Task RevealQuiz(string code, Guid elementId)
        {
            var session = RequireHostSession(code);
            await Transition(session, elementId, run => run.Status = QuizStatusEnum.Revealed);
        }

        public async Task AnswerQuiz(string code, Guid elementId, int answer)
        {
            var session = RequireSession(code);
            var user = RequireUser();
            if (user.Id == session.HostUserId)
                throw new HubException("L'hôte ne répond pas à ses propres QCM.");

            var name = $"{user.FirstName} {user.LastName}".Trim();
            var hostState =
                session.WithExistingQuiz(
                    elementId,
                    run =>
                    {
                        if (run.Status != QuizStatusEnum.Open)
                            throw new HubException("Le vote est fermé.");
                        if (answer < 0 || answer >= run.AnswerCount)
                            throw new HubException("Réponse invalide.");
                        run.Responses[user.Id] = (answer, name.Length > 0 ? name : user.Email ?? "?");
                        return run.ForHost();
                    }
                ) ?? throw new HubException("Ce QCM n'est pas ouvert.");

            await Clients.Group(HostGroup(session.Code)).SendAsync("QuizResultsChanged", hostState);
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

        private async Task Transition(
            LiveSession session,
            Guid elementId,
            Action<QuizRun> change,
            Func<QuizRun>? create = null
        )
        {
            var states = create is null
                ? session.WithExistingQuiz(elementId, Apply)
                : session.WithQuiz(elementId, create, Apply);
            if (states is null)
                throw new HubException("Ce QCM n'a pas encore été ouvert.");

            await Clients.Group(session.Code).SendAsync("QuizStateChanged", states.Participants);
            await Clients.Group(HostGroup(session.Code)).SendAsync("QuizResultsChanged", states.Host);

            QuizStates Apply(QuizRun run)
            {
                change(run);
                // Diffusé à tout le groupe : aucune réponse personnelle (Guid.Empty ne répond jamais).
                return new QuizStates(run.ForHost(), run.ForParticipant(Guid.Empty));
            }
        }

        private sealed record QuizStates(HostQuizState Host, ParticipantQuizState Participants);

        /// <summary>Lit le QCM en base et vérifie qu'il appartient à la présentation de la séance.</summary>
        private async Task<(int AnswerCount, int Correct)> LoadQuiz(LiveSession session, Guid elementId)
        {
            var element = await _context
                .Elements.Include(e => e.Slide)
                .FirstOrDefaultAsync(e =>
                    e.Id == elementId
                    && e.ArchivedAt == null
                    && e.Type == ElementTypeEnum.Quiz
                    && e.Slide!.PresentationId == session.PresentationId
                );
            if (element is null)
                throw new HubException("QCM introuvable dans cette présentation.");

            using var doc = JsonDocument.Parse(element.Configuration);
            var root = doc.RootElement;
            return (root.GetProperty("answers").GetArrayLength(), root.GetProperty("correct").GetInt32());
        }

        private LiveSession RequireSession(string code) =>
            _store.Find(code) ?? throw new HubException("Séance introuvable ou terminée.");

        private LiveSession RequireHostSession(string code)
        {
            var session = RequireSession(code);
            if (session.HostUserId != RequireUser().Id)
                throw new HubException("Seul l'hôte peut piloter cette séance.");
            return session;
        }

        private UserApp RequireUser() =>
            CheckUser.GetUserFromClaim(Context.User!, _context)
            ?? throw new HubException("Vous n'êtes pas connecté.");
    }
}
