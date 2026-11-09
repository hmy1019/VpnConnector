using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VpnConnector;

internal static class CredentialService
{
    private const uint CredentialType = 1;
    private const uint CredentialPersist = 2;
    private const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string targetName, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string targetName, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr credential);

    private static string TargetName(string vpnName) => $"VpnConnector/{vpnName}";

    public static void Save(string vpnName, string password)
    {
        if (string.IsNullOrEmpty(password) || password.Contains('\0') || password.Length * sizeof(char) > 2560)
            throw new ArgumentException("パスワードの形式または長さが不正です。");
        var blob = Marshal.StringToCoTaskMemUni(password);
        try
        {
            var credential = new Credential
            {
                Type = CredentialType,
                TargetName = TargetName(vpnName),
                CredentialBlobSize = (uint)(password.Length * sizeof(char)),
                CredentialBlob = blob,
                Persist = CredentialPersist
            };
            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(blob);
        }
    }

    public static string? Load(string vpnName)
    {
        if (!CredRead(TargetName(vpnName), CredentialType, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return null;
            throw new Win32Exception(error);
        }
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.CredentialBlobSize % sizeof(char) != 0 || credential.CredentialBlobSize > 2560)
                throw new InvalidOperationException("保存された資格情報の形式が不正です。");
            return Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / sizeof(char)) ?? "";
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public static bool Exists(string vpnName)
    {
        if (CredRead(TargetName(vpnName), CredentialType, 0, out var pointer))
        {
            CredFree(pointer);
            return true;
        }
        var error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
            return false;
        throw new Win32Exception(error);
    }

    public static void Delete(string vpnName)
    {
        if (!CredDelete(TargetName(vpnName), CredentialType, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
                throw new Win32Exception(error);
        }
    }
}