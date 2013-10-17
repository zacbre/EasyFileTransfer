using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LiteCode.Server;
using LiteCode;
using System.IO;
using System.Net;
using System.Net.Sockets;
namespace LocalFileTransfer
{
    public partial class Form1 : Form
    {
        LiteServer f;
        LiteClient x;
        int clients = 0;
        List<Client> clientx = new List<Client>();
        public NotifyIcon notifyicon;
        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            notifyicon = notifyIcon1;
            notifyicon.Text = "LocalFileTransfer";
            notifyicon.Visible = true;
            label1.Text = "Listening for connections at " + LocalIPAddress() + "...";
            //Start a TCP server.
            label1.AllowDrop = true;
            f = new LiteServer(1085);            
            f.onClientConnect += (Client cli) =>
            {
                //Ask to allow connection.
                if (MessageBox.Show("Client is connecting from " + cli.RemoteIp + ". Allow this connection?", "Connection", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == System.Windows.Forms.DialogResult.Yes)
                {

                    Console.WriteLine("Client connected from " + cli.RemoteIp);
                    cli.aClient.ShareClass("filesendlol", typeof(FileSend));
                    clientx.Add(cli);
                    listBox1.Items.Add(cli.RemoteIp);
                    //attempt to connect back.
                    if (x != null && !x.Connected)
                    {
                        x = new LiteClient(cli.RemoteIp, 1085);
                        textBox1.Text = cli.RemoteIp;
                        if (x.Connected)
                        {
                            button1.Text = "Disconnect";
                            textBox1.ReadOnly = true;
                        }
                    }
                }
                else
                {
                    cli.Disconnect();
                }
            };
            f.onClientDisconnect += (Client cli) =>
            {
                clientx.Remove(cli);
                listBox1.Items.Remove(cli.RemoteIp);
            };
            /*x = new LiteClient("127.0.0.1", 1084);
            textBox1.Text = "127.0.0.1";
            if (x.Connected)
            {
                button1.Text = "Disconnect";
            }*/
        }

        void x_onClientDisconnect(LiteClient obj)
        {
            button1.Text = "Connect";
            textBox1.ReadOnly = false;
        }
        public string LocalIPAddress()
        {
            IPHostEntry host;
            string localIP = "";
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break;
                }
            }
            return localIP;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "Disconnect")
            {
                x.Disconnect();
                button1.Text = "Connect";
                textBox1.ReadOnly = false;
            }
            else
            {
                //Connect to TCP.
                x = new LiteClient(textBox1.Text, 1085);
                x.onClientDisconnect += x_onClientDisconnect;
                if (x.Connected)
                {
                    textBox1.ReadOnly = true;
                    button1.Text = "Disconnect";
                }
            }
        }

        private void label2_DragEnter(object sender, DragEventArgs e)
        {
            if ((e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text)) && x.Connected)
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }
        Send fs = null;
        private void label2_DragDrop(object sender, DragEventArgs e)
        {
            string[] FileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            //
            foreach (string i in FileList)
            {
                if (Directory.Exists(i)) //directory
                {
                    //zip file? nah
                }
                else
                {
                    if (x.Connected)
                    {
                        if(fs == null)
                        {
                            fs = x.aClient.GetSharedClass<Send>("filesendlol");  
                        }
                        fs.ReceivedFile(File.ReadAllBytes(i), Path.GetFileName(i));//
                    }
                    else
                    {
                        foreach (Client cli in clientx)
                        {
                            if (fs == null)
                            {
                                fs = cli.aClient.GetSharedClass<Send>("filesendlol");
                            }
                            fs.ReceivedFile(File.ReadAllBytes(i), Path.GetFileName(i));//
                        }
                    }
                }
                //disconnect and reconnect.
            }
        }

        private void label2_DragOver(object sender, DragEventArgs e)
        {
            if ((e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.Html)) && x.Connected)
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(0);
        }
    }
    class FileSend : Send
    {
        [RemoteExecution]
        public void ReceivedFile(byte[] file, string filename)
        {
            if (!File.Exists(filename))
            {
                File.WriteAllBytes(filename, file);
            }
            else
            {
                if (MessageBox.Show("The file " + filename + " already exists! Overwrite?", "File Already Exists!", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    File.WriteAllBytes(filename, file);
                }
            }
            ((Form1)Form.ActiveForm).notifyicon.BalloonTipIcon = ToolTipIcon.Info;
            ((Form1)Form.ActiveForm).notifyicon.BalloonTipText = "Successfully received file " + filename;
            ((Form1)Form.ActiveForm).notifyicon.BalloonTipTitle = "File Transfer";
            ((Form1)Form.ActiveForm).notifyicon.ShowBalloonTip(10000);
        }
      
    }
    public interface Send
    {
        void ReceivedFile(byte[] file, string filename);
    }
}
