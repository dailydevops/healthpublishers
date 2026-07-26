namespace NetEvolve.HealthPublishers.Tests.Integration.Internals;

using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        // Set all tests to use the same culture
        // This is necessary to ensure consistent results across different environments
        var cultureInfo = CultureInfo.CreateSpecificCulture("en-US");
        Thread.CurrentThread.CurrentCulture = cultureInfo;
        Thread.CurrentThread.CurrentUICulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

        VerifierSettings.SortPropertiesAlphabetically();
        VerifierSettings.SortJsonObjects();

        VerifierSettings.AutoVerify(includeBuildServer: false, throwException: true);

        Verifier.DerivePathInfo(
            (_, projectDirectory, type, method) =>
            {
                var directory = Path.Combine(projectDirectory, "_snapshots");
                var createdDirectory = Directory.CreateDirectory(directory);
                return new(createdDirectory.FullName, CleanTypeName(type), CleanMethodName(method.Name));
            }
        );
    }

    private static string CleanTypeName(Type type) =>
        type.Name.Replace("Tests", string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string CleanMethodName(string methodName) =>
        methodName.Replace("Async", string.Empty, StringComparison.OrdinalIgnoreCase);
}
