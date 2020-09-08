using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Terraria.ModLoader.Core;

namespace ModBuildTasks
{
	public class TmodPackage : Task
	{
		[Required]
		public string ProjectDirectory { get; set; }

		[Required]
		public ITaskItem[] ContentFiles { get; set; }

		public string Name { get; set; } = null;
		public string Version { get; set; } = null;

		[Required]
		public string TMLVersion { get; set; }

		[Required]
		public string TmodOutputPath { get; set; }

		public override bool Execute()
		{
			ProjectDirectory = Path.GetFullPath(ProjectDirectory);

			TmodFile file = new TmodFile(TmodOutputPath, System.Version.Parse(TMLVersion), Name, System.Version.Parse(Version));

			foreach (var item in ContentFiles) {
				string fileName = item.GetMetadata("TmodFileName");
				if (fileName == null || fileName == string.Empty) {
					string fullPath = Path.GetFullPath(item.ItemSpec);

					if (fullPath.StartsWith(ProjectDirectory))
						fileName = fullPath.Substring(ProjectDirectory.Length + 1);
					else
						fileName = Path.GetFileName(fullPath);
				}
					

				file.AddFile(fileName, File.ReadAllBytes(item.ItemSpec));
			}

			file.Save();

			return true;
		}
	}
}
