using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace VpnConnector;

internal sealed record VpnSettings(string ConnectionName, string UserName);

internal static class LocalSettings
{
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VpnConnector", "settings.ini");

    public static VpnSettings Load()
    {
        if (!File.Exists(FilePath))
            return new VpnSettings("", "");
        var connectionName = "";
        var userName = "";
        var inVpnSection = false;
        foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
        {
            var value = line.Trim();
            if (value.StartsWith('[') && value.EndsWith(']'))
            {
                inVpnSection = value.Equals("[VPN]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inVpnSection)
                continue;
            var separator = line.IndexOf('=');
            if (separator < 0)
                continue;
            var key = line[..separator].Trim();
            var entry = line[(separator + 1)..].Trim();
            if (key.Equals("ConnectionName", StringComparison.OrdinalIgnoreCase))
                connectionName = entry;
            else if (key.Equals("UserName", StringComparison.OrdinalIgnoreCase))
                userName = entry;
        }
        return new VpnSettings(connectionName, userName);
    }

    public static void Save(VpnSettings settings)
    {
        if (settings.ConnectionName.Contains('\n') || settings.ConnectionName.Contains('\r') || settings.UserName.Contains('\n') || settings.UserName.Contains('\r'))
            throw new ArgumentException("設定値に改行を含めることはできません。");
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, string.Join(Environment.NewLine, "[VPN]", $"ConnectionName={settings.ConnectionName}", $"UserName={settings.UserName}", ""), Encoding.UTF8);
    }
}

internal sealed class MainForm : Form
{
    private readonly TextBox otpBox = new() { Width = 245, ImeMode = ImeMode.Disable };
    private readonly Button settingsButton = new() { Text = "認証情報の変更", AutoSize = true, MinimumSize = new Size(0, 32) };
    private readonly Button connectButton = new() { Text = "接続", AutoSize = true, MinimumSize = new Size(82, 32) };
    private VpnSettings settings = new("", "");

    public MainForm()
    {
        Text = "VPN接続";
        Font = SystemFonts.MessageBoxFont;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 2, Padding = new Padding(14), GrowStyle = TableLayoutPanelGrowStyle.FixedSize };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Anchor = AnchorStyles.Right, Margin = new Padding(0, 8, 0, 0) };
        actions.Controls.AddRange([connectButton, settingsButton]);
        layout.Controls.Add(new Label { Text = "OTP", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(5) }, 0, 0);
        layout.Controls.Add(otpBox, 1, 0);
        layout.Controls.Add(actions, 1, 1);
        Controls.Add(layout);
        AcceptButton = connectButton;
        settings = LocalSettings.Load();
        settingsButton.Click += (_, _) => OpenSettings();
        connectButton.Click += async (_, _) => await ConnectAsync();
        Shown += (_, _) =>
        {
            if (settings.ConnectionName.Length == 0 || settings.UserName.Length == 0)
                OpenSettings();
            otpBox.Focus();
        };
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(settings);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            settings = LocalSettings.Load();
    }

    private async Task ConnectAsync()
    {
        var otp = otpBox.Text;
        if (settings.ConnectionName.Length == 0 || settings.UserName.Length == 0)
        {
            OpenSettings();
            return;
        }
        string? password;
        try
        {
            password = CredentialService.Load(settings.ConnectionName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "資格情報の読み込みに失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (password is null)
        {
            MessageBox.Show(this, "固定パスワードが未登録です。", "入力確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenSettings();
            return;
        }
        settingsButton.Enabled = false;
        connectButton.Enabled = false;
        connectButton.Text = "接続中...";
        try
        {
            var result = await Task.Run(() => VpnService.Connect(settings.ConnectionName, settings.UserName, password, otp));
            if (result == 0)
            {
                Close();
                return;
            }
            MessageBox.Show(this, VpnService.GetErrorMessage(result), $"VPNエラー {result}", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "VPN接続エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            settingsButton.Enabled = true;
            connectButton.Enabled = true;
            connectButton.Text = "接続";
            otpBox.Clear();
        }
        otpBox.Focus();
    }
}

internal sealed class SettingsForm : Form
{
    private readonly ComboBox vpnBox = new() { Width = 245, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox userBox = new() { Width = 245 };
    private readonly TextBox passwordBox = new() { Width = 245, UseSystemPasswordChar = true };
    private readonly Button registerButton = new() { Text = "登録", AutoSize = true, MinimumSize = new Size(82, 32) };
    private readonly VpnSettings originalSettings;

    public SettingsForm(VpnSettings settings)
    {
        originalSettings = settings;
        Text = "認証情報の変更";
        Font = SystemFonts.MessageBoxFont;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 4, Padding = new Padding(14), GrowStyle = TableLayoutPanelGrowStyle.FixedSize };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (var i = 0; i < 4; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var actions = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Anchor = AnchorStyles.Right, Margin = new Padding(0, 8, 0, 0) };
        actions.Controls.Add(registerButton);
        layout.Controls.Add(new Label { Text = "接続名", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(5) }, 0, 0);
        layout.Controls.Add(vpnBox, 1, 0);
        layout.Controls.Add(new Label { Text = "ユーザー名", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(5) }, 0, 1);
        layout.Controls.Add(userBox, 1, 1);
        layout.Controls.Add(new Label { Text = "パスワード", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(5) }, 0, 2);
        layout.Controls.Add(passwordBox, 1, 2);
        layout.Controls.Add(actions, 1, 3);
        Controls.Add(layout);
        userBox.Text = settings.UserName;
        AcceptButton = registerButton;
        registerButton.Click += (_, _) => Register();
        Shown += (_, _) => LoadConnections();
    }

    private void LoadConnections()
    {
        try
        {
            var names = VpnService.GetConnectionNames();
            if (names.Length == 0)
            {
                MessageBox.Show(this, "Windowsに接続が登録されていません。", "接続名の取得", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }
            vpnBox.Items.AddRange(names.Cast<object>().ToArray());
            var index = Array.FindIndex(names, name => string.Equals(name, originalSettings.ConnectionName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                vpnBox.SelectedIndex = index;
            else if (names.Length == 1)
                vpnBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "接続名の取得に失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private void Register()
    {
        var vpnName = vpnBox.SelectedItem as string;
        var userName = userBox.Text.Trim();
        if (vpnName is null || userName.Length == 0)
        {
            MessageBox.Show(this, "接続名とユーザー名を入力してください。", "入力確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            if (passwordBox.Text.Length == 0 && !CredentialService.Exists(vpnName))
            {
                MessageBox.Show(this, "固定パスワードが未登録です。入力してください。", "入力確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (passwordBox.Text.Length > 0)
                CredentialService.Save(vpnName, passwordBox.Text);
            LocalSettings.Save(new VpnSettings(vpnName, userName));
            if (originalSettings.ConnectionName.Length > 0 && !string.Equals(originalSettings.ConnectionName, vpnName, StringComparison.OrdinalIgnoreCase))
                CredentialService.Delete(originalSettings.ConnectionName);
            passwordBox.Clear();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "認証情報の登録に失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}