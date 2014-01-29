using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace LocalFileTransfer
{
    public class BalloonTipE : EventArgs
    {
        public string Message{get;set;}
        public string Title{get;set;}
        public ToolTipIcon Icon{get;set;}
        public BalloonTipE(string msg, string title, ToolTipIcon icon = ToolTipIcon.Info)
        {
            Message = msg;
            Title = title;
            Icon = icon;
        }
    }
    public delegate void BalloonTip(BalloonTipE e);
    class Utilities
    {
        public static List<Client> clientx = new List<Client>();
        public static event BalloonTip Tip;
        public static void CreateNotification(string msg, string title, ToolTipIcon type = ToolTipIcon.Info)
        {
            Tip.Invoke(new BalloonTipE(msg, title, type));
        }

        public static string MD5(string filepath)
        {
            byte[] hash;
            MD5 md5 = new MD5CryptoServiceProvider();

            using (Stream file = File.OpenRead(filepath))
                hash = md5.ComputeHash(file);

            StringBuilder strBuilder = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                //change it into 2 hexadecimal digits
                //for each byte
                strBuilder.Append(hash[i].ToString("x2"));
            }

            return strBuilder.ToString();
        }
    }
}
