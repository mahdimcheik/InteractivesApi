using InteractivesApi.Models.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteractivesApi.Models.Presentations
{
    public class Slide : BaseModel
    {
        [Required]
        public Guid PresentationId { get; set; }

        [ForeignKey(nameof(PresentationId))]
        public Presentation? Presentation { get; set; }

        public int Order { get; set; }

        [MaxLength(16)]
        public string? BackgroundColor { get; set; }

        public ICollection<Element> Elements { get; set; } = new List<Element>();
    }
}
