using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace LocalFileTransfer
{
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
                    byte[] size = BitConverter.GetBytes(a.Length);
                    sck.Send(size);
                }
                else if (buff[0] == 0x01)
                {
                    x.Text = "FileTransfer";
                    x.notifyicon.BalloonTipIcon = ToolTipIcon.Error;
                    x.notifyicon.BalloonTipText = "File Transfer for \"" + filename + "\" was declined.";
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
                    x.progressBar1.Maximum = 100;
                    System.Timers.Timer timer;
                    //Set Timer
                    timer = new System.Timers.Timer();
                    timer.Elapsed += timer_Elapsed;
                    timer.Interval = 1000; //10000 ms = 10 seconds
                    timer.Enabled = true;
                    timer.Start();
                    ulong step = 0;
                    using (BinaryReader bin = new BinaryReader(File.OpenRead(filename)))
                        while (p < a.Length)
                        {
                            try
                            {
                                //sending buff's length.
                                step += (ulong)buff.Length;
                                sck.Send(bin.ReadBytes(buff.Length));
                                p += buff.Length;
                                counter.AddBytes((uint)buff.Length);
                                float af = (float)step / (float)a.Length;
                                //progressbar equals rounded.
                                int tot = (int)Math.Round(af * 100);
                                if (tot < 100)
                                    x.progressBar1.Value = tot;
                            }
                            catch {
                                x.Text = "FileTransfer";
                                receiving = false;
                                timer.Stop();
                                x.notifyicon.BalloonTipIcon = ToolTipIcon.Error;
                                x.notifyicon.BalloonTipText = "File Transfer for \"" + filename + "\" has failed!";
                                x.notifyicon.BalloonTipTitle = "File Transfer";
                                x.notifyicon.ShowBalloonTip(10000);
                                x.progressBar1.Value = 0;
                                sck.BeginReceive(buff, 0, buff.Length - 1, SocketFlags.None, new AsyncCallback(DisconnectListener), sck);
                                return;
                            }
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
                                ulong toreceive = BitConverter.ToUInt64(but, 0);
                                h.Send(new byte[] { 0x02 });
                                //start the receiving.
                                x.progressBar1.Value = 0;
                                x.progressBar1.Maximum = 100;
                                System.Timers.Timer timer;
                                timer = new System.Timers.Timer();
                                timer.Elapsed += timer_Elapsed1;
                                timer.Interval = 1000; //10000 ms = 10 seconds
                                timer.Enabled = true;
                                timer.Start();
                                ulong step = 0;
                                ulong total = toreceive;
                                using (BinaryWriter f = new BinaryWriter(File.Open(Environment.CurrentDirectory + @"\Received\" + filename, FileMode.Create, FileAccess.Write, FileShare.None)))
                                {
                                    while (toreceive > 0)
                                    {
                                        ulong recd = (ulong)h.Receive(buff);
                                        step += recd;
                                        if (recd <= 0)
                                        {
                                            //uhwat. Filetransfer failed?
                                            x.Text = "FileTransfer";
                                            receiving = false;
                                            timer.Stop();
                                            x.notifyicon.BalloonTipIcon = ToolTipIcon.Error;
                                            x.notifyicon.BalloonTipText = "File Transfer for \""+filename+"\" has failed!";
                                            x.notifyicon.BalloonTipTitle = "File Transfer";
                                            x.notifyicon.ShowBalloonTip(10000);
                                            x.progressBar1.Value = 0;
                                            sck.BeginReceive(buff, 0, buff.Length - 1, SocketFlags.None, new AsyncCallback(DisconnectListener), sck);
                                            return;
                                        }
                                        toreceive -= recd;
                                        counter.AddBytes((uint)recd);
                                        //write bytes to file.
                                        f.Write(buff, 0, (int)recd);
                                        //divide smaller number and calculate percentage.
                                        float af = (float)step / (float)total;
                                        //progressbar equals rounded.
                                        int tot = (int)Math.Round(af * 100);
                                        if (tot < 100)
                                            x.progressBar1.Value = tot;
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
        public bool Connected
        {
            get
            {
                return sck.Connected;
                //try to send 0x06 to socket.
            }
        }

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
}
