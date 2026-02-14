using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace GlassDFIR.Services
{
    public class VssService : IVssService
    {
        public string? CreateSnapshot(string volume)
        {
            try
            {
                // Ensure volume is in 'C:\' format for the command
                if (!volume.EndsWith("\\")) volume += "\\";

                // Try Get-CimInstance first (modern PowerShell), fall back to Get-WmiObject (legacy)
                string? shadowId = TryCreateSnapshotCim(volume);
                if (shadowId != null) return shadowId;

                shadowId = TryCreateSnapshotWmi(volume);
                return shadowId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VSS Creation Error: {ex.Message}");
            }
            return null;
        }

        private string? TryCreateSnapshotCim(string volume)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-Command \"Invoke-CimMethod -ClassName Win32_ShadowCopy -MethodName Create -Arguments @{{Volume='{volume}';Context='ClientAccessible'}}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                Debug.WriteLine($"VSS CIM Create Output:\n{output}");
                if (!string.IsNullOrEmpty(error))
                    Debug.WriteLine($"VSS CIM Create StdErr:\n{error}");

                if (Regex.IsMatch(output, @"ReturnValue\s*[:=]\s*0"))
                {
                    var match = Regex.Match(output, @"ShadowID\s*[:=]\s*{(.*?)}");
                    if (match.Success)
                    {
                        return "{" + match.Groups[1].Value + "}";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VSS CIM Creation failed, will try WMI: {ex.Message}");
            }
            return null;
        }

        private string? TryCreateSnapshotWmi(string volume)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-Command \"(Get-WmiObject -List Win32_ShadowCopy).Create('{volume}', 'ClientAccessible')\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                Debug.WriteLine($"VSS WMI Create Output:\n{output}");
                if (!string.IsNullOrEmpty(error))
                    Debug.WriteLine($"VSS WMI Create StdErr:\n{error}");

                if (Regex.IsMatch(output, @"ReturnValue\s*[:=]\s*0"))
                {
                    var match = Regex.Match(output, @"ShadowID\s*[:=]\s*{(.*?)}");
                    if (match.Success)
                    {
                        return "{" + match.Groups[1].Value + "}";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VSS WMI Creation Error: {ex.Message}");
            }
            return null;
        }

        public string? GetSnapshotVolume(string shadowId)
        {
            // Try vssadmin first, then fall back to WMI query
            string? volume = TryGetVolumeVssadmin(shadowId);
            if (volume != null) return volume;

            volume = TryGetVolumeWmi(shadowId);
            return volume;
        }

        private string? TryGetVolumeVssadmin(string shadowId)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "vssadmin",
                        Arguments = $"list shadows /Shadow={shadowId}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                Debug.WriteLine($"vssadmin list shadows output:\n{output}");
                if (!string.IsNullOrEmpty(error))
                    Debug.WriteLine($"vssadmin StdErr:\n{error}");

                // The actual field name is "Shadow Copy Volume:" (not "Shadow Copy Volume Name:")
                var match = Regex.Match(output, @"Shadow Copy Volume:\s*([^\r\n]+)");
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VSS vssadmin Resolution Error: {ex.Message}");
            }
            return null;
        }

        private string? TryGetVolumeWmi(string shadowId)
        {
            try
            {
                // Query Win32_ShadowCopy by ID to get DeviceObject directly
                string escapedId = shadowId.Replace("'", "''");
                string psCommand = $"(Get-CimInstance -ClassName Win32_ShadowCopy | Where-Object {{ $_.ID -eq '{escapedId}' }}).DeviceObject";

                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-Command \"{psCommand}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd().Trim();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                Debug.WriteLine($"VSS WMI DeviceObject query output: '{output}'");
                if (!string.IsNullOrEmpty(error))
                    Debug.WriteLine($"VSS WMI DeviceObject StdErr:\n{error}");

                if (!string.IsNullOrEmpty(output) && output.Contains("HarddiskVolumeShadowCopy", StringComparison.OrdinalIgnoreCase))
                {
                    return output;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VSS WMI Resolution Error: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Creates a temporary directory symlink to a VSS device path.
        /// Robocopy cannot access \\?\GLOBALROOT\Device\... paths directly,
        /// so we create a symlink and copy through it instead.
        /// </summary>
        public string? CreateSymlinkToVolume(string devicePath)
        {
            try
            {
                // Ensure the device path ends with a backslash for the symlink target
                if (!devicePath.EndsWith("\\")) devicePath += "\\";

                string symlinkPath = Path.Combine(Path.GetTempPath(), $"GlassDFIR_VSS_{Guid.NewGuid():N}");

                // Clean up if somehow exists
                if (Directory.Exists(symlinkPath))
                {
                    try { Directory.Delete(symlinkPath, false); } catch { }
                }

                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c mklink /d \"{symlinkPath}\" \"{devicePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                proc.WaitForExit();

                Debug.WriteLine($"mklink output: {output}");
                if (!string.IsNullOrEmpty(error))
                    Debug.WriteLine($"mklink StdErr: {error}");

                if (proc.ExitCode == 0 && Directory.Exists(symlinkPath))
                {
                    Debug.WriteLine($"VSS symlink created: {symlinkPath} -> {devicePath}");
                    return symlinkPath;
                }
                else
                {
                    Debug.WriteLine($"mklink failed with exit code {proc.ExitCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VSS Symlink Error: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Removes a temporary VSS symlink created by CreateSymlinkToVolume.
        /// </summary>
        public void CleanupSymlink(string symlinkPath)
        {
            try
            {
                if (Directory.Exists(symlinkPath))
                {
                    // Use rmdir to remove the symlink without deleting the target
                    var proc = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c rmdir \"{symlinkPath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    proc.Start();
                    proc.WaitForExit();
                    Debug.WriteLine($"VSS symlink removed: {symlinkPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VSS Symlink Cleanup Error: {ex.Message}");
            }
        }

        public void DeleteSnapshot(string shadowId)
        {
            try
            {
                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "vssadmin",
                        Arguments = $"delete shadows /Shadow={shadowId} /Quiet",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                proc.WaitForExit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VSS Deletion Error: {ex.Message}");
            }
        }
    }
}
