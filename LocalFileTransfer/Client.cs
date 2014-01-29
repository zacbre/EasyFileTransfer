using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LocalFileTransfer
{
    public class Client
    {
        Form1 x;
        private Socket sck;
        bool receiving = false;
        string filename = "";
        bool accepted = false;
        string checksum = "";

        public Protocol Proto;
        
        public bool Accepted { get { return accepted; } set { accepted = value; } }

        public Client(Socket sock, bool begin, Form1 w, bool requireaccept)
        {
            x = w;
            sck = sock;
            //listen for receives.
            accepted = requireaccept ? false : true;
            remoteip = sck.RemoteEndPoint.ToString().Split(':')[0];

            // Start Receiving.
            Proto = new Protocol(this);
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
                this.OnDisconnect();
            }
            catch { }
        }

        public void OnDisconnect()
        {
            try
            {
                sck.Disconnect(false);
                FEvents.RemoveFromList(this.RemoteIp);
                Utilities.clientx.Remove(this);
            }
            catch { }
            FEvents.ConnectButton = "Connect";
            FEvents.IP = false;
            Utilities.CreateNotification("The connection to \"" + remoteip + "\" has failed!", "Connection Failed!", ToolTipIcon.Error);
        }
    }
}
