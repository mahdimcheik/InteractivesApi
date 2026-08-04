using InteractivesApi.Utilities;
using System.ComponentModel.DataAnnotations;

namespace InteractivesApi.Models.Presentations
{
    // ---- Requests: Presentation ----

    public class CreatePresentationRequest
    {
        [Required]
        [MaxLength(256)]
        public string Title { get; set; } = string.Empty;
    }

    public class UpdatePresentationRequest
    {
        [Required]
        [MaxLength(256)]
        public string Title { get; set; } = string.Empty;
    }

    // ---- Requests: Slide ----

    public class CreateSlideRequest
    {
        /// <summary>Null appends the slide at the end of the deck.</summary>
        public int? Order { get; set; }

        [MaxLength(16)]
        public string? BackgroundColor { get; set; }
    }

    public class UpdateSlideRequest
    {
        // Order is intentionally not editable here — use ReorderSlidesRequest instead, so a
        // stray UpdateSlide can't silently desync ordering from the rest of the deck.
        [MaxLength(16)]
        public string? BackgroundColor { get; set; }
    }

    public class ReorderSlidesRequest
    {
        [Required]
        public List<Guid> SlideIds { get; set; } = new();
    }

    // ---- Requests: Element ----

    public class CreateElementRequest
    {
        [Required]
        public ElementTypeEnum Type { get; set; }

        [Required]
        public double X { get; set; }

        [Required]
        public double Y { get; set; }

        [Required]
        public double Width { get; set; }

        [Required]
        public double Height { get; set; }

        public double Rotation { get; set; }

        /// <summary>Null stacks the element on top of the current highest ZIndex.</summary>
        public int? ZIndex { get; set; }

        public bool Locked { get; set; }
        public bool Visible { get; set; } = true;

        [Required]
        public string Configuration { get; set; } = "{}";
    }

    public class UpdateElementRequest
    {
        // Type is immutable after creation — Configuration's shape is tied to it.
        [Required]
        public double X { get; set; }

        [Required]
        public double Y { get; set; }

        [Required]
        public double Width { get; set; }

        [Required]
        public double Height { get; set; }

        public double Rotation { get; set; }
        public int ZIndex { get; set; }
        public bool Locked { get; set; }
        public bool Visible { get; set; }

        [Required]
        public string Configuration { get; set; } = "{}";
    }

    // ---- Responses ----

    public class PresentationSummary
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public Guid CreatorId { get; set; }
        public int SlideCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public PresentationSummary() { }

        public PresentationSummary(Presentation presentation, int slideCount)
        {
            Id = presentation.Id;
            Title = presentation.Title;
            CreatorId = presentation.CreatorId;
            SlideCount = slideCount;
            CreatedAt = presentation.CreatedAt;
            UpdatedAt = presentation.UpdatedAt;
        }
    }

    public class PresentationDetails
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public Guid CreatorId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public PresentationDetails() { }

        public PresentationDetails(Presentation presentation)
        {
            Id = presentation.Id;
            Title = presentation.Title;
            CreatorId = presentation.CreatorId;
            CreatedAt = presentation.CreatedAt;
            UpdatedAt = presentation.UpdatedAt;
        }
    }

    public class SlideDetails
    {
        public Guid Id { get; set; }
        public Guid PresentationId { get; set; }
        public int Order { get; set; }
        public string? BackgroundColor { get; set; }
        public int ElementCount { get; set; }

        public SlideDetails() { }

        public SlideDetails(Slide slide, int elementCount)
        {
            Id = slide.Id;
            PresentationId = slide.PresentationId;
            Order = slide.Order;
            BackgroundColor = slide.BackgroundColor;
            ElementCount = elementCount;
        }
    }

    public class ElementDetails
    {
        public Guid Id { get; set; }
        public Guid SlideId { get; set; }
        public ElementTypeEnum Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public int ZIndex { get; set; }
        public bool Locked { get; set; }
        public bool Visible { get; set; }
        public string Configuration { get; set; } = "{}";
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        public ElementDetails() { }

        public ElementDetails(Element element)
        {
            Id = element.Id;
            SlideId = element.SlideId;
            Type = element.Type;
            X = element.X;
            Y = element.Y;
            Width = element.Width;
            Height = element.Height;
            Rotation = element.Rotation;
            ZIndex = element.ZIndex;
            Locked = element.Locked;
            Visible = element.Visible;
            Configuration = element.Configuration;
            CreatedAt = element.CreatedAt;
            UpdatedAt = element.UpdatedAt;
        }
    }
}
