using System;
using System.Deployment.Application;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using JuliusSweetland.ETTA.Native;
using JuliusSweetland.ETTA.Native.Enums;
using JuliusSweetland.ETTA.Native.Structs;
using Microsoft.Win32;

namespace JuliusSweetland.ETTA.Static
{
    public static class DiagnosticInfo
    {
        private const string uacRegistryKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System";
        private const string uacRegistryValue = "EnableLUA";

        private const uint STANDARD_RIGHTS_READ = 0x00020000;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);

        public static string AssemblyVersion
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public static string AssemblyFileVersion
        {
            get
            {
                var attribute = System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(
                    typeof (System.Reflection.AssemblyFileVersionAttribute), false);

                if (attribute.Any())
                {
                    return ((System.Reflection.AssemblyFileVersionAttribute)(System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(
                        typeof(System.Reflection.AssemblyFileVersionAttribute), false).First())).Version;
                }

                return null;
            }
        }

        public static bool IsApplicationNetworkDeployed
        {
            get
            {
                return ApplicationDeployment.IsNetworkDeployed;
            }
        }

        public static string DeploymentVersion
        {
            get
            {
                return ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
            }
        }
    
        public static bool RunningAsAdministrator
        {
            get { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
        }

        public static string ProcessBitness
        {
            get { return Environment.Is64BitProcess ? "64-Bit" : "32-Bit"; }
        }

        public static string OperatingSystemBitness
        {
            get { return Environment.Is64BitOperatingSystem ? "64-Bit" : "32-Bit"; }
        }

        public static string OperatingSystemVersion
        {
            get
            {
                var vs = Environment.OSVersion.Version;

                switch (vs.Major)
                {
                    case 3:
                        return "Windows NT 3.51";

                    case 4:
                        return "Windows NT 4.0";

                    case 5:
                        if (vs.Minor == 0) return "Windows 2000";

                        if (vs.Minor == 1) return "Windows XP";

                        if (IsServerVersion())
                        {
                            if (WindowsAPI.GetSystemMetrics(89) == 0) return "Windows Server 2003";

                            return "Windows Server 2003 R2";
                        }

                        return "Windows XP";

                    case 6:
                        if (vs.Minor == 0)
                        {
                            if (IsServerVersion()) return "Windows Server 2008";

                            return "Windows Vista";
                        }

                        if (vs.Minor == 1)
                        {
                            if (IsServerVersion()) return "Windows Server 2008 R2";

                            return "Windows 7";
                        }

                        if (vs.Minor == 2) return "Windows 8";

                        if (IsServerVersion()) return "Windows Server 2012 R2";

                        return "Windows 8.1";
                }

                return "Unknown";
            }
        }

        public static string OperatingSystemServicePack
        {
            get
            {
                var os = new OSVERSIONINFO { dwOSVersionInfoSize = Marshal.SizeOf(typeof(OSVERSIONINFO)) };
                WindowsAPI.GetVersionEx(ref os); 
                return string.IsNullOrEmpty(os.szCSDVersion) ? "No Service Pack" : os.szCSDVersion; 
            }
        }

        public static bool IsProcessElevated
        {
            get
            {
                if (IsUacEnabled)
                {
                    IntPtr tokenHandle;
                    if (!WindowsAPI.OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_READ, out tokenHandle))
                    {
                        throw new ApplicationException("Could not get process token.  Win32 Error Code: " + Marshal.GetLastWin32Error());
                    }
                
                    try
                    {
                        var elevationResult = TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault;
                        int elevationResultSize = Marshal.SizeOf((int) elevationResult);
                        IntPtr elevationTypePtr = Marshal.AllocHGlobal(elevationResultSize);
                        
                        try
                        {
                            uint returnedSize;
                            var success = WindowsAPI.GetTokenInformation(
                                tokenHandle, TOKEN_INFORMATION_CLASS.TokenElevationType, elevationTypePtr, 
                                (uint) elevationResultSize, out returnedSize);

                            if (success)
                            {
                                elevationResult = (TOKEN_ELEVATION_TYPE) Marshal.ReadInt32(elevationTypePtr);
                                bool isProcessAdmin = elevationResult == TOKEN_ELEVATION_TYPE.TokenElevationTypeFull;
                                return isProcessAdmin;
                            }
                            else
                            {
                                throw new ApplicationException("Unable to determine the current elevation.");
                            }
                        }
                        finally
                        {
                            if (elevationTypePtr != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(elevationTypePtr);
                            }
                        }
                    }
                    finally
                    {
                        if (tokenHandle != IntPtr.Zero)
                        {
                            WindowsAPI.CloseHandle(tokenHandle);
                        }
                    }
                }
                else
                {
                    var identity = WindowsIdentity.GetCurrent();
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
        }
    
        private static bool IsServerVersion() 
        { 
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem")) 
            { 
                foreach (var managementObject in searcher.Get()) 
                { 
                    // ProductType will be one of: 
                    // 1: Workstation 
                    // 2: Domain Controller 
                    // 3: Server 
                    var productType = (uint)managementObject.GetPropertyValue("ProductType"); 
                    return productType != 1; 
                } 
            } 
            return false; 
        } 
    
        private static bool IsUacEnabled
        {
            get
            {
                var uacKey = Registry.LocalMachine.OpenSubKey(uacRegistryKey, false);
                return uacKey.GetValue(uacRegistryValue).Equals(1);
            }
        }
    }
}