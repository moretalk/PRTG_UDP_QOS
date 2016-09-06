//--------------------------------------------------------
// The is release under Creative Commons Attribution 4.0 International license
// https://creativecommons.org/licenses/by/4.0/
//--------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Timers;


namespace UDPReflector
{
    class Program
    {
        List<RemoteList> Clientlist = new List<RemoteList>();
        Object RemoteListLock = new object();
        int Top;
        int Left;
        UdpClient socket;
        Int32 RemotePort=50000;

        void UpdateClientList(IPAddress SourceAddress, Int32 Port, Int64 Bytes)
        {
            bool Found = false;
            lock (RemoteListLock)
            {
                for (int t = 0; t < Clientlist.Count;t++)
                {
                    if (Clientlist[t].SourceAddress.ToString() == SourceAddress.ToString())
                    {
                        Found = true;
                        Clientlist[t].Port = Port;
                        Clientlist[t].Counter += Bytes;
                        Clientlist[t].LastTick = DateTime.Now.Ticks;

                    }

                }
                if(Found == false)
                {
                    RemoteList client = new RemoteList();
                    client.SourceAddress = SourceAddress;
                    client.Port = Port;
                    client.Counter = Bytes;
                    client.LastTick = DateTime.Now.Ticks;
                    Clientlist.Add(client);
                }

            }
        }


        void OnUdpData()
        {

            IPEndPoint source = new IPEndPoint(0, 0);                               // points towards whoever had sent the message:
            try {
                byte[] message = socket.Receive(ref source);                 // get the actual message and fill out the source:

                IPEndPoint target = new IPEndPoint(source.Address, 50000); // sending data (for the sake of simplicity, back to ourselves):
                socket.Send(message, message.Length, source);
                UpdateClientList(source.Address, source.Port, message.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());

            }

        }


        static void Main(string[] args)
        {
            Program myProgram = new Program();
            myProgram.ProcessArguments(args);

            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(myProgram.OnTimedEvent);
            aTimer.Interval = 5000;
            aTimer.Enabled = true;

            Console.Clear();
            myProgram.Top = Console.CursorTop;
            myProgram.Left = Console.CursorLeft;

            myProgram.socket = new UdpClient(myProgram.RemotePort);

            while (Console.KeyAvailable == false)
            {
                if (myProgram.socket.Available > 0)
                {
                    myProgram.OnUdpData();

                }
            }
            
        }

        //---------------------------
        // get argument value based on left hand side of the =
        //--------------------------------------
        String GetArgument(string[] args, string key)
        {
            key = key.ToLower();


            foreach (String arg in args)
            {

                String[] values = arg.ToLower().Split('=');
                if (values.Length == 2)
                {
                    if (values[0] == key)
                    {
                        return values[1];
                    }
                }
            }
            return "";
        }

        //-------------------------
        // see if an argument exists
        //-------------------------
        bool ArgumentExists(string[] args, string key)
        {
            foreach (String arg in args)
            {
                key = key.ToLower();

                String[] values = arg.ToLower().Split('=');
                if (values[0] == key)
                {
                    return true;
                }
            }

            return false;
        }

        //------------------------------------
        // show help
        //-----------------------------------
        void PrintHelp()
        {
            Console.WriteLine("Example:   port=50000 ");
            Console.WriteLine("Values above are the defaults if no parameters are listed.");


        }

        //-----------------------------------------
        // check the arguments
        //------------------------------------------
        void ProcessArguments(string[] args)
        {

            if (ArgumentExists(args, "--help"))
            {
                PrintHelp(); Environment.Exit(0);

            }
            if (ArgumentExists(args, "/?help"))
            {
                PrintHelp(); Environment.Exit(0);


            }

            if (ArgumentExists(args, "port"))
            {
                try
                {
                    RemotePort = Convert.ToInt32(GetArgument(args, "port"));
                }
                catch (Exception e)
                {
                        Console.WriteLine("RemotePort-ToInt32: " + e.Message);
                        Console.WriteLine("GetArgs=" + GetArgument(args, "port"));
                    
                }

                if (RemotePort < 1 || RemotePort > 65535)
                    RemotePort = 50000;
            }


        }

        void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            int DownCounter = 1;
            lock (RemoteListLock)
            {
                Console.SetCursorPosition(Left, Top);
                Console.WriteLine("Port=" + RemotePort.ToString() + " Total Clients = " + Clientlist.Count);
                foreach ( RemoteList Client in Clientlist)
                {
                    Int64 ms = (DateTime.Now.Ticks - Client.LastTick) / TimeSpan.TicksPerMillisecond;
                    String CountText = "Bytes";
                    String ByteText = Client.Counter.ToString();
                    if(Client.Counter > 10240)
                    {
                        CountText = "KB";
                        ByteText = (Client.Counter / 1024).ToString();

                    }

                    if (Client.Counter > 102400000)
                    {
                        CountText = "MB";
                        ByteText = (Client.Counter / (1024*1024)).ToString();

                    }


                    String s = String.Format("{0}:{1} {2} {3}  Last Seen {4}ms", Client.SourceAddress, Client.Port, ByteText, CountText, ms);

                    Console.SetCursorPosition(Left, Top+DownCounter);
                    Console.Write(new String(' ', Console.BufferWidth));
                    Console.SetCursorPosition(Left, Top + DownCounter);
                    Console.WriteLine(s);
                    DownCounter++;
                }

            }

        }

    }
    class RemoteList
    {
        public IPAddress SourceAddress;
        public Int32 Port;
        public Int64 Counter;
        public Int64 LastTick;        
    }
}
