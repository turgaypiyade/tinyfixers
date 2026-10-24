// Bind the three presentation classes and editor preview to installed Unity API metadata.
// Does not emit an assembly, launch Unity, run Play, or build the project.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
using System;using System.IO;using System.Linq;using System.Collections.Generic;
using Microsoft.CodeAnalysis;using Microsoft.CodeAnalysis.CSharp;
string unity="/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine";
var refs=new List<MetadataReference>{MetadataReference.CreateFromFile(typeof(object).Assembly.Location),MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)};
refs.AddRange(Directory.GetFiles(unity,"*.dll").Select(f=>MetadataReference.CreateFromFile(f)));
refs.Add(MetadataReference.CreateFromFile(Path.GetFullPath("Library/ScriptAssemblies/UnityEngine.UI.dll")));
refs.Add(MetadataReference.CreateFromFile("/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5/Facades/netstandard.dll"));
var files=new[]{"GelLauncherPressMotion","GelLauncherPressRodGraphic","GelLauncherPressView"};
var trees=files.Select(n=>CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Grid/Board/"+n+".cs"))).ToList();
trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Editor/GelLauncherPressPreview.cs")));
trees.Add(CSharpSyntaxTree.ParseText("public enum ObstacleId {GelLauncherUp,GelLauncherDown,GelLauncherLeft,GelLauncherRight} public static class GelLauncherFx {public static bool IsVertical(ObstacleId id)=>true;}"));
var compilation=CSharpCompilation.Create("GelPressApiCheck",trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var errors=compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(d=>d.ToString())));
// Binding alone does not check dynamically added component dependencies. Unity's
// Graphic accesses its CanvasRenderer during enable, before Build assigns material.
var rod=compilation.GetTypeByMetadataName("GelLauncherPressRodGraphic");
bool requiresRenderer=rod.GetAttributes().Any(a=>
 a.AttributeClass.ToDisplayString()=="UnityEngine.RequireComponent" &&
 a.ConstructorArguments.Any(v=>v.Value is ITypeSymbol t && t.ToDisplayString()=="UnityEngine.CanvasRenderer"));
if(!requiresRenderer)throw new Exception("Press rod must declare CanvasRenderer so AddComponent supplies it before Graphic.OnEnable.");
Console.WriteLine("PASS: three new press classes bind to installed Unity 6000.3.9f1 UI/API metadata. No assembly emitted/project build.");
Console.WriteLine("PASS: dynamically created press rod declares its CanvasRenderer dependency before enable.");
var editorErrors=CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Editor/LevelDataEditor.cs")).GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(editorErrors.Length>0)throw new Exception(string.Join("\n",editorErrors.Select(d=>d.ToString())));
Console.WriteLine("PASS: layered editor preview binds to UnityEditor API; LevelDataEditor syntax checked. No editor UI rendered.");
