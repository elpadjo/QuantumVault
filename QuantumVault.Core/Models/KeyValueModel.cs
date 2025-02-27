using System.ComponentModel.DataAnnotations;

namespace QuantumVault.Core.Models
{
    public class KeyValueModel
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;
    }
}
