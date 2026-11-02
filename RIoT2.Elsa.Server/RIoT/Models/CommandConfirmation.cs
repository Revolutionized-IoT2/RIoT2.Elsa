namespace RIoT2.Elsa.Server.RIoT.Models
{
    /// <summary>
    /// The orchestrator's answer to a command sent with <c>"wait": true</c> (<c>POST /api/v2/commands</c>, design 7.1).
    /// </summary>
    public class CommandConfirmation
    {
        public string? CorrelationId { get; set; }
        public string? CommandId { get; set; }
        public string? NodeId { get; set; }

        /// <summary>
        /// <c>executed</c>, <c>duplicate</c> or <c>sent</c> (a node that doesn't report results), or <c>failed</c>,
        /// <c>unknownCommand</c>, <c>expired</c>; <c>pending</c> if the wait ended before a result.
        /// </summary>
        public string Status { get; set; } = "pending";

        /// <summary>Why the command failed, if it did.</summary>
        public string? Error { get; set; }

        public int Attempts { get; set; }

        /// <summary>The device ran the command, or a node that doesn't report results was sent it.</summary>
        public bool Succeeded => Status is "executed" or "duplicate" or "sent";
    }
}
