using System;
using System.Collections.Generic;
using System.Text;

namespace LocalFileTransfer
{
    public delegate void MessageHandler(ChangedEvent e);
    public class ChangedEvent : EventArgs
    {
        public ChangedEvent(object g)
        {
            Message = g;
        }
        public object Message { get; set; }       
    }
    public class FEvents
    {
        public static event MessageHandler title;
        public static event MessageHandler status;
        public static event MessageHandler progressbar;
        public static event MessageHandler drop;
        public static event MessageHandler ip;
        public static event MessageHandler connectbutton;
        public static event MessageHandler list;

        public static string Title
        {
            set { title.Invoke(new ChangedEvent(value)); }
        }

        public static string Status
        {
            set { status.Invoke(new ChangedEvent(value)); }
        }

        public static int Progress
        {
            set { progressbar.Invoke(new ChangedEvent(value)); }
        }

        public static bool DropEnabled
        {
            set { drop.Invoke(new ChangedEvent(value)); }
        }

        public static bool IP
        {
            set { ip.Invoke(new ChangedEvent(value)); }
        }

        public static string ConnectButton
        {
            set { connectbutton.Invoke(new ChangedEvent(value)); }
        }
        
        public static void RemoveFromList(string g)
        {
            list.Invoke(new ChangedEvent(g));
        }
    }
}
