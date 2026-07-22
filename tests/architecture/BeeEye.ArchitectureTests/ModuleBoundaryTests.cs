using System.Reflection;
using BeeEye.Shared.Modularity;
using Xunit;

namespace BeeEye.ArchitectureTests;

/// <summary>
/// Enforces the modular-monolith boundary rules described in
/// <c>docs/architecture/module-boundaries.md</c>. These run against the compiled
/// assemblies in the test output directory.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private const string ModulePrefix = "BeeEye.Modules.";
    private const string SharedKernelAssembly = "BeeEye.Shared";

    private static IReadOnlyList<Assembly> ModuleAssemblies()
    {
        var dir = AppContext.BaseDirectory;
        return Directory.GetFiles(dir, $"{ModulePrefix}*.dll")
            .Select(Assembly.LoadFrom)
            .Where(a => a.GetName().Name?.StartsWith(ModulePrefix, StringComparison.Ordinal) == true)
            .ToList();
    }

    [Fact]
    public void All_nineteen_modules_are_present()
    {
        // Guards against a module silently dropping out of the composition/build.
        Assert.Equal(19, ModuleAssemblies().Count);
    }

    [Fact]
    public void Modules_do_not_reference_other_modules()
    {
        var violations = new List<string>();

        foreach (var module in ModuleAssemblies())
        {
            var self = module.GetName().Name;
            var offending = module.GetReferencedAssemblies()
                .Select(r => r.Name)
                .Where(name => name is not null
                    && name.StartsWith(ModulePrefix, StringComparison.Ordinal)
                    && name != self);

            foreach (var referenced in offending)
            {
                violations.Add($"{self} -> {referenced}");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Modules must not reference one another. Violations:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void Pure_shared_kernel_does_not_depend_on_aspnetcore()
    {
        // BeeEye.Shared must stay framework-free so every host can reference it cheaply.
        // The web-coupled contract lives in BeeEye.Shared.Web.
        var kernel = ModuleAssemblies()
            .Concat(AppDomain.CurrentDomain.GetAssemblies())
            .FirstOrDefault(a => a.GetName().Name == SharedKernelAssembly)
            ?? Assembly.Load(SharedKernelAssembly);

        var aspNetReferences = kernel.GetReferencedAssemblies()
            .Select(r => r.Name)
            .Where(name => name?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true)
            .ToList();

        Assert.True(
            aspNetReferences.Count == 0,
            $"The pure shared kernel must not reference ASP.NET Core. Found: {string.Join(", ", aspNetReferences)}");
    }

    [Fact]
    public void Every_module_exposes_exactly_one_sealed_IModule_implementation()
    {
        foreach (var assembly in ModuleAssemblies())
        {
            var implementations = assembly.GetTypes()
                .Where(t => typeof(IModule).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
                .ToList();

            Assert.True(
                implementations.Count == 1,
                $"{assembly.GetName().Name} must expose exactly one IModule implementation, found {implementations.Count}.");
            Assert.True(
                implementations[0].IsSealed,
                $"{implementations[0].FullName} must be sealed.");
        }
    }
}
