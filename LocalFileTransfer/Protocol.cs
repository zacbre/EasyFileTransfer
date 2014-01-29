using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LocalFileTransfer
{
    enum ReceivedType
    {
        Size = 0x09,
        ChecksumReq = 0x06,
        Declined = 0x01,
        SendFile = 0x02,
        Incoming = 0x09
    }
    public class Protocol
    {
        private Client p;
        private byte[] buff = new byte[2048];
        private string filename = "";
        private string checksum = "";

        BandwidthCounter totalsize = new BandwidthCounter();
        BandwidthCounter counter = new BandwidthCounter();

        public Protocol(Client cli)
        {
            p = cli;
            p.Sock.BeginReceive(buff, 0, buff.Length - 1, SocketFlags.None, new AsyncCallback(Listen), p.Sock);
        }

        public void Listen(IAsyncResult ar)
        {
            int received = 0;
            Socket h = null;
            try
            {
                h = (Socket)ar.AsyncState; //Grab our socket's state from the ASync return handler.
                received = h.EndReceive(ar); //Tell our socket to stop receiving data because our buffer is full.
            }
            catch (Exception ex)
            {
                //error because client disconnected, show error.
                p.OnDisconnect();
                return;
            }

            if (received <= 0)
            {
                try
                {
                    //show client disconnected error.
                    p.OnDisconnect();
                }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }
                return;
            }

            else if (!p.Accepted)
                BeginReceive();

            else if (received == 4)
            {
                if (buff[0] == 0x05 && buff[1] == 0x02 && buff[2] == 0x05 && buff[3] == 0x01)
                {
                    //disconnected.
                    p.Sock.Disconnect(false);
                    FEvents.ConnectButton = "Disconnect";
                    FEvents.IP = false;
                    FEvents.RemoveFromList(p.RemoteIp);
                    Utilities.clientx.Remove(p);
                    //GOOD DISCONNECT.
                }
            }
            else if (received == 1)
            {
                switch ((ReceivedType)buff[0])
                {
                    case ReceivedType.Size:
                        //send filesize.
                        FileInfo file = new FileInfo(filename);
                        byte[] size = BitConverter.GetBytes(file.Length);
                        p.Sock.Send(size);
                        break;
                    case ReceivedType.ChecksumReq:
                        //wait for checksum to be set.
                        while (true)
                        {
                            if (checksum != "")
                            {
                                p.Sock.Send(Encoding.ASCII.GetBytes(checksum));
                                checksum = "";
                                break;
                            }
                            Thread.Sleep(10);
                        }
                        break;
                    case ReceivedType.Declined:
                        //File was declined.
                        FEvents.Title = "FileTransfer";
                        Utilities.CreateNotification("File Transfer for \"" + filename + "\" was declined.", "File Transfer", ToolTipIcon.Error);
                        break;
                    case ReceivedType.SendFile:
                        //Send file.
                        new Thread(new ThreadStart(delegate()
                        { checksum = Utilities.MD5(filename); })).Start();

                        //start sending file.
                        FileInfo a = new FileInfo(filename);
                        //progressbar gets maximum of filesize.

                        System.Timers.Timer timer = new System.Timers.Timer() { Enabled = true, Interval = 1000 };
                        timer.Elapsed += this.timer_Elapsed;
                        timer.Start();

                        ulong step = 0;
                        totalsize = new BandwidthCounter();

                        using (BinaryReader bin = new BinaryReader(File.OpenRead(filename)))
                        {
                            totalsize.AddBytes((ulong)a.Length);
                            while (step < (ulong)a.Length)
                            {
                                try
                                {
                                    //sending buff's length.
                                    step += (ulong)buff.Length;
                                    p.Sock.Send(bin.ReadBytes(buff.Length));

                                    counter.AddBytes((uint)buff.Length);

                                    float af = (float)step / (float)a.Length;
                                    int tot = (int)Math.Round(af * 100);
                                    if (tot < 100)
                                        FEvents.Progress = tot;
                                    else
                                        FEvents.Progress = 100;
                                }
                                catch
                                {
                                    timer.Stop();
                                    
                                    FEvents.Title = "FileTransfer";
                                    FEvents.Progress = 0;

                                    Utilities.CreateNotification("File Transfer for \"" + filename + "\" has failed!", "File Transfer", ToolTipIcon.Error);
                                    BeginReceive();
                                    return;
                                }
                            }
                        }
                        timer.Stop();

                        FEvents.Title = "FileTransfer";
                        FEvents.Status = "Idle.";
                        FEvents.Progress = 0;
                        FEvents.DropEnabled = true;

                        Utilities.CreateNotification("Successfully sent the file \"" + filename + "\".", "File Transfer", ToolTipIcon.Info);
                        break;
                }
            }
            else if (received > 0)
            {
                if ((ReceivedType)buff[0] == ReceivedType.Incoming && received != 1)
                {
                    try
                    {
                        byte[] buf = new byte[received - 1];
                        Array.Copy(buff, 1, buf, 0, buf.Length);
                        filename = Encoding.ASCII.GetString(buf);
                        if (MessageBox.Show("Receive the File " + filename + "?", "Incoming Filetransfer", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        {
                            FEvents.DropEnabled = false;
                            //receiving file
                            FEvents.Title = "FileTransfer";
                            FEvents.Status = "Requesting File Size...";
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
                                FEvents.Status = "Starting Transfer...";

                                FEvents.Progress = 0;

                                System.Timers.Timer timer = new System.Timers.Timer() { Interval = 1000, Enabled = true };
                                timer.Elapsed += timer_Elapsed1;
                                timer.Start();

                                ulong step = 0;
                                ulong total = toreceive;
                                totalsize = new BandwidthCounter();
                                using (BinaryWriter f = new BinaryWriter(File.Open(Environment.CurrentDirectory + "\\Received\\" + filename, FileMode.Create, FileAccess.Write, FileShare.None)))
                                {
                                    totalsize.AddBytes(total);
                                    while (toreceive > 0)
                                    {
                                        ulong recd = (ulong)h.Receive(buff);
                                        step += recd;
                                        if (recd <= 0)
                                        {
                                            //uhwat. Filetransfer failed?
                                            timer.Stop();
                                            FEvents.Title = "FileTransfer";
                                            FEvents.Progress = 0;

                                            Utilities.CreateNotification("File Transfer for \"" + filename + "\" has failed!", "File Transfer", ToolTipIcon.Error);
                                            BeginReceive();
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
                                            FEvents.Progress = tot;
                                        else
                                            FEvents.Progress = 100;
                                    }
                                }
                                timer.Stop();

                                //request MD5.
                                h.Send(new byte[] { 0x06 });
                                FEvents.Status = "Requesting Checksum...";

                                int chkc = h.Receive(buff);

                                checksum = "";
                                if (chkc > 0) { checksum = Encoding.ASCII.GetString(buff).Trim('\0').Substring(0, chkc); }

                                FEvents.Status = "Checking File for Corruption...";

                                if (Utilities.MD5(Environment.CurrentDirectory + "\\Received\\" + filename) == checksum) { Utilities.CreateNotification("Successfully Received \"" + filename + "\"! Click to Open Folder.", "File Transfer", ToolTipIcon.Info); }
                                else { Utilities.CreateNotification("File was corrupted while sent. Please try again.", "File Transfer", ToolTipIcon.Error); }

                                FEvents.Progress = 0;
                                FEvents.Status = "Idle.";
                            }
                            FEvents.Title = "FileTransfer";
                            FEvents.DropEnabled = true;
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
            BeginReceive();
        }

        public void SendFile(string file)
        {
            //send file.
            FEvents.DropEnabled = false;
            FEvents.Title = "FileTransfer - Sending";
            filename = file;
            byte[] fil = Encoding.ASCII.GetBytes(Path.GetFileName(file));
            byte[] sup = new byte[fil.Length + 1];
            new byte[] { 0x09 }.CopyTo(sup, 0);
            Array.Copy(fil, 0, sup, 1, fil.Length);
            p.Sock.Send(sup);
            //wait for reply.
        }

        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            FEvents.Title = "FileTransfer - Sending (" + counter.GetPerSecond() + ")";
            FEvents.Status = string.Format("{0} out of {1} transferred.", counter.ToString(), totalsize.ToString());

        }

        private void timer_Elapsed1(object sender, System.Timers.ElapsedEventArgs e)
        {
            FEvents.Title = "FileTransfer - Receiving (" + counter.GetPerSecond() + ")";
            FEvents.Status = string.Format("{0} out of {1} transferred.", counter.ToString(), totalsize.ToString());
        }

        private void BeginReceive()
        {
            p.Sock.BeginReceive(buff, 0, buff.Length - 1, SocketFlags.None, new AsyncCallback(Listen), p.Sock);
        }
    }
}
