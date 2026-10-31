// Run: csi Tools/check_ios_auth_dependencies.csx. No Unity build, package fetch or export mutation.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
#r "System.Xml.Linq"
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
string sourcePath="Assets/_Project/Editor/Auth/IosAuthDependencyPostProcess.cs";
string source=File.ReadAllText(sourcePath);
var parse=new CSharpParseOptions(preprocessorSymbols:new[]{"UNITY_EDITOR","UNITY_IOS"});
var tree=CSharpSyntaxTree.ParseText(source,parse,path:sourcePath);
string methods=string.Join("\n",tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"DeduplicateSwiftPackages","MergeEntries"}.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var google=XDocument.Load("Assets/_Project/Editor/Auth/GoogleSignInDependencies.xml");
var package=google.Root.Elements("remoteSwiftPackage").Single();
if((string)package.Attribute("url")!="https://github.com/google/GoogleSignIn-iOS.git"||(string)package.Attribute("version")!="10.1.0")throw new Exception("Google SPM identity/version changed");
if((string)package.Element("swiftPackage").Attribute("name")!="GoogleSignIn"||(string)package.Element("swiftPackage").Attribute("replacesPod")!="GoogleSignIn")throw new Exception("Missing Pod replacement mapping");
if(google.Root.Element("androidPackages").Elements("androidPackage").Count()!=3)throw new Exception("Android dependencies lost");
foreach(string name in new[]{"App","Auth","Analytics","Firestore"}){
 var firebase=XDocument.Load("Assets/Firebase/Editor/"+name+"Dependencies.xml");
 if((string)firebase.Root.Element("remoteSwiftPackage").Attribute("version")!="12.15.0")throw new Exception("Firebase version changed");
}
Console.WriteLine("PASS: Google SPM/Pod replacement and pinned Firebase/Android dependency declarations.");
string harness="using System; using System.Text; using System.Text.RegularExpressions; using System.Collections.Generic;\npublic static class Policy {\n"+methods+"\n}\n"+File.ReadAllText("Tools/ios_auth_dependencies_harness.txt");
try {Console.WriteLine("PASS: "+CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(typeof(System.Text.RegularExpressions.Regex).Assembly)).GetAwaiter().GetResult()+" production SPM normalization checks");}
catch(Exception e){Console.Error.WriteLine(e);Environment.Exit(1);}
string unity="/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/Resources/Scripting/Managed/UnityEngine";
var refs=new List<MetadataReference>{MetadataReference.CreateFromFile(typeof(object).Assembly.Location),MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),MetadataReference.CreateFromFile(typeof(System.Text.RegularExpressions.Regex).Assembly.Location)};
refs.AddRange(Directory.GetFiles(unity,"*.dll").Select(p=>MetadataReference.CreateFromFile(p)));
refs.Add(MetadataReference.CreateFromFile("/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5/Facades/netstandard.dll"));
refs.Add(MetadataReference.CreateFromFile("/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll"));
refs.Add(MetadataReference.CreateFromFile(Path.GetFullPath("Assets/ExternalDependencyManager/Editor/1.2.187/Google.IOSResolver.dll")));
var trees=new[]{tree,CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Scripts/Editor/IosDeploymentTargetPostProcess.cs"),parse),CSharpSyntaxTree.ParseText(File.ReadAllText("Assets/_Project/Editor/Auth/IosFacebookFrameworkEmbedPostProcess.cs"),parse)};
var compilation=CSharpCompilation.Create("IosDependencyApiCheck",trees,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
var errors=compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.ToString())));
Console.WriteLine("PASS: export hooks bind to installed Unity Xcode and EDM4U APIs; no hooks invoked or assembly emitted.");
