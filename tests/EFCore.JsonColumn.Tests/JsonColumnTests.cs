// <copyright file="JsonColumnTests.cs" company="Canon Europe Limited">
// Copyright (c) Canon Europe Limited. All rights reserved.
// </copyright>

namespace EFCore.JsonColumn.Tests;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EFCore.JsonColumn;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>JSON-owned type used by integration tests.</summary>
public class Address
{
    /// <summary>Gets or sets the street.</summary>
    public string Street { get; set; } = "";

    /// <summary>Gets or sets the city.</summary>
    public string City { get; set; } = "";
}

/// <summary>Additional JSON-owned type used by integration tests.</summary>
public class CustomerProfile
{
    /// <summary>Gets or sets the profile bio.</summary>
    public string Bio { get; set; } = "";
}

/// <summary>Entity with JSON-column owned navigations.</summary>
public class Order
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the shipping address stored as JSON.</summary>
    [JsonColumn]
    public Address ShippingAddress { get; set; } = new();

    /// <summary>Gets or sets the customer profile stored as JSON.</summary>
    [JsonColumn]
    public CustomerProfile Profile { get; set; } = new();
}

/// <summary>DbContext used for SQLite JSON-column integration tests.</summary>
public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    /// <summary>Gets the orders set.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureJsonColumns();
}

file static class TestCompilationFactory
{
    public static CSharpCompilation Create(string source)
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ModelBuilder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(JsonColumnGenerator).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
        };

        TryAddReference(references, "netstandard");
        TryAddReference(references, "System.Collections");
        TryAddReference(references, "System.Linq");
        TryAddReference(references, "System.Linq.Expressions");

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static void TryAddReference(ICollection<MetadataReference> references, string assemblyName)
    {
        try
        {
            references.Add(MetadataReference.CreateFromFile(Assembly.Load(assemblyName).Location));
        }
        catch
        {
        }
    }
}

file static class SqliteJsonColumnTestContextFactory
{
    private static int s_connectionIndex;

    public static async Task<SqliteConnection> OpenRootConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(CreateConnectionString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    public static TestDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new TestDbContext(options);
    }

    private static string CreateConnectionString()
        => $"Data Source=jctest_{Interlocked.Increment(ref s_connectionIndex)};Mode=Memory;Cache=Shared";
}

/// <summary>Verifies source-generator output.</summary>
public class GeneratorOutputTests
{
    private static IReadOnlyDictionary<string, string> RunGenerator(string source)
    {
        var compilation = TestCompilationFactory.Create(source);
        var generator = new JsonColumnGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .RunGenerators(compilation);

        return driver.GetRunResult().GeneratedTrees
            .ToDictionary(
                static tree => Path.GetFileName(tree.FilePath),
                static tree => tree.GetText().ToString());
    }

    private static IReadOnlyList<Diagnostic> GetDiagnostics(string source)
    {
        var compilation = TestCompilationFactory.Create(source);
        var generator = new JsonColumnGenerator();
        CSharpGeneratorDriver.Create(generator).RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out var diagnostics);

