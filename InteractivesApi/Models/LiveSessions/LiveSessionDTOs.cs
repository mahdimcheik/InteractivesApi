using System.ComponentModel.DataAnnotations;

namespace InteractivesApi.Models.LiveSessions
{
    public class StartLiveSessionRequest
    {
        [Required]
        public Guid PresentationId { get; set; }
    }

    public class LiveSessionDetails
    {
        [Required]
        public string Code { get; set; } = string.Empty;

        [Required]
        public Guid PresentationId { get; set; }

        [Required]
        public int CurrentSlideIndex { get; set; }

        [Required]
        public int ParticipantCount { get; set; }

        [Required]
        public DateTimeOffset StartedAt { get; set; }
    }
}

namespace InteractivesApi.Models.LiveSessions
{
    /// <summary>Déroulé d'un QCM pendant la séance.</summary>
    public enum QuizStatusEnum
    {
        Pending,
        Open,
        Closed,
        Revealed,
    }

    /// <summary>
    /// État d'un QCM vu par un participant. <see cref="Counts"/> et <see cref="Correct"/> ne sont
    /// remplis qu'après la révélation : la bonne réponse ne quitte jamais le serveur avant.
    /// </summary>
    public class ParticipantQuizState
    {
        public Guid ElementId { get; set; }
        public QuizStatusEnum Status { get; set; }
        public int? MyAnswer { get; set; }
        public List<int>? Counts { get; set; }
        public int? Correct { get; set; }
    }

    public class QuizResponse
    {
        public string ParticipantName { get; set; } = string.Empty;
        public int Answer { get; set; }
    }

    /// <summary>État d'un QCM vu par l'hôte : répartition et réponses nominatives en continu.</summary>
    public class HostQuizState
    {
        public Guid ElementId { get; set; }
        public QuizStatusEnum Status { get; set; }
        public List<int> Counts { get; set; } = new();
        public int? Correct { get; set; }
        public List<QuizResponse> Responses { get; set; } = new();
    }

    public class ParticipantJoinResult
    {
        public int CurrentSlideIndex { get; set; }
        public List<ParticipantQuizState> Quizzes { get; set; } = new();
    }

    public class HostJoinResult
    {
        public int ParticipantCount { get; set; }
        public List<HostQuizState> Quizzes { get; set; } = new();
    }
}
