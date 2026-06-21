namespace OrderAccumulator.Core.Models
{
    /// <summary>
    /// Result of processing an order: accepted or rejected with reason
    /// </summary>
    public class ProcessingResult
    {
        public bool Accepted { get; set; }
        public string OrderID { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
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
        public static ProcessingResult CreateRejected(string orderID, string rejectReason, decimal currentExposure)
        {
            return new ProcessingResult
            {
                Accepted = false,
                OrderID = orderID,
                RejectReason = rejectReason,
                ResultingExposure = currentExposure
            };
        }
    }
}
