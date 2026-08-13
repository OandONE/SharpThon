# SharpThon VS Code extension

This extension registers `*.spy` files as SharpThon documents and starts the
`SharpThon.LSP` server over standard input/output. Hover information and
published diagnostics are provided by the language server.

## Development

From this directory, install dependencies and compile the extension:

```sh
npm install
npm run compile
```

Open the repository root in VS Code, then launch **Run SharpThon Extension**
from the Run and Debug view. The Extension Development Host opens with the
repository as its workspace. Open a `.spy` file to activate the extension.

By default the extension runs the adjacent server project with:

```sh
dotnet run --project ../sharpton_cs/SharpThon.LSP/SharpThon.LSP.csproj --no-launch-profile
```

For a prebuilt server, set `sharpthon.server.path` to either the absolute path
of `SharpThon.LSP.dll` (executed by `dotnet`) or a server executable. Relative
paths are resolved from the first workspace folder.
