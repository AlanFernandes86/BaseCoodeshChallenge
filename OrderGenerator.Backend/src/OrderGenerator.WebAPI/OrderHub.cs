using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class OrderHub : Hub
{
    public async Task SubmitOrder(string symbol, string side, int quantity, decimal price)
    {
        // Placeholder: will be implemented in PHASE 3
        await Clients.Caller.SendAsync("OrderResult", new 
        { 
            success = false, 
            message = "Not yet implemented" 
        });
    }
}
