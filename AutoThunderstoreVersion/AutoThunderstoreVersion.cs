﻿using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoThunderstoreVersion;

[Generator]
internal class AutoThunderstoreVerison : ISourceGenerator
{

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int MessageBox(IntPtr hWnd, String text, String caption, int options);

    private const string AttributeSource = @"
namespace AutoThunderstoreVersion;

/// <summary>
/// Add a PluginVersion field to the attribut-ed class filled from the thunderstore.toml versionNumber field
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal class AutoVersionAttribute : System.Attribute {}
";

    public void Initialize(GeneratorInitializationContext ctx)
    {
        ctx.RegisterForPostInitialization(static i => i.AddSource("AutoVersionAttribute.g.cs", AttributeSource));

        ctx.RegisterForSyntaxNotifications(static () => new VersionedClassesCollector());
    }

    public void Execute(GeneratorExecutionContext ctx)
    {
        if (ctx.SyntaxContextReceiver is not VersionedClassesCollector collector)
            return;

        ctx.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir);
        var thunderstoreProject = Path.Combine(projectDir!, "..", "thunderstore.toml");
        if (!File.Exists(thunderstoreProject))
        {
            return;
        }
        var versionLine = File.ReadAllLines(thunderstoreProject).First(static line => line.StartsWith("versionNumber")).Replace("versionNumber", "PluginVersion");

        foreach (var type in collector.Classes)
        {
            var containingNamespace = type.ContainingNamespace.ToDisplayString();

            var source = $@"
namespace {containingNamespace};

public partial class {type.Name}
{{
    /// <summary>
    /// Version of the plugin, should be matching with the thunderstore package.
    /// </summary>
    public const string {versionLine};
}}
";
            ctx.AddSource($"{type.ToDisplayString()}.g.AutoVersion.cs", source);
        }
    }
}

internal class VersionedClassesCollector : ISyntaxContextReceiver
{
    public List<ITypeSymbol> Classes { get; } = new();

    public void OnVisitSyntaxNode(GeneratorSyntaxContext ctx)
    {
        if (ctx.Node is ClassDeclarationSyntax { AttributeLists.Count: > 0 } klass)
        {
            var type = ctx.SemanticModel.GetDeclaredSymbol(klass) as ITypeSymbol;
            if (type!.GetAttributes().Any(static att => att.AttributeClass!.ToDisplayString() == "AutoThunderstoreVersion.AutoVersionAttribute"))
            {
                Classes.Add(type);
            }
        }
    }
}