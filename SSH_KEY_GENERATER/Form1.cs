using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace SSH_KEY_GENERATER
{
    public partial class Form1 : Form
    {
        private string save_base_path;
        const int KEY_SIZE = 4096; //RSAキーサイズ

        public Form1()
        {
            InitializeComponent();
            //大きさ固定
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            //フォームが最大化されないようにする
            this.MaximizeBox = false;
            //フォームが最小化されないようにする
            this.MinimizeBox = false;

            Txt_UserName.Text = string.Empty;
            Txt_UserName.ImeMode = ImeMode.Disable;

            //カレントディレクトリを初期値にする。
             save_base_path = Environment.CurrentDirectory;

            //自分自身のAssemblyを取得
            System.Reflection.Assembly asm =
            System.Reflection.Assembly.GetExecutingAssembly();
            //バージョンの取得
            System.Version ver = asm.GetName().Version;
            this.Text = "SSHキージェネレータ　" + "Ver " + ver;

        }

        private void Btn_Generate_Click(object sender, EventArgs e)
        {
            DialogResult ret;
            if (string.IsNullOrEmpty(Txt_UserName.Text))
            {
                MessageBox.Show("ユーザ名を入れてください", "エラー",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            //ファイルダイアログ表示
            //https://dobon.net/vb/dotnet/form/folderdialog.htmlより
            //FolderBrowserDialogクラスのインスタンスを作成
            FolderBrowserDialog fbd = new FolderBrowserDialog()
            {
                //上部に表示する説明テキストを指定する
                Description = "SSHキーを保存するフォルダを指定してください。",
                //ルートフォルダを指定する
                //デフォルトでDesktop
                RootFolder = Environment.SpecialFolder.Desktop,
                //最初に選択するフォルダを指定する
                //RootFolder以下にあるフォルダである必要がある
                SelectedPath = save_base_path,
                //ユーザーが新しいフォルダを作成できるようにする
                //デフォルトでTrue
                ShowNewFolderButton = true
            };

            //ダイアログを表示する
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                //選択されたフォルダを表示する
                save_base_path = fbd.SelectedPath;
            }

            //ユーザ名セット
            string username = Txt_UserName.Text;

            //既にファイルがあるかチェック
            if (File.Exists(save_base_path + "\\" + username + "_id_rsa") ||
                File.Exists(save_base_path + "\\" + username + "_id_rsa.pub") ||
                File.Exists(save_base_path + "\\" + username + "_id_rsa.ppk"))
            {
                ret = MessageBox.Show("既にファイルがあります。上書きしますか？", "確認", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);
                if (ret == DialogResult.Cancel)
                {
                    return;
                }
            }
            ret = MessageBox.Show(KEY_SIZE + "bitのキーを生成します。よろしいですか？", "確認", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);
            if (ret == DialogResult.Cancel)
            {
                return;
            }
            try
            {
                //フォーム全体を無効化
                this.Enabled = false;

                //指定したビット数で非対称キー作成
                //http://kagasu.hatenablog.com/entry/2017/02/02/132741より
                var rsa = new RSACryptoServiceProvider(KEY_SIZE);
                var parameter = rsa.ExportParameters(true);
                //秘密鍵をPEM形式で取り出す
                var privateKey = RsaPemMaker.GetPrivatePemString(parameter);
                File.WriteAllText(save_base_path + "\\" + username + "_id_rsa", privateKey);
                //Console.WriteLine(privateKey);
                //公開鍵をPEM形式で取り出す
                var publicKey = RsaPemMaker.GetPublicPemString(parameter);
                //File.WriteAllText(save_base_path + "\\" + username + "_public-key.pem", publicKey);
                //Console.WriteLine(publicKey);

                //公開鍵をRSA形式で取り出す
                //https://stackoverflow.com/questions/15457710/converting-an-rsa-public-key-into-a-rfc-4716-public-key-with-bouncy-castleより
                //pemをrsaに変換する部分のみ抜粋
                using (StringReader sr = new StringReader(publicKey))
                {
                    PemReader reader = new PemReader(sr);
                    RsaKeyParameters r = (RsaKeyParameters)reader.ReadObject();
                    byte[] sshrsa_bytes = Encoding.Default.GetBytes("ssh-rsa");
                    byte[] n = r.Modulus.ToByteArray();
                    byte[] ee = r.Exponent.ToByteArray();

                    string buffer64;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(ToBytes(sshrsa_bytes.Length), 0, 4);
                        ms.Write(sshrsa_bytes, 0, sshrsa_bytes.Length);
                        ms.Write(ToBytes(ee.Length), 0, 4);
                        ms.Write(ee, 0, ee.Length);
                        ms.Write(ToBytes(n.Length), 0, 4);
                        ms.Write(n, 0, n.Length);
                        ms.Flush();
                        buffer64 = Convert.ToBase64String(ms.ToArray());
                    }

                    var PublicSSH = string.Format("ssh-rsa {0} " + username, buffer64);
                    //Console.WriteLine(PublicSSH);
                    File.WriteAllText(save_base_path + "\\" + username + "_id_rsa.pub", PublicSSH);
                }
                //キーペアをPPKに変換
                var hoge = PuttyKeyFileGenerator.RSAToPuttyPrivateKey(parameter);
                //Console.WriteLine(hoge);
                File.WriteAllText(save_base_path + "\\" + username + "_id_rsa.ppk", hoge);

                MessageBox.Show("生成完了!");
                return;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("内部エラーが発生しました：" + ex.Message);
                return;
            }
            finally
            {
                fbd.Dispose();
                this.Enabled = true;
            }

        }
        private static byte[] ToBytes(int i)
        {
            byte[] bts = BitConverter.GetBytes(i);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bts);
            }
            return bts;
        }
    }
}
