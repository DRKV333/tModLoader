using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;

namespace ModBuildTasks
{
	public class TmodDetectTmlVersion : Task
	{
		[Required]
		public string TMLPath { get; set; }

		[Required]
		public string CacheFilePath { get; set; }

		[Output]
		public string Version { get; set; }

		public override bool Execute()
		{
			if (File.Exists(CacheFilePath)) {
				using (StreamReader sr = new StreamReader(CacheFilePath)) {
					Version = sr.ReadLine();
				}
				return true;
			}

			// TODO: Actually look at the tML executable to determine version.

			using (StreamWriter sw = new StreamWriter(CacheFilePath)) {
				sw.WriteLine("0.11.7.5");
				Version = "0.11.7.5";
			}

			return true;
		}
	}
}