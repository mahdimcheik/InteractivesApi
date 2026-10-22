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
