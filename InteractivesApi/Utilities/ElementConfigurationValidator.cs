using System.Text.Json;

namespace InteractivesApi.Utilities
{
    /// <summary>
    /// Presence/shape validation for <c>Element.Configuration</c>, keyed by <see cref="ElementTypeEnum"/>.
    /// Intentionally minimal — adding a new element type means adding one more switch arm here,
    /// not a schema-registry/plugin system.
    /// </summary>
    public static class ElementConfigurationValidator
    {
        /// <returns>null si valide ; sinon un message d'erreur en français.</returns>
        public static string? Validate(ElementTypeEnum type, string configurationJson)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(configurationJson);
            }
            catch (JsonException)
            {
                return "La configuration doit être un JSON valide.";
            }

            var root = doc.RootElement;
            return type switch
            {
                ElementTypeEnum.Text => root.TryGetProperty("content", out _)
                    ? null
                    : "Le texte doit contenir un champ 'content'.",
                ElementTypeEnum.Image => root.TryGetProperty("url", out _)
                    ? null
                    : "L'image doit contenir un champ 'url'.",
                ElementTypeEnum.Quiz => ValidateQuiz(root),
                ElementTypeEnum.TextImageTitle => ValidateTextImageTitle(root),
                _ => "Type d'élément inconnu.",
            };
        }

        private static string? ValidateTextImageTitle(JsonElement root)
        {
            if (!root.TryGetProperty("title", out _) || !root.TryGetProperty("body", out _))
                return "L'élément titre + texte + image doit contenir 'title' et 'body'.";

            if (!root.TryGetProperty("image", out var image)
                || image.ValueKind != JsonValueKind.Object
                || !image.TryGetProperty("url", out _))
                return "L'élément titre + texte + image doit contenir un objet 'image' avec 'url'.";

            return null;
        }

        private static string? ValidateQuiz(JsonElement root)
        {
            if (!root.TryGetProperty("question", out _))
                return "Le quiz doit contenir une 'question'.";

            if (!root.TryGetProperty("answers", out var answers)
                || answers.ValueKind != JsonValueKind.Array
                || answers.GetArrayLength() < 2)
                return "Le quiz doit contenir au moins deux réponses.";

            if (!root.TryGetProperty("correct", out var correct)
                || correct.ValueKind != JsonValueKind.Number
                || correct.GetInt32() < 0
                || correct.GetInt32() >= answers.GetArrayLength())
                return "L'index de la bonne réponse est invalide.";

            return null;
        }
    }
}
