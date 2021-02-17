using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace Unwxpack
{
    public partial class MainForm : Form
    {
        private string wxAppFile = string.Empty;
        private string wxId = string.Empty;
        string[] PATH_SPLITOR = new string[] { "\\"};

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void btnSelectFile_Click(object sender, EventArgs e)
        {
            if(string.IsNullOrEmpty(txtFile.Text))
            {
                openFileDialog1.FileName = txtFile.Text;
            }
         
            if(openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                txtFile.Text = openFileDialog1.FileName;
                wxAppFile = txtFile.Text;
                ExtractWxid();
            }
        }

        private void ExtractWxid()
        {
            string[] dirS = wxAppFile.Split(PATH_SPLITOR, StringSplitOptions.None);
            if (dirS.Length >= 5)
            {
                if (dirS[dirS.Length - 1] == "__APP__.wxapkg" && dirS[dirS.Length - 4]=="Applet")
                {
                    txtWxid.Text = dirS[dirS.Length - 3];
                    wxId = txtWxid.Text;
                }
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            if(string.IsNullOrEmpty(txtFile.Text))
            {
                MessageBox.Show("请先选择文件！");
                return;
            }
            wxAppFile = txtFile.Text;
            if (CheckIsPcFile(wxAppFile))
            {
                if(string.IsNullOrEmpty(txtWxid.Text))
                {
                    MessageBox.Show("Pc版文件需要正确的wxid, wxid不能为空！");
                    return;
                }
                wxId = txtWxid.Text;
                string decryFile = DecryPcFile(wxAppFile);
                ExtractFile(decryFile, Application.StartupPath + $"\\{wxId}");
            }
            else
            {
                ExtractFile(wxAppFile, Application.StartupPath + $"\\{DateTime.Now.ToString("yyyyMMdd_hhmmss")}");
            }
        }

        private void Log(string message)
        {
            txtLog.AppendText(message + "\r\n");
        }

        private void ExtractFile(string fn, string targetDir)
        {
            if(!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            WxFileExtractor extractor = new WxFileExtractor();
            extractor.LogMessageHandle = Log;
            extractor.Extractor(fn, targetDir);

        }

        private string DecryPcFile(string srcFile)
        {
            //这些wxapkg文件的数据格式分成三个部分：

            //第一部分，文件的前6字节为V1MMWX；
            //第二部分，之后的1024字节为AES CBC加密数据；
            //第三部分，从1024 + 6之后的所有数据为异或加密的数据。
            //用UE等十六进制编辑器打开一个wxapkg，会很容易看到三个部分的界限。

            //文件第二部分的AES CBC加密，使用的key与对应的微信小程序id有关，也就是说每个微信小程序的wxapkg加密密钥是不同的。
            // key是由pbkdf2算法生成的，32字节，算法使用的哈希函数为sha1，pass为微信小程序id，salt为固定值“saltiest”，迭代次数为1000。
            //加密算法的iv是固定值“the iv: 16 bytes”，真够懒的。

            //文件第三部分的异或加密，真就是简单的异或，xorkey为微信小程序id的倒数第二字节的内容
            FileStream fs = new FileStream(wxAppFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            string decryFile = srcFile + ".decry";
            BinaryReader br = new BinaryReader(fs);
            br.BaseStream.Seek(6, SeekOrigin.Begin);
            byte[] aesData = br.ReadBytes(1024);
            byte[] key = Pbkdf2(wxId);
            Console.WriteLine(BitConverter.ToString(key));
            byte[] data = AesDecrypt(aesData, key);
            Console.WriteLine(BitConverter.ToString(data));
            if (File.Exists(decryFile))
            {
                File.Delete(decryFile);
            }

            FileStream newFs = new FileStream(decryFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite); ;
            newFs.Write(data, 0, data.Length);
            newFs.Flush();

            byte xorKey = 0x66;
            if (wxId.Length >= 2)
                xorKey = (byte)wxId[wxId.Length - 2];
            try
            {
                while (true)
                {
                    byte b = br.ReadByte();
                    newFs.WriteByte((byte)(b ^ xorKey));
                }
            }
            catch (Exception)
            {
            }
            newFs.Flush();
            newFs.Close();
            return decryFile;
        }

        public static byte[] Pbkdf2(string wxid)
        {
            string password = wxid;// "Mgen!";
            //salt default='saltiest')
            byte[] salt = Encoding.UTF8.GetBytes("saltiest");

            //默认以UTF8（无BOM）得到字节。嘻哈
            var kd = new Rfc2898DeriveBytes(password, salt);
            kd.IterationCount = 1000;
            //输出密钥1
            byte[] ret = kd.GetBytes(32);
            //Console.WriteLine(BitConverter.ToString(ret));
            //Console.WriteLine(BitConverter.ToString(ret));
            return ret;
        }

        public static byte[] AesDecrypt(byte[] data, byte[] key, string iv = "the iv: 16 bytes")
        {
            RijndaelManaged rm = new RijndaelManaged
            {
                Key = key,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
            rm.IV = Encoding.UTF8.GetBytes(iv);

            ICryptoTransform cTransform = rm.CreateDecryptor();
            Byte[] resultArray = cTransform.TransformFinalBlock(data, 0, data.Length);

            return resultArray;
        }

        private bool CheckIsPcFile(string wxAppFile)
        {
            FileStream fs = new FileStream(wxAppFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            BinaryReader br = new BinaryReader(fs);
            char[] cc = br.ReadChars(6);

            string s = new string(cc);
            bool isPc = (s == "V1MMWX");
            fs.Close();
            br.Close();
            return isPc;
        }
    }
}
