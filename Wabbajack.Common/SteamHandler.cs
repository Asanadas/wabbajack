using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace Wabbajack.Common
{
    public class SteamGame
    {
        public int AppId;
        public string Name;
        public string InstallDir;
    }

    /// <summary>
    /// Class for all Steam operations
    /// </summary>
    public class SteamHandler
    {
        private const string SteamRegKey = @"Software\Valve\Steam";

        /// <summary>
        /// Path to the Steam folder
        /// </summary>
        public string SteamPath { get; internal set; }
        /// <summary>
        /// HashSet of all known Steam Libraries
        /// </summary>
        public HashSet<string> InstallFolders { get; internal set; }
        /// <summary>
        /// HashSet of all known SteamGames
        /// </summary>
        public HashSet<SteamGame> Games { get; internal set; }

        private string SteamConfig => Path.Combine(SteamPath, "config", "config.vdf");

<<<<<<< HEAD
        public SteamHandler(bool init)
        {
            var steamKey = Registry.CurrentUser.OpenSubKey(SteamRegKey);
            SteamPath = steamKey?.GetValue("SteamPath").ToString();
            if(!init) return;
            LoadInstallFolders();
            LoadAllSteamGames();
=======
        public SteamHandler()
        {
            var steamKey = Registry.CurrentUser.OpenSubKey(SteamRegKey);
            SteamPath = steamKey?.GetValue("SteamPath").ToString();
>>>>>>> Created SteamHandler
        }

        /// <summary>
        /// Finds the installation path of a Steam game by ID
        /// </summary>
        /// <param name="id">ID of the Steam game</param>
        /// <returns></returns>
        public string GetGamePathById(int id)
        {
            return Games.FirstOrDefault(f => f.AppId == id)?.InstallDir;
        }

        /// <summary>
        /// Reads the config file and adds all found installation folders to the HashSet
        /// </summary>
        public void LoadInstallFolders()
        {
            var paths = new HashSet<string>();

            File.ReadLines(SteamConfig, Encoding.UTF8).Do(l =>
            {
                if (!l.Contains("BaseInstallFolder_")) return;
<<<<<<< HEAD
                var s = GetVdfValue(l);
                s = Path.Combine(s, "steamapps");
                paths.Add(s);
=======
                paths.Add(GetVdfValue(l));
>>>>>>> Created SteamHandler
            });

            InstallFolders = paths;
        }

        /// <summary>
        /// Enumerates through all Steam Libraries to find and read all .afc files, adding the found game
        /// to the HashSet
        /// </summary>
        public void LoadAllSteamGames()
        {
            var games = new HashSet<SteamGame>();

            InstallFolders.Do(p =>
            {
                Directory.EnumerateFiles(p, "*.acf", SearchOption.TopDirectoryOnly).Do(f =>
                {
                    var steamGame = new SteamGame();
                    File.ReadAllLines(f, Encoding.UTF8).Do(l =>
                    {
                        if(l.Contains("\"appid\""))
                            steamGame.AppId = int.Parse(GetVdfValue(l));
                        if (l.Contains("\"name\""))
                            steamGame.Name = GetVdfValue(l);
                        if (l.Contains("\"installdir\""))
                            steamGame.InstallDir = Path.Combine(p, "common", GetVdfValue(l));
                    });

                    games.Add(steamGame);
                });
            });

            Games = games;
        }

        private static string GetVdfValue(string line)
        {
            var trim = line.Trim('\t').Replace("\t", "");
            string[] s = trim.Split('\"');
            return s[3].Replace("\\\\", "\\");
        }
    }
}
