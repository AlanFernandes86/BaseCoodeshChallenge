namespace OrderAccumulator.Core.Models
{
    /// <summary>
    /// Result of processing an order: accepted or rejected with reason
    /// </summary>
    public class ProcessingResult
    {
        public bool Accepted { get; set; }
        public string OrderID { get; set; } = string.Empty;  // Unique order ID assigned by acceptor
        public string? RejectReason { get; set; }  // Populated only if rejected

        // Calculated exposure after this order (for logging/tracking)
        public decimal ResultingExposure { get; set; }

        /// <summary>
        /// Creates a successful processing result
        /// </summary>
        public static ProcessingResult CreateAccepted(string orderID, decimal resultingExposure)
        {
            return new ProcessingResult
            {
                Accepted = true,
                OrderID = orderID,
                RejectReason = null,
                ResultingExposure = resultingExposure
            };
        }

        /// <summary>
        /// Creates a rejection result
        /// </summary>
        public static ProcessingResult CreateRejected(string rejectReason, decimal currentExposure)
        {
            return new ProcessingResult
            {
                Accepted = false,
                OrderID = string.Empty,
                RejectReason = rejectReason,
                ResultingExposure = currentExposure
            };
        }

        public override string ToString() =>
            Accepted
                ? $"ProcessingResult {{ Accepted=true, OrderID={OrderID}, Exposure={ResultingExposure} }}"
                : $"ProcessingResult {{ Accepted=false, RejectReason={RejectReason}, Exposure={ResultingExposure} }}";
    }
}
