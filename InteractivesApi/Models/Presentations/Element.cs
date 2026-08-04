using InteractivesApi.Models.Interfaces;
using InteractivesApi.Utilities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InteractivesApi.Models.Presentations
{
    public class Element : BaseModel
    {
        [Required]
        public Guid SlideId { get; set; }

        [ForeignKey(nameof(SlideId))]
        public Slide? Slide { get; set; }

        [Required]
        public ElementTypeEnum Type { get; set; }

        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public int ZIndex { get; set; }
        public bool Locked { get; set; }
        public bool Visible { get; set; } = true;

        /// <summary>Raw JSON, shape depends on <see cref="Type"/>. Validated by the service, not EF.</summary>
        [Required]
        [Column(TypeName = "jsonb")]
        public string Configuration { get; set; } = "{}";
    }
}
