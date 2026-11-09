using System.Runtime.InteropServices;
using System.Text;

namespace VpnConnector;

internal static class VpnService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RasDialParams
    {
        public uint size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        public string entryName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
        public string phoneNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
        public string callbackNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        public string userName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        public string password;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string domain;
        public uint subEntry;
        public UIntPtr callbackId;
        public uint ifIndex;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RasEntryName
    {
        public uint size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        public string entryName;
        public uint flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
        public string phonebookPath;
    }

    [DllImport("rasapi32.dll", EntryPoint = "RasEnumEntriesW", CharSet = CharSet.Unicode)]
    private static extern uint RasEnumEntries(string? reserved, string? phonebook, IntPtr entries, ref uint bufferSize, out uint count);

    [DllImport("rasapi32.dll", EntryPoint = "RasDialW", CharSet = CharSet.Unicode)]
    private static extern uint RasDial(IntPtr extensions, string? phonebook, ref RasDialParams parameters, uint notifierType, IntPtr notifier, out IntPtr connection);

    [DllImport("rasapi32.dll", EntryPoint = "RasHangUpW")]
    private static extern uint RasHangUp(IntPtr connection);

    [DllImport("rasapi32.dll", EntryPoint = "RasGetErrorStringW", CharSet = CharSet.Unicode)]
    private static extern uint RasGetErrorString(uint error, StringBuilder buffer, uint bufferSize);

    public static uint Connect(string vpnName, string userName, string password, string otp)
    {
        var credential = $"{password}:{otp}";
        if (vpnName.Length is < 1 or > 256 || userName.Length is < 1 or > 256 || credential.Length > 256)
            throw new ArgumentException("VPN名または認証情報がRAS APIの文字数制限を超えています。");
        var size = Marshal.SizeOf<RasDialParams>();
        if (size != 2120)
            throw new InvalidOperationException($"RASDIALPARAMSのサイズが想定外です: {size} (x64版のみ対応)");
        var parameters = new RasDialParams
        {
            size = (uint)size,
            entryName = vpnName,
            phoneNumber = "",
            callbackNumber = "",
            userName = userName,
            password = credential,
            domain = ""
        };
        var error = RasDial(IntPtr.Zero, null, ref parameters, 0, IntPtr.Zero, out var connection);
        if (error != 0 && connection != IntPtr.Zero)
            RasHangUp(connection);
        return error;
    }

    public static string GetErrorMessage(uint error)
    {
        var buffer = new StringBuilder(512);
        return RasGetErrorString(error, buffer, (uint)buffer.Capacity) == 0 ? buffer.ToString() : $"エラーコード: {error}";
    }

    public static string[] GetConnectionNames()
    {
        uint bufferSize = 0;
        var error = RasEnumEntries(null, null, IntPtr.Zero, ref bufferSize, out _);
        if (error == 0)
            return [];
        if (error != 603)
            throw new InvalidOperationException($"接続一覧の取得に失敗しました（エラー {error}）: {GetErrorMessage(error)}");
        var entrySize = Marshal.SizeOf<RasEntryName>();
        if (bufferSize < (uint)entrySize)
            throw new InvalidOperationException("RAS接続一覧のバッファサイズが不正です。");
        var buffer = Marshal.AllocHGlobal(checked((int)bufferSize));
        try
        {
            Marshal.WriteInt32(buffer, entrySize);
            error = RasEnumEntries(null, null, buffer, ref bufferSize, out var count);
            if (error != 0)
                throw new InvalidOperationException($"接続一覧の取得に失敗しました（エラー {error}）: {GetErrorMessage(error)}");
            if ((ulong)count * (uint)entrySize > bufferSize)
                throw new InvalidOperationException("RAS接続一覧のデータサイズが不正です。");
            var names = new List<string>(checked((int)count));
            for (var i = 0; i < (int)count; i++)
            {
                var entry = Marshal.PtrToStructure<RasEntryName>(IntPtr.Add(buffer, checked(i * entrySize)));
                if (!string.IsNullOrWhiteSpace(entry.entryName))
                    names.Add(entry.entryName);
            }
            return names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}