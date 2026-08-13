using StreamJsonRpc;

namespace SharpThon.LSP;

internal static class Program
{
    public static async Task Main()
    {
        // stdout is reserved exclusively for LSP messages.
        var sendingStream = Console.OpenStandardOutput();
        var receivingStream = Console.OpenStandardInput();
        var server = new SharpThonLanguageServer();

        using var rpc = new JsonRpc(sendingStream, receivingStream, server);
        server.Attach(rpc);
        rpc.StartListening();
        await rpc.Completion;
    }
}
