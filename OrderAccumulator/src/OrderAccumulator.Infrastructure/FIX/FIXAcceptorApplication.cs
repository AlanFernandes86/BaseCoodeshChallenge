using System;
using QuickFix;
using OrderAccumulator.Core;

namespace OrderAccumulator.Infrastructure.FIX;

/// <summary>
/// Placeholder FIX Acceptor application.
/// Implements IApplication interface but defers message processing logic.
/// Will implement full processing once QuickFIXn.Core 1.14.1 API is researched.
/// </summary>
public class FIXAcceptorApplication : IApplication
{
    private readonly ExposureState _exposureState;

    public FIXAcceptorApplication(ExposureState exposureState)
    {
        _exposureState = exposureState ?? throw new ArgumentNullException(nameof(exposureState));
    }

    public void OnCreate(SessionID sessionID)
    {
        Console.WriteLine($"[FIX] Session created: {sessionID}");
    }

    public void OnLogon(SessionID sessionID)
    {
        Console.WriteLine($"[FIX] Logon: {sessionID} - Connection established");
    }

    public void OnLogout(SessionID sessionID)
    {
        Console.WriteLine($"[FIX] Logout: {sessionID} - Connection closed");
    }

    public void FromAdmin(Message msg, SessionID sessionID)
    {
        Console.WriteLine($"[FIX] Admin message from {sessionID}");
    }

    public void FromApp(Message msg, SessionID sessionID)
    {
        Console.WriteLine($"[FIX] App message from {sessionID} - processing deferred");
        // Full implementation deferred until API research complete
    }

    public void ToApp(Message msg, SessionID sessionID)
    {
        Console.WriteLine($"[FIX] Sending message to {sessionID}");
    }

    public void ToAdmin(Message msg, SessionID sessionID)
    {
        // Admin message being sent
    }
}
