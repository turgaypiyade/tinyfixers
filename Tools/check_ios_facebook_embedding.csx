// Execute production configuration with Unity's real managed PBX API against a synthetic project.
// No Unity build, device launch, existing Xcode export write or package fetch.
#r "Microsoft.CodeAnalysis"
#r "Microsoft.CodeAnalysis.CSharp"
#r "Microsoft.CodeAnalysis.Scripting"
#r "Microsoft.CodeAnalysis.CSharp.Scripting"
using System;
using System.IO;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
string sdk="/Applications/Unity/Hub/Editor/6000.3.9f1/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport/UnityEditor.iOS.Extensions.Xcode.dll";
string source=File.ReadAllText("Assets/_Project/Editor/Auth/IosFacebookFrameworkEmbedPostProcess.cs").Replace("#if UNITY_EDITOR && UNITY_IOS", "").Replace("#endif", "").Replace("using UnityEditor;", "").Replace("using UnityEditor.Build;", "").Replace("using UnityEditor.Callbacks;", "");
string harness="using System.Linq; using System.Text.RegularExpressions;\n"+source+File.ReadAllText("Tools/ios_facebook_embedding_harness.txt");
try{
 var result=CSharpScript.EvaluateAsync<int>(harness,ScriptOptions.Default.AddReferences(sdk,typeof(System.Linq.Enumerable).Assembly.Location,typeof(System.Text.RegularExpressions.Regex).Assembly.Location)).GetAwaiter().GetResult();
 Console.WriteLine("PASS: "+result+" production Facebook embedding checks using installed Unity PBX API");
}catch(Exception e){Console.Error.WriteLine(e);Environment.Exit(1);}
