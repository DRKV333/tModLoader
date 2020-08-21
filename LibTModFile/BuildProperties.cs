using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Terraria.ModLoader.Core
{
	//todo: further documentation
	public class BuildProperties
	{
		public struct ModReference
		{
			public string mod;
			public Version target;

			public ModReference(string mod, Version target) {
				this.mod = mod;
				this.target = target;
			}

			public override string ToString() => target == null ? mod : mod + '@' + target;

			public static ModReference Parse(string spec) {
				var split = spec.Split('@');
				if (split.Length == 1)
					return new ModReference(split[0], null);

				if (split.Length > 2)
					throw new Exception("Invalid mod reference: " + spec);

				try {
					return new ModReference(split[0], new Version(split[1]));
				}
				catch {
					throw new Exception("Invalid mod reference: " + spec);
				}
			}
		}

		public List<string> DllReferences { get; private set; } = new List<string>();
		public List<ModReference> ModReferences { get; private set; } = new List<ModReference>();
		public List<ModReference> WeakReferences { get; private set; } = new List<ModReference>();
		//this mod will load after any mods in this list
		//sortAfter includes (mod|weak)References that are not in sortBefore
		public List<string> SortAfter { get; private set; } = new List<string>();
		//this mod will load before any mods in this list
		public List<string> SortBefore { get; private set; } = new List<string>();
		public List<string> BuildIgnores { get; private set; } = new List<string>();
		public string Author { get; set; } = "";
		public Version Version { get; set; } = new Version(1, 0);
		public string DisplayName { get; set; } = "";
		public bool NoCompile { get; set; } = false;
		public bool HideCode { get; set; } = false;
		public bool HideResources { get; set; } = false;
		public bool IncludeSource { get; set; } = false;
		public bool IncludePDB { get; set; } = true;
		public string EacPath { get; set; } = "";
		// This .tmod was built against a beta release, preventing publishing.
		public bool Beta { get; set; } = false;
		public Version BuildVersion { get; internal set; }
		public string Homepage { get; set; } = "";
		public string Description { get; set; } = "";
		public ModSide Side { get; set; }

		public BuildProperties(Version buildVersion, bool beta)
		{
			this.BuildVersion = buildVersion;
			this.Beta = beta;
		}

		private BuildProperties()
		{
		}

		public IEnumerable<ModReference> Refs(bool includeWeak) =>
			includeWeak ? ModReferences.Concat(WeakReferences) : ModReferences;

		public IEnumerable<string> RefNames(bool includeWeak) => Refs(includeWeak).Select(dep => dep.mod);

		private static IEnumerable<string> ReadList(string value)
			=> value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);

		private static List<string> ReadList(BinaryReader reader) {
			var list = new List<string>();
			for (string item = reader.ReadString(); item.Length > 0; item = reader.ReadString())
				list.Add(item);

			return list;
		}

		private static void WriteList<T>(IEnumerable<T> list, BinaryWriter writer) {
			foreach (var item in list)
				writer.Write(item.ToString());

			writer.Write("");
		}

		public static BuildProperties ReadBuildFile(string modDir, Version buildVersion, bool beta) {
			string propertiesFile = modDir + Path.DirectorySeparatorChar + "build.txt";
			string descriptionfile = modDir + Path.DirectorySeparatorChar + "description.txt";
			BuildProperties properties = new BuildProperties(buildVersion, beta);
			if (!File.Exists(propertiesFile)) {
				return properties;
			}
			if (File.Exists(descriptionfile)) {
				properties.Description = File.ReadAllText(descriptionfile);
			}
			foreach (string line in File.ReadAllLines(propertiesFile)) {
				if (string.IsNullOrWhiteSpace(line)) {
					continue;
				}
				int split = line.IndexOf('=');
				string property = line.Substring(0, split).Trim();
				string value = line.Substring(split + 1).Trim();
				if (value.Length == 0) {
					continue;
				}
				switch (property) {
					case "dllReferences":
						properties.DllReferences = ReadList(value).ToList();
						break;
					case "modReferences":
						properties.ModReferences = ReadList(value).Select(ModReference.Parse).ToList();
						break;
					case "weakReferences":
						properties.WeakReferences = ReadList(value).Select(ModReference.Parse).ToList();
						break;
					case "sortBefore":
						properties.SortBefore = ReadList(value).ToList();
						break;
					case "sortAfter":
						properties.SortAfter = ReadList(value).ToList();
						break;
					case "author":
						properties.Author = value;
						break;
					case "version":
						properties.Version = new Version(value);
						break;
					case "displayName":
						properties.DisplayName = value;
						break;
					case "homepage":
						properties.Homepage = value;
						break;
					case "noCompile":
						properties.NoCompile = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
						break;
					case "hideCode":
						properties.HideCode = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
						break;
					case "hideResources":
						properties.HideResources = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
						break;
					case "includeSource":
						properties.IncludeSource = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
						break;
					case "includePDB":
						properties.IncludePDB = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
						break;
					case "buildIgnore":
						properties.BuildIgnores = value.Split(',').Select(s => s.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)).Where(s => s.Length > 0).ToList();
						break;
					case "side":
						if (!Enum.TryParse(value, true, out ModSide side))
							throw new Exception("side is not one of (Both, Client, Server, NoSync): " + value);
						properties.Side = side;
						break;
				}
			}

			var refs = properties.RefNames(true).ToList();
			if (refs.Count != refs.Distinct().Count())
				throw new Exception("Duplicate mod/weak reference");

			//add (mod|weak)References that are not in sortBefore to sortAfter
			properties.SortAfter = properties.RefNames(true).Where(dep => !properties.SortBefore.Contains(dep))
				.Concat(properties.SortAfter).Distinct().ToList();

			return properties;
		}

		public byte[] ToBytes() {
			byte[] data;
			using (MemoryStream memoryStream = new MemoryStream()) {
				using (BinaryWriter writer = new BinaryWriter(memoryStream)) {
					if (DllReferences.Count > 0) {
						writer.Write("dllReferences");
						WriteList(DllReferences, writer);
					}
					if (ModReferences.Count > 0) {
						writer.Write("modReferences");
						WriteList(ModReferences, writer);
					}
					if (WeakReferences.Count > 0) {
						writer.Write("weakReferences");
						WriteList(WeakReferences, writer);
					}
					if (SortAfter.Count > 0) {
						writer.Write("sortAfter");
						WriteList(SortAfter, writer);
					}
					if (SortBefore.Count > 0) {
						writer.Write("sortBefore");
						WriteList(SortBefore, writer);
					}
					if (Author.Length > 0) {
						writer.Write("author");
						writer.Write(Author);
					}
					writer.Write("version");
					writer.Write(Version.ToString());
					if (DisplayName.Length > 0) {
						writer.Write("displayName");
						writer.Write(DisplayName);
					}
					if (Homepage.Length > 0) {
						writer.Write("homepage");
						writer.Write(Homepage);
					}
					if (Description.Length > 0) {
						writer.Write("description");
						writer.Write(Description);
					}
					if (NoCompile) {
						writer.Write("noCompile");
					}
					if (!HideCode) {
						writer.Write("!hideCode");
					}
					if (!HideResources) {
						writer.Write("!hideResources");
					}
					if (IncludeSource) {
						writer.Write("includeSource");
					}
					if (IncludePDB) {
						writer.Write("includePDB");
					}
					if (EacPath.Length > 0) {
						writer.Write("eacPath");
						writer.Write(EacPath);
					}
					if (Side != ModSide.Both) {
						writer.Write("side");
						writer.Write((byte)Side);
					}
					if (Beta) {
						writer.Write("beta");
					}

					writer.Write("buildVersion");
					writer.Write(BuildVersion.ToString());

					writer.Write("");
				}
				data = memoryStream.ToArray();
			}
			return data;
		}

		public static BuildProperties ReadModFile(TmodFile modFile) {
			return ReadFromStream(modFile.GetStream("Info"));
		}

		public static BuildProperties ReadFromStream(Stream stream) {
			BuildProperties properties = new BuildProperties();
			// While the intended defaults for these are false, Info will only have !hideCode and !hideResources entries, so this is necessary.
			properties.HideCode = true;
			properties.HideResources = true;
			using (var reader = new BinaryReader(stream)) {
				for (string tag = reader.ReadString(); tag.Length > 0; tag = reader.ReadString()) {
					if (tag == "dllReferences") {
						properties.DllReferences = ReadList(reader);
					}
					if (tag == "modReferences") {
						properties.ModReferences = ReadList(reader).Select(ModReference.Parse).ToList();
					}
					if (tag == "weakReferences") {
						properties.WeakReferences = ReadList(reader).Select(ModReference.Parse).ToList();
					}
					if (tag == "sortAfter") {
						properties.SortAfter = ReadList(reader);
					}
					if (tag == "sortBefore") {
						properties.SortBefore = ReadList(reader);
					}
					if (tag == "author") {
						properties.Author = reader.ReadString();
					}
					if (tag == "version") {
						properties.Version = new Version(reader.ReadString());
					}
					if (tag == "displayName") {
						properties.DisplayName = reader.ReadString();
					}
					if (tag == "homepage") {
						properties.Homepage = reader.ReadString();
					}
					if (tag == "description") {
						properties.Description = reader.ReadString();
					}
					if (tag == "noCompile") {
						properties.NoCompile = true;
					}
					if (tag == "!hideCode") {
						properties.HideCode = false;
					}
					if (tag == "!hideResources") {
						properties.HideResources = false;
					}
					if (tag == "includeSource") {
						properties.IncludeSource = true;
					}
					if (tag == "includePDB") {
						properties.IncludePDB = true;
					}
					if (tag == "eacPath") {
						properties.EacPath = reader.ReadString();
					}
					if (tag == "side") {
						properties.Side = (ModSide)reader.ReadByte();
					}
					if (tag == "beta") {
						properties.Beta = true;
					}
					if (tag == "buildVersion") {
						properties.BuildVersion = new Version(reader.ReadString());
					}
				}
			}
			return properties;
		}

		public static void InfoToBuildTxt(Stream src, Stream dst) {
			BuildProperties properties = ReadFromStream(src);
			var sb = new StringBuilder();
			if (properties.DisplayName.Length > 0)
				sb.AppendLine($"displayName = {properties.DisplayName}");
			if (properties.Author.Length > 0)
				sb.AppendLine($"author = {properties.Author}");
			sb.AppendLine($"version = {properties.Version}");
			if (properties.Homepage.Length > 0)
				sb.AppendLine($"homepage = {properties.Homepage}");
			if (properties.DllReferences.Count > 0)
				sb.AppendLine($"dllReferences = {string.Join(", ", properties.DllReferences)}");
			if (properties.ModReferences.Count > 0)
				sb.AppendLine($"modReferences = {string.Join(", ", properties.ModReferences)}");
			if (properties.WeakReferences.Count > 0)
				sb.AppendLine($"weakReferences = {string.Join(", ", properties.WeakReferences)}");
			if (properties.NoCompile)
				sb.AppendLine($"noCompile = true");
			if (properties.HideCode)
				sb.AppendLine($"hideCode = true");
			if (properties.HideResources)
				sb.AppendLine($"hideResources = true");
			if (properties.IncludeSource)
				sb.AppendLine($"includeSource = true");
			if (properties.IncludePDB)
				sb.AppendLine($"includePDB = true");
			// buildIgnores isn't preserved in Info, but it doesn't matter with extraction since the ignored files won't be present anyway.
			// if (properties.buildIgnores.Length > 0)
			//	sb.AppendLine($"buildIgnores = {string.Join(", ", properties.buildIgnores)}");
			if (properties.Side != ModSide.Both)
				sb.AppendLine($"side = {properties.Side}");
			if (properties.SortAfter.Count > 0)
				sb.AppendLine($"sortAfter = {string.Join(", ", properties.SortAfter)}");
			if (properties.SortBefore.Count > 0)
				sb.AppendLine($"sortBefore = {string.Join(", ", properties.SortBefore)}");
			var bytes = Encoding.UTF8.GetBytes(sb.ToString());
			dst.Write(bytes, 0, bytes.Length);
		}

		public bool IgnoreFile(string resource) => BuildIgnores.Any(fileMask => FitsMask(resource, fileMask));

		private bool FitsMask(string fileName, string fileMask) {
			string pattern =
				'^' +
				Regex.Escape(fileMask.Replace(".", "__DOT__")
								 .Replace("*", "__STAR__")
								 .Replace("?", "__QM__"))
					 .Replace("__DOT__", "[.]")
					 .Replace("__STAR__", ".*")
					 .Replace("__QM__", ".")
				+ '$';
			return new Regex(pattern, RegexOptions.IgnoreCase).IsMatch(fileName);
		}
	}
}
