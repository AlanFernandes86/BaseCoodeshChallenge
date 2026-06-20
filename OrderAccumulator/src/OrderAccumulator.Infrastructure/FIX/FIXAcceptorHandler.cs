using System;
using OrderAccumulator.Core;
using OrderAccumulator.Core.Models;
using QuickFix;

namespace OrderAccumulator.Infrastructure.FIX
{
    /// <summary>
    /// Simple FIX message handler stub for bootstrap validation.
    /// Full FIX integration (NewOrderSingle parsing + ExecutionReport building) deferred to PHASE 3.
    /// </summary>
    public class FIXAcceptorHandler
    {
        private readonly ExposureState _exposureState;

        public FIXAcceptorHandler(ExposureState exposureState)
        {
            _exposureState = exposureState ?? throw new ArgumentNullException(nameof(exposureState));
        }

        public void ProcessMessage(string messageType)
        {
            Console.WriteLine($"[FIXAcceptorHandler] Processing message type: {messageType}");
        }

        public ExposureState GetExposureState() => _exposureState;
    }
}
