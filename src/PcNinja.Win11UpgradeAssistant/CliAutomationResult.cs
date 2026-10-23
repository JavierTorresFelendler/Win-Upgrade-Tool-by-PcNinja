using System.Text.Json;

namespace PcNinja.Win11UpgradeAssistant;

internal static class CliAutomationResult
{
    internal static Dictionary<string, object?> Create(object details, string resultCode,
        int exitCode, string message, bool readyForRestart)
    {
        var payload = JsonSerializer.SerializeToElement(details).EnumerateObject()
            .ToDictionary(property => property.Name, property => (object?)property.Value.Clone());
        payload["schemaVersion"] = 1;
        payload["success"] = exitCode == 0;
        payload["exitCode"] = exitCode;
        payload["resultCode"] = resultCode;
        payload["message"] = message;
        // Read-only checks/dry runs and every failure must never authorize reboot.
        payload["readyForRestart"] = exitCode == 0 &&
            resultCode == "UPGRADE_PREPARED_RESTART_REQUIRED" && readyForRestart;
        payload["logPath"] = PcNinja.Win11Upgrade.Core.AppLog.Path;
        payload["version"] = ProductIdentity.Version;
        payload["build"] = ProductIdentity.BuildVersion;
        return payload;
    }
}
