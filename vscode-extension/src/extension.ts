import * as path from 'node:path';
import * as vscode from 'vscode';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

export function activate(context: vscode.ExtensionContext): void {
    const serverOptions = createServerOptions(context);
    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'sharpthon', pattern: '**/*.spy' }],
        synchronize: {
            configurationSection: 'sharpthon'
        }
    };

    client = new LanguageClient(
        'sharpthonLanguageServer',
        'SharpThon Language Server',
        serverOptions,
        clientOptions
    );

    void client.start();
    context.subscriptions.push({
        dispose: () => {
            void client?.stop();
        }
    });
}

export async function deactivate(): Promise<void> {
    await client?.stop();
}

function createServerOptions(context: vscode.ExtensionContext): ServerOptions {
    const configuredPath = vscode.workspace
        .getConfiguration('sharpthon.server')
        .get<string>('path', '')
        .trim();

    if (configuredPath) {
        const serverPath = resolvePath(configuredPath);
        if (serverPath.endsWith('.dll')) {
            return {
                command: 'dotnet',
                args: [serverPath],
                transport: TransportKind.stdio
            };
        }

        return {
            command: serverPath,
            transport: TransportKind.stdio
        };
    }

    const projectPath = context.asAbsolutePath(
        path.join('..', 'sharpton_cs', 'SharpThon.LSP', 'SharpThon.LSP.csproj')
    );

    return {
        command: 'dotnet',
        args: ['run', '--project', projectPath, '--no-launch-profile'],
        transport: TransportKind.stdio
    };
}

function resolvePath(configuredPath: string): string {
    if (path.isAbsolute(configuredPath)) {
        return configuredPath;
    }

    const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
    return workspaceFolder
        ? path.join(workspaceFolder.uri.fsPath, configuredPath)
        : configuredPath;
}
