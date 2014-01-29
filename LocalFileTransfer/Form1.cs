using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Reflection;
namespace LocalFileTransfer
{
    public partial class Form1 : Form
    {
        Socket f;
        Client x;
        
        public NotifyIcon notifyicon;
        public Form1()
        {
            InitializeComponent();
        }
        string dir = Environment.CurrentDirectory + "\\Received\\";
        void notifyicon_BalloonTipClicked(object sender, EventArgs e)
        {
            //open the folder location.
            System.Diagnostics.Process.Start(dir);
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            Directory.CreateDirectory(dir);
            FieldInfo iconField = typeof(Form).GetField("defaultIcon", BindingFlags.NonPublic | BindingFlags.Static);
            Icon myIcon = (Icon)iconField.GetValue(iconField);
            notifyicon = notifyIcon1;
            notifyicon.Text = "LocalFileTransfer";
            notifyicon.Visible = true;
            notifyicon.BalloonTipClicked += notifyicon_BalloonTipClicked;
            notifyicon.Icon = myIcon;
            //Start a TCP server.
            label1.AllowDrop = true;
            
            //Start events.
            FEvents.connectbutton += FEvents_connectbutton;
            FEvents.ip += FEvents_ip;
            FEvents.list += FEvents_list;
            FEvents.progressbar += FEvents_progressbar;
            FEvents.status += FEvents_status;
            FEvents.title += FEvents_title;
            FEvents.drop += FEvents_drop;
            Utilities.Tip += Utilities_Tip;
        }

        void Utilities_Tip(BalloonTipE e)
        {
            this.Invoke((MethodInvoker)(() => { notifyIcon1.BalloonTipText = e.Message; notifyIcon1.BalloonTipTitle = e.Title; notifyIcon1.BalloonTipIcon = e.Icon; notifyIcon1.ShowBalloonTip(10000); }));
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
                Socket p = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    p.Connect(new IPEndPoint(IPAddress.Parse(textBox1.Text), int.Parse(textBox3.Text)));
                    x = new Client(p, false, this, false);
                    if (x.Connected)
                    {
                        textBox1.ReadOnly = true;
                        button1.Text = "Disconnect";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private void label2_DragEnter(object sender, DragEventArgs e)
        {
            if ((e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.Html)) && ((x != null && x.Connected) || Utilities.clientx.Count > 0))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }
        private void label2_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                new Thread(new ThreadStart(delegate()
                    {
                        string[] FileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                        //
                        foreach (string i in FileList)
                        {
                            if (Directory.Exists(i)) //directory
                            {
                                // zip file?
                            }
                            else
                            {
                                if (x != null && x.Connected)
                                {
                                    //send file.
                                    x.Proto.SendFile(i);
                                }
                                if (Utilities.clientx.Count > 0)
                                {
                                    foreach (Client cli in Utilities.clientx)
                                    {
                                        //send file if clients are connected.
                                        cli.Proto.SendFile(i);
                                    }
                                }
                            }
                            //disconnect and reconnect.
                        }
                    })).Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void label2_DragOver(object sender, DragEventArgs e)
        {
            if ((e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.Html)) && ((x != null && x.Connected) || Utilities.clientx.Count > 0))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(0);
        }

        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {

        }

        private void disconnectClientToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBox1.SelectedItems.Count > 0)
            {
                string ip = listBox1.SelectedItems[0].ToString();
                foreach (Client cl in Utilities.clientx)
                {
                    if (cl.RemoteIp == ip)
                    {
                        listBox1.Items.Remove(cl.RemoteIp);
                        cl.Disconnect();
                        Utilities.clientx.Remove(cl);
                        //disconnect x?
                        break;
                    }
                }
            }
        }
        Thread th;
        private void button2_Click(object sender, EventArgs e) {
            f = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            f.Bind(new IPEndPoint(IPAddress.Any, int.Parse(textBox2.Text)));
            f.Listen(50000);

            label1.Text = string.Format("Listening for connections at {0}:{1}...", LocalIPAddress(), textBox2.Text);
            button2.Enabled = false;
            button3.Enabled = true;
            textBox2.ReadOnly = true;

            th = new Thread(new ThreadStart(delegate()
            {
                while (true)
                    try
                    {
                        new Thread(new ParameterizedThreadStart(delegate(object _socket)
                        {
                            Client cli = new Client((Socket)_socket, true, this, true);
                            //Ask to allow connection.
                            if (MessageBox.Show("Client is connecting from " + cli.RemoteIp + ". Allow this connection?", "Incoming Connection", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == System.Windows.Forms.DialogResult.Yes)
                            {
                                //Client must be confirmed to send files, etc.
                                cli.Accepted = true;

                                Console.WriteLine("Client connected from " + cli.RemoteIp);

                                Utilities.clientx.Add(cli);
                                if(listBox1.InvokeRequired)
                                {
                                    listBox1.Invoke((MethodInvoker)(() => { listBox1.Items.Add(cli.RemoteIp); }));
                                }
                            }
                            else
                            {
                                cli.Disconnect();
                            }
                        })).Start(f.Accept());
                    }
                    catch { }
            }));
            th.Start();
            textBox1.Focus();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            f.Close();

            button2.Enabled = true;
            button3.Enabled = false;
            textBox2.ReadOnly = false;

            label1.Text = "Idle.";
            th.Abort();
        }

        //Events
        void FEvents_drop(ChangedEvent e)
        {
            this.Invoke((MethodInvoker)(() => this.label2.Enabled = (bool)e.Message));
        }

        void FEvents_title(ChangedEvent e)
        {
            this.Invoke((MethodInvoker)(() => this.Text = (string)e.Message)); 
        }

        void FEvents_status(ChangedEvent e)
        {
            this.Invoke((MethodInvoker)(() => this.label5.Text = (string)e.Message));
        }

        void FEvents_progressbar(ChangedEvent e)
        {
            this.Invoke((MethodInvoker)(() => this.progressBar1.Value = (int)e.Message));
        }

        void FEvents_list(ChangedEvent e)
        {
            this.Invoke((MethodInvoker)(() => this.listBox1.Items.Remove(e.Message)));
        }

        void FEvents_ip(ChangedEvent e)
        {
            this.Invoke((MethodInvoker)(() => this.textBox1.ReadOnly = (bool)e.Message));
        }

        void FEvents_connectbutton(ChangedEvent e)
        {
            this.Invoke((MethodInvoker)(() => this.button1.Text = (string)e.Message));
        }
    }
}