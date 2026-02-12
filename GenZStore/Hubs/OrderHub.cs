using Microsoft.AspNetCore.SignalR;

namespace GenZStore.Hubs
{
    // This class handles the connections
    public class OrderHub : Hub
    {
        // We can add specific methods here if the client needs to talk to the server,
        // but mostly we will use the Context to send messages OUT.
    }
}