using AsmResolver.DotNet;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;
using System.Text;

namespace TheLongDarkNuGetFactory;

internal class Program
{

	static string outputfolder { get; set; } = $"../../../../Nuget";

	static void Main(string[] args)
	{
		MemoryStream stream = new();
		ZipFile zipFile = ZipFile.Create(stream);
		zipFile.BeginUpdate();
		foreach ((string path, byte[] data) in GetEntries(args[0]))
		{
			zipFile.Add(new StreamDataSource(new MemoryStream(data)), new ZipEntry(path));
			Console.WriteLine($"Added: {path}");
		}
		zipFile.CommitUpdate();
		if (!Directory.Exists(outputfolder))
		{
			Directory.CreateDirectory(outputfolder);
		}

		File.WriteAllBytes($"{outputfolder}/{PackageInformation.Name}.{PackageInformation.Version}.nupkg", stream.ToArray());
		File.WriteAllText($"{outputfolder}/{NuSpec.Path}", NuSpec.Content);
		Console.WriteLine("Done!");
	}

	private static IEnumerable<(string, byte[])> GetEntries(string il2cppAssembliesDirectory)
	{
		yield return GetEntryData(Rels.Path, Rels.Content);
		yield return GetEntryData(NuSpec.Path, NuSpec.Content);
		yield return GetEntryData(ContentTypes.Path, ContentTypes.Content);
		yield return GetEntryData(Psmdcp.Path, Psmdcp.Content);
		yield return GetEntryData(License.Path, License.Content);
		yield return GetEntryData(ReadMe.Path, ReadMe.Content);
		foreach (string assemblyPath in Directory.EnumerateFiles(il2cppAssembliesDirectory, "*.dll"))
		{
			string fileName = Path.GetFileName(assemblyPath);
			yield return ($"lib/net6.0/{fileName}", RemoveNullableAttribute(File.ReadAllBytes(assemblyPath)));
		}
	}

	private static (string, byte[]) GetEntryData(string path, string content)
	{
		return (path, Encoding.UTF8.GetBytes(content));
	}

	// This fixes an issue where the NullableAttribute defined in the assembly confuses the compiler.
	private static byte[] RemoveNullableAttribute(byte[] data)
	{
		ModuleDefinition module = ModuleDefinition.FromBytes(data);
		bool modified = false;
		for (int i = module.TopLevelTypes.Count - 1; i >= 0; i--)
		{
			if (module.TopLevelTypes[i] is { Namespace.Value: "System.Runtime.CompilerServices", Name.Value: "NullableAttribute" })
			{
				module.TopLevelTypes.RemoveAt(i);
				modified = true;
			}
		}
		if (modified)
		{
			using MemoryStream stream = new();
			module.Write(stream);
			data = stream.ToArray();
			Console.WriteLine($"Modified: {module.Name}");

			File.WriteAllBytes(module.Assembly.Name+".dll", stream.ToArray());
		}
		return data;
	}
}
