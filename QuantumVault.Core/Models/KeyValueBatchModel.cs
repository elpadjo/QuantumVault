using System.ComponentModel.DataAnnotations;

namespace QuantumVault.Core.Models
{
    public class KeyValueBatchModel
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one key-value pair is required.")]
        public Dictionary<string, string> KeyValues { get; set; } = new();
    }
}
