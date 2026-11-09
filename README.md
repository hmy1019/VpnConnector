
# VpnConnector
Windowsに登録済みのVPNへ、ワンタイムパスワード(OTP)を使って接続するための補助ツールです。
固定パスワードをWindows資格情報マネージャーに保存し、接続時にはOTPのみを入力できます。

## 動作環境
- Windows 10/11 x64
- WindowsにVPN接続が登録されていること
- `固定パスワード:OTP` の形式で認証するVPNであること

SingleID + YAMAHA RTXの構成などで使用できます。

## Download
[Download latest release](https://github.com/hmy1019/VpnConnector/releases/latest)

## 使い方

### 初回設定
1. `VpnConnector.exe` を起動します。
2. 「認証情報の変更」をクリックします。
3. VPN接続名を選択します。
4. ユーザー名と固定パスワードを入力します。
5. 「登録」をクリックします。

### VPN接続
1. `VpnConnector.exe` を起動します。
2. 認証アプリに表示されたOTPを入力します。
3. 「接続」をクリックします。

接続成功後、アプリは自動的に終了します。
VPNの切断はWindows標準のVPN設定から行ってください。

### 認証情報の変更
「認証情報の変更」から再登録できます。
パスワードを空欄にした場合、同じVPN接続名に保存されているパスワードを維持します。
VPN接続名を変更する場合は、パスワードも入力してください。

## 認証情報の保存先
- 接続名・ユーザー名: `%LOCALAPPDATA%\VpnConnector\settings.ini`
- 固定パスワード: Windows資格情報マネージャー
- 資格情報のキー: `VpnConnector/<VPN接続名>`

固定パスワードはINIファイルには保存されません。
OTPおよびOTP生成用の秘密鍵も保存しません。

## 注意事項
- 本ツールはWindowsに登録済みのVPN接続を利用します。VPN接続の作成や設定変更は行いません。
- 対応する認証形式以外での動作は保証しません。
- 接続できない場合は、Windows標準のVPN接続機能を利用してください。
- 本ツールは非公式の補助ツールであり、VPNサービスの提供元とは関係ありません。

## アンインストール
1. `VpnConnector.exe` を削除します。
2. 必要に応じて設定ファイルを削除します。
3. Windows資格情報マネージャーから `VpnConnector/` で始まる資格情報を削除します。

Windowsに登録済みのVPN接続設定は削除されません。