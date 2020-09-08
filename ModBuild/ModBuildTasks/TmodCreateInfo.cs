using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace ModBuildTasks
{
	public class TmodCreateInfo : Task
	{
		[Required]
		public string InfoOutputPath { get; set; }

		[Required]
		public string TMLVersion { get; set; }

		//TODO: Mod/Dll references.
		public string Author { get; set; } = "";
		public string Version { get; set; } = "1.0";
		public string DisplayName { get; set; } = "";
		public bool HideCode { get; set; } = false;
		public bool HideResources { get; set; } = false;
		//TODO: Include source
		//TODO: Include PDB
		//TODO: EAC
		public string Homepage { get; set; } = "";
		public string Description { get; set; } = "";
		public string Side { get; set; } = "Both";

		public override bool Execute()
		{
			//TODO: Detect beta version.
			BuildProperties props = new BuildProperties(System.Version.Parse(TMLVersion), false) {
				Author = Author,
				Version = System.Version.Parse(Version),
				DisplayName = DisplayName,
				HideCode = HideCode,
				HideResources = HideResources,
				Homepage = Homepage,
				Description = Description,
			};

			if (!Enum.TryParse(Side, true, out ModSide side))
				throw new ArgumentException("side is not one of (Both, Client, Server, NoSync): " + Side);

			props.Side = side;

			using (FileStream fs = new FileStream(InfoOutputPath, FileMode.Create, FileAccess.Write)) {
				byte[] infoBytes = props.ToBytes();
				fs.Write(infoBytes, 0, infoBytes.Length);
			}

			return true;
		}
	}
}