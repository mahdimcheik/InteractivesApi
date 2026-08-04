using InteractivesApi.Models.Interfaces;
using InteractivesApi.Models.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteractivesApi.Models.Presentations
{
    public class Presentation : BaseModel
    {
        [Required]
        [MaxLength(256)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public Guid CreatorId { get; set; }

        [ForeignKey(nameof(CreatorId))]
        public UserApp? Creator { get; set; }

        public ICollection<Slide> Slides { get; set; } = new List<Slide>();
    }
}
