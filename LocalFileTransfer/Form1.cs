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
        int clients = 0;
        public List<Client> clientx = new List<Client>();
        public NotifyIcon notifyicon;
        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false; 
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
            label1.Text = "Listening for connections at " + LocalIPAddress() + "...";
            //Start a TCP server.
            label1.AllowDrop = true;
            f = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            f.Bind(new IPEndPoint(IPAddress.Any, 1084));
            f.Listen(50000);
            new Thread(new ThreadStart(delegate()
                {

                while(true)
            new Thread(new ParameterizedThreadStart(delegate(object _socket)
                {
                    Client cli = new Client((Socket)_socket, true, this);
                    //Ask to allow connection.
                    if (MessageBox.Show("Client is connecting from " + cli.RemoteIp + ". Allow this connection?", "Incoming Connection", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == System.Windows.Forms.DialogResult.Yes)
                    {

                        Console.WriteLine("Client connected from " + cli.RemoteIp);
                        clientx.Add(cli);
                        listBox1.Items.Add(cli.RemoteIp);
                    }
                    else
                    {
                        cli.Disconnect();
                    }
                })).Start(f.Accept());
                })).Start();
                //button1.Text = "Connect";
                //textBox1.ReadOnly = false;
            /*x = new LiteClient("127.0.0.1", 1084);
            textBox1.Text = "127.0.0.1";
            if (x.Connected)
            {
                button1.Text = "Disconnect";
            }*/
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
                    p.Connect(new IPEndPoint(IPAddress.Parse(textBox1.Text), 1084));
                    x = new Client(p, false, this);
                    if (x.Connected)
                    {
                        textBox1.ReadOnly = true;
                        button1.Text = "Disconnect";
                    }
                }
                catch(Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private void label2_DragEnter(object sender, DragEventArgs e)
        {
            if ((e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.Html)) && ((x != null && x.Connected) || clientx.Count > 0))
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
                                //zip file? yah

                            }
                            else
                            {
                                if (x != null && x.Connected)
                                {
                                    //send file.
                                    x.SendFile(i);
                                }
                                else
                                {
                                    foreach (Client cli in clientx)
                                    {
                                        //send file if clients are connected.
                                        cli.SendFile(i);
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
            if ((e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.Html)) && ((x != null && x.Connected) || clientx.Count > 0))
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
                foreach(Client cl in clientx)
                {
                    if (cl.RemoteIp == ip)
                    {
                        listBox1.Items.Remove(cl.RemoteIp);
                        cl.Disconnect();
                        clientx.Remove(cl);
                        //disconnect x?
                        break;
                    }
                }
            }
        }
    }
    public class Client
    {
        Form1 x;
        private Socket sck;
        private byte[] buff;
        bool receiving = false;
        string filename = "";
        public Client(Socket sock, bool begin, Form1 w)
        {
            x = w;
            sck = sock;
            buff = new byte[2048];
            //listen for receives.
            sck.BeginReceive(buff, 0, buff.Length - 1, SocketFlags.None, new AsyncCallback(DisconnectListener), sck);
            remoteip = sck.RemoteEndPoint.ToString().Split(':')[0];
        }

        private void DisconnectListener(IAsyncResult ar)
        {
            int received = 0;
            Socket h = null;
            try
            {
                h = (Socket)ar.AsyncState; //Grab our socket's state from the ASync return handler.
                received = h.EndReceive(ar); //Tell our socket to stop receiving data because our buffer is full.
            }
            catch
            {
                //client disconnected.
                h.Disconnect(false);
                sck.Disconnect(false);
                x.listBox1.Items.Remove(this.RemoteIp);
                x.clientx.Remove(this);
            }
            if (received <= 0)
            //Disconnected or for some reason not valid handle.
            {
                try
                {
                    h.Close(); //Close our socket.
                    sck.Close();
                }
                catch { }
                return;
            }
            else if (received == 4)
            {
                if (buff[0] == 0x05 && buff[1] == 0x02 && buff[2] == 0x05 && buff[3] == 0x01)
                {
                    //disconnected.
                    sck.Disconnect(false);
                    x.button1.Text = "Connect";
                    x.textBox1.ReadOnly = false;
                    MessageBox.Show("Client disconnected.");
                    x.clientx.Remove(this);
                    x.listBox1.Items.Remove(this.remoteip);
                }
            }
            else if (received == 1)
            {
                if (buff[0] == 0x09)
                {
                    //send filesize.
                    FileInfo a = new FileInfo(filename);
                    sck.Send(BitConverter.GetBytes(a.Length));
                }
                else if (buff[0] == 0x01)
                {
                    x.Text = "FileTransfer";
                    x.notifyicon.BalloonTipIcon = ToolTipIcon.Error;
                    x.notifyicon.BalloonTipText = "File Transfer for \""+filename+"\" was declined.";
                    x.notifyicon.BalloonTipTitle = "File Transfer";
                    x.notifyicon.ShowBalloonTip(10000);
                }
                else if (buff[0] == 0x02)
                {
                    //start sending file.
                    FileInfo a = new FileInfo(filename);
                    int p = 0;
                    //progressbar gets maximum of filesize.
                    x.progressBar1.Value = 0;
                    x.progressBar1.Maximum = (int)a.Length;
                    System.Timers.Timer timer;
                    //Set Timer
                    timer = new System.Timers.Timer();
                    timer.Elapsed += timer_Elapsed;
                    timer.Interval = 1000; //10000 ms = 10 seconds
                    timer.Enabled = true;
                    timer.Start();
                    using (BinaryReader bin = new BinaryReader(File.OpenRead(filename)))
                        while (p < a.Length)
                        {
                            sck.Send(bin.ReadBytes(buff.Length));
                            p += buff.Length;
                            counter.AddBytes((uint)buff.Length);
                            if (x.progressBar1.Value + buff.Length > x.progressBar1.Maximum) x.progressBar1.Value = x.progressBar1.Maximum;
                            else
                                x.progressBar1.Value += buff.Length;
                        }
                    x.Text = "FileTransfer";
                    timer.Stop();
                    x.notifyicon.BalloonTipIcon = ToolTipIcon.Info;
                    x.notifyicon.BalloonTipText = "Successfully Sent File \"" + filename + "\"!";
                    x.notifyicon.BalloonTipTitle = "File Transfer";
                    x.notifyicon.ShowBalloonTip(10000);
                    x.progressBar1.Value = 0;
                }               
            }
            else if (received > 0)
            {
                if (buff[0] == 0x09 && received != 1)
                {
                    try
                    {
                        byte[] buf = new byte[received - 1];
                        Array.Copy(buff, 1, buf, 0, buf.Length);
                        filename = Encoding.ASCII.GetString(buf);
                        if (MessageBox.Show("Receive the File " + filename + "?", "Incoming Filetransfer", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            //receiving file
                            x.Text = "FileTransfer - Receiving";
                            receiving = true;
                            h.Send(new byte[] { 0x09 });
                            //start receiving.
                            int l = h.Receive(buff);
                            if (l > 0)
                            {
                                //convert to int.
                                byte[] but = new byte[l];
                                Array.Copy(buff, 0, but, 0, l);
                                int toreceive = BitConverter.ToInt32(but, 0);
                                h.Send(new byte[] { 0x02 });
                                //start the receiving.
                                x.progressBar1.Value = 0;
                                x.progressBar1.Maximum = toreceive;
                                System.Timers.Timer timer;
                                timer = new System.Timers.Timer();
                                timer.Elapsed += timer_Elapsed1;
                                timer.Interval = 1000; //10000 ms = 10 seconds
                                timer.Enabled = true;
                                timer.Start();
                                using (BinaryWriter f = new BinaryWriter(File.OpenWrite(Environment.CurrentDirectory + "\\Received\\" + filename)))
                                {
                                    while (toreceive > 0)
                                    {
                                        int recd = h.Receive(buff);
                                        toreceive -= recd;
                                        counter.AddBytes((uint)recd);
                                        //write bytes to file.
                                        f.Write(buff, 0, recd);
                                        if (x.progressBar1.Value + recd > x.progressBar1.Maximum) x.progressBar1.Value = x.progressBar1.Maximum;
                                        else
                                            x.progressBar1.Value += recd;
                                    }
                                }
                                timer.Stop();
                                x.notifyicon.BalloonTipIcon = ToolTipIcon.Info;
                                x.notifyicon.BalloonTipText = "Successfully Received File \"" + filename + "\". Click to Open Folder.";
                                x.notifyicon.BalloonTipTitle = "File Transfer";
                                x.notifyicon.ShowBalloonTip(10000);
                                x.progressBar1.Value = 0;
                            }
                            x.Text = "FileTransfer";
                            receiving = false;
                        }
                        else
                        {
                            h.Send(new byte[] { 0x01 });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }
                else if (buff[0] == 0x06)
                {
                    h.Send(new byte[] { 0x03 });
                }
            }
            sck.BeginReceive(buff, 0, buff.Length - 1, SocketFlags.None, new AsyncCallback(DisconnectListener), sck);
        }
        BandwidthCounter counter = new BandwidthCounter();
        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            x.Text = "FileTransfer - Sending (" + counter.GetPerSecond() + ")";
        }

        void timer_Elapsed1(object sender, System.Timers.ElapsedEventArgs e)
        {
            x.Text = "FileTransfer - Receiving (" + counter.GetPerSecond() + ")";
        }

        public void SendFile(string file)
        {
            //send file.
            x.Text = "FileTransfer - Sending";
            filename = file;
            byte[] fil = Encoding.ASCII.GetBytes(Path.GetFileName(file));
            byte[] sup = new byte[fil.Length + 1];
            new byte[] { 0x09 }.CopyTo(sup, 0);
            Array.Copy(fil, 0, sup, 1, fil.Length);
            sck.Send(sup);
            //wait for reply.
        }

        public Socket Sock { get { return sck; } }
        string remoteip;
        public string RemoteIp { get { return remoteip; } }
        public bool Connected { get {
            return sck.Connected;
            //try to send 0x06 to socket.
        } }

        public void Disconnect()
        {
            try
            {
                sck.Send(new byte[] { 0x05, 0x02, 0x05, 0x01 });
                sck.Disconnect(false);
            }
            catch { }
        }
    }
    public class BandwidthCounter
    {
        /// <summary>
        /// Class to manage an adapters current transfer rate
        /// </summary>
        class MiniCounter
        {
            public uint bytes = 0;
            public uint kbytes = 0;
            public uint mbytes = 0;
            public uint gbytes = 0;
            public uint tbytes = 0;
            public uint pbytes = 0;
            DateTime lastRead = DateTime.Now;

            /// <summary>
            /// Adds bits(total misnomer because bits per second looks a lot better than bytes per second)
            /// </summary>
            /// <param name="count">The number of bits to add</param>
            public void AddBytes(uint count)
            {
                bytes += count;
                while (bytes > 1024)
                {
                    kbytes++;
                    bytes -= 1024;
                }
                while (kbytes > 1024)
                {
                    mbytes++;
                    kbytes -= 1024;
                }
                while (mbytes > 1024)
                {
                    gbytes++;
                    mbytes -= 1024;
                }
                while (gbytes > 1024)
                {
                    tbytes++;
                    gbytes -= 1024;
                }
                while (tbytes > 1024)
                {
                    pbytes++;
                    tbytes -= 1024;
                }
            }

            /// <summary>
            /// Returns the bits per second since the last time this function was called
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                if (pbytes > 0)
                {
                    double ret = (double)pbytes + ((double)((double)tbytes / 1024));
                    ret = ret / (DateTime.Now - lastRead).TotalSeconds;
                    lastRead = DateTime.Now;
                    string s = ret.ToString();
                    if (s.Length > 6)
                        s = s.Substring(0, 6);
                    return s + " Pb";
                }
                else if (tbytes > 0)
                {
                    double ret = (double)tbytes + ((double)((double)gbytes / 1024));
                    ret = ret / (DateTime.Now - lastRead).TotalSeconds;
                    lastRead = DateTime.Now;
                    string s = ret.ToString();
                    if (s.Length > 6)
                        s = s.Substring(0, 6);
                    return s + " Tb";
                }
                else if (gbytes > 0)
                {
                    double ret = (double)gbytes + ((double)((double)mbytes / 1024));
                    ret = ret / (DateTime.Now - lastRead).TotalSeconds;
                    lastRead = DateTime.Now;
                    string s = ret.ToString();
                    if (s.Length > 6)
                        s = s.Substring(0, 6);
                    return s + " Gb";
                }
                else if (mbytes > 0)
                {
                    double ret = (double)mbytes + ((double)((double)kbytes / 1024));
                    ret = ret / (DateTime.Now - lastRead).TotalSeconds;
                    lastRead = DateTime.Now;
                    string s = ret.ToString();
                    if (s.Length > 6)
                        s = s.Substring(0, 6);
                    return s + " Mb";
                }
                else if (kbytes > 0)
                {
                    double ret = (double)kbytes + ((double)((double)bytes / 1024));
                    ret = ret / (DateTime.Now - lastRead).TotalSeconds;
                    lastRead = DateTime.Now;
                    string s = ret.ToString();
                    if (s.Length > 6)
                        s = s.Substring(0, 6);
                    return s + " Kb";
                }
                else
                {
                    double ret = bytes;
                    ret = ret / (DateTime.Now - lastRead).TotalSeconds;
                    lastRead = DateTime.Now;
                    string s = ret.ToString();
                    if (s.Length > 6)
                        s = s.Substring(0, 6);
                    return s + " b";
                }
            }
        }

        private uint bytes = 0;
        private uint kbytes = 0;
        private uint mbytes = 0;
        private uint gbytes = 0;
        private uint tbytes = 0;
        private uint pbytes = 0;
        MiniCounter perSecond = new MiniCounter();

        /// <summary>
        /// Empty constructor, because thats constructive
        /// </summary>
        public BandwidthCounter()
        {

        }

        /// <summary>
        /// Accesses the current transfer rate, returning the text
        /// </summary>
        /// <returns></returns>
        public string GetPerSecond()
        {
            string s = perSecond.ToString() + "/s";
            perSecond = new MiniCounter();
            return s;
        }

        /// <summary>
        /// Adds bytes to the total transfered
        /// </summary>
        /// <param name="count">Byte count</param>
        public void AddBytes(uint count)
        {
            // overflow max
            if ((count * 8) >= Int32.MaxValue)
                return;

            count = 8 * count;
            perSecond.AddBytes(count);
            bytes += count;
            while (bytes > 1024)
            {
                kbytes++;
                bytes -= 1024;
            }
            while (kbytes > 1024)
            {
                mbytes++;
                kbytes -= 1024;
            }
            while (mbytes > 1024)
            {
                gbytes++;
                mbytes -= 1024;
            }
            while (gbytes > 1024)
            {
                tbytes++;
                gbytes -= 1024;
            }
            while (tbytes > 1024)
            {
                pbytes++;
                tbytes -= 1024;
            }
        }

        /// <summary>
        /// Prints out a relevant string for the bits transfered
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (pbytes > 0)
            {
                double ret = (double)pbytes + ((double)((double)tbytes / 1024));
                string s = ret.ToString();
                if (s.Length > 6)
                    s = s.Substring(0, 6);
                return s + " Pb";
            }
            else if (tbytes > 0)
            {
                double ret = (double)tbytes + ((double)((double)gbytes / 1024));
                string s = ret.ToString();
                if (s.Length > 6)
                    s = s.Substring(0, 6);
                return s + " Tb";
            }
            else if (gbytes > 0)
            {
                double ret = (double)gbytes + ((double)((double)mbytes / 1024));
                string s = ret.ToString();
                if (s.Length > 6)
                    s = s.Substring(0, 6);
                return s + " Gb";
            }
            else if (mbytes > 0)
            {
                double ret = (double)mbytes + ((double)((double)kbytes / 1024));
                string s = ret.ToString();
                if (s.Length > 6)
                    s = s.Substring(0, 6);
                return s + " Mb";
            }
            else if (kbytes > 0)
            {
                double ret = (double)kbytes + ((double)((double)bytes / 1024));
                string s = ret.ToString();
                if (s.Length > 6)
                    s = s.Substring(0, 6);
                return s + " Kb";
            }
            else
            {
                string s = bytes.ToString();
                if (s.Length > 6)
                    s = s.Substring(0, 6);
                return s + " b";
            }
        }
    }
}
