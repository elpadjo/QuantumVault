using QuantumVault.Core.Enums;

namespace QuantumVault.Core.Models
{
    public class KeyValueRequestModel
    {
        public RequestPriority Priority { get; }
        public Func<Task> Execute { get; } // Encapsulates the logic to execute the request

        public KeyValueRequestModel(RequestPriority priority, Func<Task> execute)
        {
            Priority = priority;
            Execute = execute;
        }
    }
}
