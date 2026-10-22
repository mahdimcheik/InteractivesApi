using System.Collections.Concurrent;
using System.Security.Cryptography;
using InteractivesApi.Models.LiveSessions;

namespace InteractivesApi.Services
{
    /// <summary>
    /// Séance en direct en cours. Rien n'est conservé après la séance : l'état vit en mémoire
    /// (singleton), et une seule séance est active par présentation.
    /// </summary>
    public class LiveSession
    {
        private readonly object _lock = new();
        // connectionId → userId : un participant ouvert dans deux onglets ne compte qu'une fois.
        private readonly Dictionary<string, Guid> _participants = new();

        public required string Code { get; init; }
        public required Guid PresentationId { get; init; }
        public required Guid HostUserId { get; init; }
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public int CurrentSlideIndex { get; private set; }

        public int ParticipantCount
        {
            get
            {
                lock (_lock)
                    return _participants.Values.Distinct().Count();
            }
        }

        public void GoToSlide(int index)
        {
            lock (_lock)
                CurrentSlideIndex = Math.Max(index, 0);
        }

        public int AddParticipant(string connectionId, Guid userId)
        {
            lock (_lock)
            {
                _participants[connectionId] = userId;
                return _participants.Values.Distinct().Count();
            }
        }

        /// <returns>Le nouveau nombre de participants, ou null si la connexion n'en était pas un.</returns>
        public int? RemoveParticipant(string connectionId)
        {
            lock (_lock)
            {
                return _participants.Remove(connectionId)
                    ? _participants.Values.Distinct().Count()
                    : null;
            }
        }

        public LiveSessionDetails ToDetails() =>
            new()
            {
                Code = Code,
                PresentationId = PresentationId,
                CurrentSlideIndex = CurrentSlideIndex,
                ParticipantCount = ParticipantCount,
                StartedAt = StartedAt,
            };
    }

    public class LiveSessionStore
    {
        // Sans caractères ambigus à la dictée ou à l'écran (0/O, 1/I/L).
        private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const int CodeLength = 6;

        private readonly ConcurrentDictionary<string, LiveSession> _byCode = new();
        private readonly ConcurrentDictionary<Guid, string> _codeByPresentation = new();
        private readonly object _startLock = new();

        public LiveSession? Find(string code) =>
            _byCode.TryGetValue(code.Trim().ToUpperInvariant(), out var session) ? session : null;

        /// <summary>Reprend la séance active de la présentation, ou en démarre une.</summary>
        public LiveSession StartOrResume(Guid presentationId, Guid hostUserId)
        {
            lock (_startLock)
            {
                if (
                    _codeByPresentation.TryGetValue(presentationId, out var existingCode)
                    && _byCode.TryGetValue(existingCode, out var existing)
                )
                    return existing;

                string code;
                do
                    code = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
                while (_byCode.ContainsKey(code));

                var session = new LiveSession
                {
                    Code = code,
                    PresentationId = presentationId,
                    HostUserId = hostUserId,
                };
                _byCode[code] = session;
                _codeByPresentation[presentationId] = code;
                return session;
            }
        }

        public void End(LiveSession session)
        {
            lock (_startLock)
            {
                _byCode.TryRemove(session.Code, out _);
                _codeByPresentation.TryRemove(session.PresentationId, out _);
            }
        }

        /// <summary>Toutes les séances en cours.</summary>
        public IEnumerable<LiveSession> All() => _byCode.Values;
    }
}
