﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clowd.Installer.Features
{
    public class DShowFilter : IFeature
    {
        public string InstallDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Constants.DirectShowAppName);

        public string[] FilterNames => new string[]
        {
            "loopback-audio-x86.dll",
            "loopback-audio-x64.dll",
            "UScreenCapture-x86.ax",
            "UScreenCapture-x64.ax",
        };

        public bool CheckInstalled(string assetPath)
        {
            return Directory.Exists(InstallDirectory) && Directory.EnumerateFiles(InstallDirectory).Any();
        }

        public void Install(string assetPath)
        {
            if (!SystemEx.IsProcessElevated)
            {
                Program.Elevate(true, this.GetType(), assetPath);
                return;
            }

            if (!Directory.Exists(InstallDirectory))
                Directory.CreateDirectory(InstallDirectory);

            StringBuilder revsvrUninstallCommands = new StringBuilder();

            foreach (var f in FilterNames)
            {
                if (f.Contains("x64") && !Environment.Is64BitOperatingSystem)
                    continue; // skip 64 assy on 32 bit systems

                var file = ResourcesEx.WriteResourceToFile(f, InstallDirectory);
                var code = regsvr32(true, file);
                if (code != 0)
                {
                    Uninstall(assetPath);
                    throw new Exception("regsvr32 returned non-zero exit code: " + code);
                }

                revsvrUninstallCommands.AppendLine(regsvr32_command(false, file));
            }

            //This works great, but it might be a good idea to move the cd command to the start (this insures that the path is also available to the elevated script -
            // otherwise the elevated script just runs from system32). You should also redirect the net command to nul to hide it's output: net session >nul 2>&1

            var unstallFileText = $@"
NET SESSION
IF %ERRORLEVEL% NEQ 0 GOTO ELEVATE
GOTO ADMINTASKS

:ELEVATE
CD /d %~dp0
MSHTA ""javascript: var shell = new ActiveXObject('shell.application'); shell.ShellExecute('%~nx0', '', '', 'runas', 1);close();""
EXIT

:ADMINTASKS
{revsvrUninstallCommands.ToString()}

reg delete ""HKEY_CURRENT_USER\{Constants.UninstallRegistryPath}\{Constants.DirectShowAppName}"" /f

start /b """" cmd /c rd /s /q ""%~dp0"" & msg * /self /w ""Uninstallation of {Constants.DirectShowAppName} has been successful""";

            var uninstallFilePath = Path.Combine(InstallDirectory, "uninstall.bat");

            File.WriteAllText(uninstallFilePath, unstallFileText);
            File.WriteAllText(Path.Combine(InstallDirectory, "readme.txt"), "Do not delete the files in this directory without first running the 'uninstall.bat' file." +
                "\r\nIt will unregister the assemblies from COM and then this folder can be safely deleted.");

            // write icon for uninstall programs list
            var programIcon = ResourcesEx.WriteResourceToFile("default.ico", InstallDirectory);

            var info = new ControlPanel.ControlPanelInfo()
            {
                DisplayName = Constants.DirectShowAppName,
                UninstallString = uninstallFilePath,
                InstallDirectory = InstallDirectory,
                DisplayIconPath = programIcon,
            };

            ControlPanel.ControlPanelInfo.Install(Constants.DirectShowAppName, info);
        }

        public bool NeedsPrivileges()
        {
            return true;
        }

        public void Uninstall(string assetPath)
        {
            if (!CheckInstalled(assetPath))
                return;

            if (!SystemEx.IsProcessElevated)
            {
                Program.Elevate(false, this.GetType(), assetPath);
                return;
            }

            string[] getBinaries() => Directory.GetFiles(InstallDirectory).Where(f => f.EndsWith("dll") || f.EndsWith("ax")).ToArray();

            if (Directory.Exists(InstallDirectory))
                foreach (var f in getBinaries())
                    if (0 == regsvr32(false, f))
                        File.Delete(f);

            var files = getBinaries();
            if (!files.Any())
                Directory.Delete(InstallDirectory, true);
            else
                throw new Exception("regsvr32 unable to uninstall files: \n" + String.Join("\n", files));

            ControlPanel.ControlPanelInfo.Uninstall(Constants.DirectShowAppName);
        }

        private int regsvr32(bool install, string filepath)
        {
            var uflag = install ? "" : "/u ";
            var p = Process.Start("regsvr32", $"/s {uflag}\"{filepath}\"");
            p.WaitForExit();
            return p.ExitCode;
        }

        private string regsvr32_command(bool install, string filepath)
        {
            var uflag = install ? "" : "/u ";
            return $"regsvr32 /s {uflag}\"{filepath}\"";
        }
    }
}