        return diagnostics;
    }

    /// <summary>Verifies that the generator always emits the attribute source file.</summary>
    [Fact]
    public void AlwaysEmits_AttributeFile()
        => RunGenerator(string.Empty).Should().ContainKey("EFCore.JsonColumn.Attribute.g.cs");

    /// <summary>Verifies that the generator always emits the core source file.</summary>
    [Fact]
    public void AlwaysEmits_CoreFile()
        => RunGenerator(string.Empty).Should().ContainKey("EFCore.JsonColumn.Core.g.cs");

    /// <summary>Verifies that empty compilations generate a no-op configuration method.</summary>
    [Fact]
    public void EmptyProject_GeneratesEmptyConfigureMethod()
    {
        var output = RunGenerator(string.Empty)["EFCore.JsonColumn.Core.g.cs"];
        output.Should().Contain("public static ModelBuilder ConfigureJsonColumns(this ModelBuilder modelBuilder)");
        output.Should().Contain("return modelBuilder;");
        output.Should().NotContain("OwnsOne");
    }

    /// <summary>Verifies that a single annotated property generates an OwnsOne call.</summary>
    [Fact]
    public void SingleJsonColumn_GeneratesOwnsOne()
    {
        var output = RunGenerator(
            """
            using EFCore.JsonColumn;
            public class Address { }
            public class Order
            {
                [JsonColumn]
                public Address ShippingAddress { get; set; } = new();
            }
            """)["EFCore.JsonColumn.Core.g.cs"];

        output.Should().Contain("OwnsOne");
    }

    /// <summary>Verifies that a single annotated property generates an entity block.</summary>
    [Fact]
    public void SingleJsonColumn_GeneratesEntityBlock()
        => RunGenerator(
            """
            using EFCore.JsonColumn;
            public class Address { }
            public class Order
            {
                [JsonColumn]
                public Address ShippingAddress { get; set; } = new();
            }
            """)["EFCore.JsonColumn.Core.g.cs"].Should().Contain("modelBuilder.Entity<");

    /// <summary>Verifies that a single annotated property generates a ToJson call.</summary>
    [Fact]
    public void SingleJsonColumn_GeneratesToJson()
        => RunGenerator(
            """
            using EFCore.JsonColumn;
            public class Address { }
            public class Order
            {
                [JsonColumn]
                public Address ShippingAddress { get; set; } = new();
            }
            """)["EFCore.JsonColumn.Core.g.cs"].Should().Contain(".ToJson()");

    /// <summary>Verifies that multiple JSON columns on the same entity are all configured.</summary>
    [Fact]
    public void MultipleJsonColumns_SameEntity_BothConfigured()
    {
        var output = RunGenerator(
            """
            using EFCore.JsonColumn;
            public class Address { }
            public class CustomerProfile { }
            public class Order
            {
                [JsonColumn]
                public Address ShippingAddress { get; set; } = new();

                [JsonColumn]
                public CustomerProfile Profile { get; set; } = new();
            }
            """)["EFCore.JsonColumn.Core.g.cs"];

        output.Should().Contain("ShippingAddress");
        output.Should().Contain("Profile");
    }

    /// <summary>Verifies that multiple entities produce separate entity blocks.</summary>
    [Fact]
    public void MultipleEntities_BothConfigured()
    {
        var output = RunGenerator(
            """
            using EFCore.JsonColumn;
            public class Address { }
            public class CustomerProfile { }
            namespace MyApp;
            public class Order
            {
                [JsonColumn]
                public Address ShippingAddress { get; set; } = new();
            }
            public class Customer
            {
                [JsonColumn]
                public CustomerProfile Profile { get; set; } = new();
            }
            """)["EFCore.JsonColumn.Core.g.cs"];

        output.Split("modelBuilder.Entity<").Length.Should().Be(3);
    }

    /// <summary>Verifies that generated type names use the global qualifier.</summary>
    [Fact]
    public void NamespacePrefix_UsesGlobalQualifier()
        => RunGenerator(
            """
            using EFCore.JsonColumn;
            namespace MyApp;
            public class Address { }
            public class Order
            {
                [JsonColumn]
                public Address ShippingAddress { get; set; } = new();
            }
            """)["EFCore.JsonColumn.Core.g.cs"].Should().Contain("global::");

    /// <summary>Verifies that value-type JSON-column properties produce JSCOL001.</summary>
    [Fact]
    public void ValueTypeProperty_ReportsJSCOL001()
        => GetDiagnostics(
            """
            using EFCore.JsonColumn;
            public class Order
            {
                [JsonColumn]
                public int BadProp { get; set; }
            }
            """).Should().ContainSingle(static diagnostic => diagnostic.Id == "JSCOL001");

    /// <summary>Verifies that reference-type JSON-column properties do not produce JSCOL001.</summary>
    [Fact]
    public void ReferenceTypeProperty_NoJSCOL001()
        => GetDiagnostics(
            """
            using EFCore.JsonColumn;
            public class Address { }
            public class Order
            {
                [JsonColumn]
                public Address GoodProp { get; set; } = new();
            }
            """).Should().NotContain(static diagnostic => diagnostic.Id == "JSCOL001");

    /// <summary>Verifies that generated source includes the auto-generated banner.</summary>
    [Fact]
    public void HasAutoGeneratedComment()
        => RunGenerator(string.Empty)["EFCore.JsonColumn.Core.g.cs"].Should().Contain("// <auto-generated by Swevo.EFCore.JsonColumn/>");
}

