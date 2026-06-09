using System;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

namespace Vantuz.Plugins.Test;

/// <summary>
/// Mock game launch plugin for headless/GUI recidivism testing.
/// Sets gameCommand/gameArgs/gameWorkDir for downstream OS.ExecuteCommand without real game logic.
/// Per INVARIANT_THEORY.md В§1.2 Measurability: deterministic, no external processes.
/// </summary>
public class MockGameLaunchCommand : ICommandPlugin
{
    public string Name => "Test.MockGameLaunch";
/// F_doc: {ExecuteAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ExecuteAsync behavior

    public Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        string command = stepConfig.TryGetProperty("command", out var cmd)
            ? cmd.GetString() ?? "cmd.exe"
            : "cmd.exe";

        string arguments = stepConfig.TryGetProperty("arguments", out var args)
            ? args.GetString() ?? "/c echo mock-launch"
            : "/c echo mock-launch";

        string workDir = stepConfig.TryGetProperty("workDir", out var wd)
            ? wd.GetString() ?? AppContext.BaseDirectory
            : AppContext.BaseDirectory;

        context.Set("gameCommand", command);
        context.Set("gameArgs", arguments);
        context.Set("gameWorkDir", workDir);

        context.Reporter.ReportState($"[MOCK] Game launch simulated: {command} {arguments}");
        return Task.FromResult(new CommandResult(true));
    }
/// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
