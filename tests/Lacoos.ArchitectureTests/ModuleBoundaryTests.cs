using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Lacoos.Modules.Budget;
using Lacoos.Modules.Checklist;
using Lacoos.Modules.Dashboard;
using Lacoos.Modules.Gifts;
using Lacoos.Modules.Guests;
using Lacoos.Modules.Identity;
using Lacoos.Modules.Payments;
using Lacoos.Modules.Suppliers;
using Lacoos.Modules.Weddings;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using NetArchTest.Rules;
using Xunit;

namespace Lacoos.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly Dictionary<string, Assembly> Modules = new()
    {
        ["Identity"] = typeof(IdentityModule).Assembly,
        ["Weddings"] = typeof(WeddingsModule).Assembly,
        ["Dashboard"] = typeof(DashboardModule).Assembly,
        ["Checklist"] = typeof(ChecklistModule).Assembly,
        ["Budget"] = typeof(BudgetModule).Assembly,
        ["Guests"] = typeof(GuestsModule).Assembly,
        ["Suppliers"] = typeof(SuppliersModule).Assembly,
        ["Payments"] = typeof(PaymentsModule).Assembly,
        ["Gifts"] = typeof(GiftsModule).Assembly,
    };

    private static readonly string[] InternalLayers = ["Domain", "Application", "Persistence", "Endpoints"];

    public static TheoryData<string> ModuleNames => new(Modules.Keys);

    public static TheoryData<string> DataModuleNames => new(Modules.Keys.Where(m => m != "Dashboard"));

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_only_references_contracts_of_other_modules(string module)
    {
        var offending = Modules[module].GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("Lacoos.Modules.", StringComparison.Ordinal))
            .Where(name => !name.EndsWith(".Contracts", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offending);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_project_file_only_references_contracts_of_other_modules(string module)
    {
        var path = Path.Combine(RepositoryRoot(), "src", "Modules", module, $"Lacoos.Modules.{module}", $"Lacoos.Modules.{module}.csproj");

        var offending = XDocument.Load(path).Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/')))
            .Where(name => name.StartsWith("Lacoos.Modules.", StringComparison.Ordinal))
            .Where(name => !name.EndsWith(".Contracts", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offending);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_does_not_depend_on_the_interior_of_other_modules(string module)
    {
        var forbidden = Modules.Keys
            .Where(other => other != module)
            .SelectMany(other => InternalLayers.Select(layer => $"Lacoos.Modules.{other}.{layer}"))
            .ToArray();

        var result = Types.InAssembly(Modules[module]).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_exposes_only_its_entry_point(string module)
    {
        var exposed = Modules[module].GetExportedTypes()
            .Where(type => !typeof(Migration).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToList();

        Assert.Equal([$"{module}Module"], exposed);
    }

    [Theory]
    [MemberData(nameof(DataModuleNames))]
    public void Contracts_do_not_depend_on_persistence_or_on_module_interiors(string module)
    {
        var contracts = Assembly.Load($"Lacoos.Modules.{module}.Contracts");

        var references = contracts.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(references, name => name.StartsWith("Lacoos.", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(DataModuleNames))]
    public void Data_module_has_a_single_context_with_its_own_schema(string module)
    {
        var contexts = Modules[module].GetTypes().Where(IsModuleContext).ToList();

        var context = Assert.Single(contexts);
        var schema = context.GetField("SchemaName")?.GetRawConstantValue();
        Assert.Equal(module.ToLowerInvariant(), schema);
    }

    [Theory]
    [MemberData(nameof(DataModuleNames))]
    public void Data_module_has_its_own_migrations(string module)
    {
        Assert.Contains(Modules[module].GetTypes(), type => typeof(Migration).IsAssignableFrom(type));
    }

    [Fact]
    public void Dashboard_has_no_persistence()
    {
        var dashboard = Modules["Dashboard"];

        Assert.DoesNotContain(dashboard.GetTypes(), type => typeof(DbContext).IsAssignableFrom(type));
        Assert.DoesNotContain(
            dashboard.GetReferencedAssemblies(),
            assembly => assembly.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Host_only_uses_module_entry_points()
    {
        var forbidden = Modules.Keys
            .SelectMany(module => InternalLayers.Select(layer => $"Lacoos.Modules.{module}.{layer}"))
            .ToArray();

        var result = Types.InAssembly(typeof(Program).Assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Shared_kernel_does_not_know_the_modules()
    {
        var result = Types.InAssembly(typeof(IModule).Assembly).ShouldNot().HaveDependencyOn("Lacoos.Modules").GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Migrations_do_not_create_foreign_keys_to_other_schemas()
    {
        var root = Path.Combine(RepositoryRoot(), "src", "Modules");
        var offending = new List<string>();

        foreach (var (module, _) in Modules)
        {
            var folder = Path.Combine(root, module, $"Lacoos.Modules.{module}", "Persistence", "Migrations");
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(folder, "*Initial.cs"))
            {
                var principals = Regex.Matches(File.ReadAllText(file), "principalSchema: \"(?<schema>[a-z_]+)\"")
                    .Select(m => m.Groups["schema"].Value)
                    .Where(schema => schema != module.ToLowerInvariant());
                offending.AddRange(principals.Select(schema => $"{module} -> {schema}"));
            }
        }

        Assert.Empty(offending);
    }

    private static bool IsModuleContext(Type type) => type is { IsAbstract: false } && typeof(ModuleDbContext).IsAssignableFrom(type);

    private static string Failures(NetArchTest.Rules.TestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LacoosApi.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