/// <summary>Verifies EF Core JSON-column integration using SQLite shared in-memory databases.</summary>
public class JsonColumnIntegrationTests
{
    private readonly Order order = new()
    {
        ShippingAddress = new Address { Street = "1 High Street", City = "London" },
        Profile = new CustomerProfile { Bio = "First bio" },
    };

    /// <summary>Verifies that a JSON column round-trips through SQLite.</summary>
    [Fact]
    public async Task SaveAndLoad_JsonColumn_RoundTrips()
    {
        await using var rootConnection = await SqliteJsonColumnTestContextFactory.OpenRootConnectionAsync();
        await using (var setupContext = SqliteJsonColumnTestContextFactory.CreateDbContext(rootConnection.ConnectionString))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Orders.Add(order);
            await setupContext.SaveChangesAsync();
        }

        await using var verifyContext = SqliteJsonColumnTestContextFactory.CreateDbContext(rootConnection.ConnectionString);
        var reloadedOrder = await verifyContext.Orders.SingleAsync();

        reloadedOrder.ShippingAddress.Street.Should().Be("1 High Street");
    }

    /// <summary>Verifies that multiple JSON columns round-trip through SQLite.</summary>
    [Fact]
    public async Task SaveAndLoad_MultipleJsonColumns_BothRoundTrip()
    {
        await using var rootConnection = await SqliteJsonColumnTestContextFactory.OpenRootConnectionAsync();
        await using (var setupContext = SqliteJsonColumnTestContextFactory.CreateDbContext(rootConnection.ConnectionString))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Orders.Add(order);
            await setupContext.SaveChangesAsync();
        }

        await using var verifyContext = SqliteJsonColumnTestContextFactory.CreateDbContext(rootConnection.ConnectionString);
        var reloadedOrder = await verifyContext.Orders.SingleAsync();

        reloadedOrder.ShippingAddress.City.Should().Be("London");
        reloadedOrder.Profile.Bio.Should().Be("First bio");
    }

    /// <summary>Verifies that updates to JSON-owned values are persisted.</summary>
    [Fact]
    public async Task JsonColumn_Update_PersistsChanges()
    {
        await using var rootConnection = await SqliteJsonColumnTestContextFactory.OpenRootConnectionAsync();
        await using (var setupContext = SqliteJsonColumnTestContextFactory.CreateDbContext(rootConnection.ConnectionString))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Orders.Add(order);
            await setupContext.SaveChangesAsync();
        }

        await using (var updateContext = SqliteJsonColumnTestContextFactory.CreateDbContext(rootConnection.ConnectionString))
        {
            var orderToUpdate = await updateContext.Orders.SingleAsync();
            orderToUpdate.ShippingAddress.City = "Paris";
            await updateContext.SaveChangesAsync();
        }

        await using var verifyContext = SqliteJsonColumnTestContextFactory.CreateDbContext(rootConnection.ConnectionString);
        var reloadedOrder = await verifyContext.Orders.SingleAsync();

        reloadedOrder.ShippingAddress.City.Should().Be("Paris");
    }

    /// <summary>Verifies that default JSON-owned instances remain non-null after persistence.</summary>
    [Fact]
    public async Task JsonColumn_DefaultValue_IsNotNull()
    {
        await using var rootConnection = await SqliteJsonColumnTestContextFactory.OpenRootConnectionAsync();
        await using (var setupContext = SqliteJsonColumnTestContextFactory.CreateDbContext(rootConnection.ConnectionString))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Orders.Add(new Order());
            await setupContext.SaveChangesAsync();
        }

        await using var verifyContext = SqliteJsonColumnTestContextFactory.CreateDbContext(rootConnection.ConnectionString);
        var reloadedOrder = await verifyContext.Orders.SingleAsync();

        reloadedOrder.ShippingAddress.Should().NotBeNull();
    }
}

