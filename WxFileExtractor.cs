using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Unwxpack
{
    

    #region WXAPKG 文件结构

    [StructLayout(LayoutKind.Explicit)]
    public class WXAPKG_HEADER
    {
        [MarshalAs(UnmanagedType.U1), FieldOffset(0)]
        internal byte m_first_mark;
        [MarshalAs(UnmanagedType.I4), FieldOffset(1)]
        internal int m_edition;
        [MarshalAs(UnmanagedType.I4), FieldOffset(5)]
        internal int m_index_info_length;
        [MarshalAs(UnmanagedType.I4), FieldOffset(9)]
        internal int m_body_info_length;
        [MarshalAs(UnmanagedType.U1), FieldOffset(13)]
        internal byte m_last_mark;
        [MarshalAs(UnmanagedType.I4), FieldOffset(14)]
        internal int m_file_count;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class WXAPKG_FILE
    {
        internal int m_name_length;
        internal string m_name;
        internal int m_offset;
        internal int m_size;
    }

    #endregion

    public delegate void LogMessage(string message);

    public class WxFileExtractor
    {
        public LogMessage LogMessageHandle;

        public void Log(string message)
        {
            if (LogMessageHandle != null)
            {
                LogMessageHandle(message);
            }
        }

        public void Extractor(string wxFile, string targetDir)
        {
            FileStream fs = null;
            try
            {
                fs = new FileStream(wxFile, FileMode.Open, FileAccess.Read);

                // 获取头部信息
                WXAPKG_HEADER header = new WXAPKG_HEADER();

                // 第一个标识，固定为 0xBE
                header.m_first_mark = (byte)fs.ReadByte();
                if (0xBE != header.m_first_mark)
                {
                    Log("文件第一个标识有误，非 WXAPKG 格式文件！");

                    throw new Exception("文件第一个标识有误，非 WXAPKG 格式文件！");
                }

                // 版本号，0 -> 微信分发到客户端 1 -> 开发者上传到微信后台
                byte[] b_int = new byte[4];
                fs.Read(b_int, 0, b_int.Length);
                header.m_edition = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(b_int, 0));

                // 索引段的长度
                fs.Read(b_int, 0, b_int.Length);
                header.m_index_info_length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(b_int, 0));

                // 数据段的长度
                fs.Read(b_int, 0, b_int.Length);
                header.m_body_info_length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(b_int, 0));

                // 第二个标识，固定为 0xED
                header.m_last_mark = (byte)fs.ReadByte();
                if (0xED != header.m_last_mark)
                {
                    Log("文件标识有误，非 WXAPKG 格式文件！");
                    throw new Exception("文件标识有误，非 WXAPKG 格式文件！");
                }

                // 文件数量
                fs.Read(b_int, 0, b_int.Length);
                header.m_file_count = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(b_int, 0));


                List<WXAPKG_FILE> fileList = new List<WXAPKG_FILE>();
                // 获取每个文件的信息
                for (int i = 0; i < header.m_file_count; i++)
                {
                    WXAPKG_FILE file = new WXAPKG_FILE();

                    // 文件长度
                    fs.Read(b_int, 0, b_int.Length);
                    file.m_name_length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(b_int, 0));

                    if (file.m_name_length > 255)
                    {
                        throw new Exception("错误的文件长度！");
                    }

                    // 文件名
                    byte[] b_name = new byte[file.m_name_length];
                    fs.Read(b_name, 0, b_name.Length);
                    file.m_name = Encoding.UTF8.GetString(b_name);

                    // 偏移
                    fs.Read(b_int, 0, b_int.Length);
                    file.m_offset = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(b_int, 0));

                    // 大小
                    fs.Read(b_int, 0, b_int.Length);
                    file.m_size = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(b_int, 0));
                    fileList.Add(file);
                   
                }

                Log($"准备抽取文件到目录{targetDir}");
                for (int i = 0; i < fileList.Count; i++)
                {
                    WXAPKG_FILE file = fileList[i];
                   
                    WriteNewFile(fs, file, targetDir);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                if (null != fs)
                {
                    fs.Close();
                }
            }
        }

        private void WriteNewFile(FileStream fs, WXAPKG_FILE file, string targetDir)
        {
            fs.Seek(file.m_offset, SeekOrigin.Begin);
            string fileName = targetDir + "\\" + file.m_name;
            FileInfo fi = new FileInfo(fileName);
            if(!Directory.Exists(fi.DirectoryName))
            {
                Directory.CreateDirectory(fi.DirectoryName);
            }
            Log($"抽取文件: {fi.FullName}");
            FileStream outFs = new FileStream(fi.FullName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            byte[] buffer = new byte[32 * 1024];
            int alreadyRead = 0;
            while(true)
            {
                if(alreadyRead + buffer.Length > file.m_size)
                {
                    int shouldRead = file.m_size - alreadyRead;
                    fs.Read(buffer, 0, shouldRead);
                    outFs.Write(buffer, 0, shouldRead);
                    alreadyRead += shouldRead;
                    break;
                } else
                {
                    fs.Read(buffer, 0, buffer.Length);
                    outFs.Write(buffer, 0, buffer.Length);
                    alreadyRead += buffer.Length;
                }
            }
            outFs.Flush();
            outFs.Close();
        }
    }
}
