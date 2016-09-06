using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Timers;

namespace PRTG_UDP_Qos
{
    class Program
    {
        List<PacketList> Packets = new List<PacketList>();          // list of packets sent
        Object PacketListLock = new object();                       // lock of the list for access
        UdpClient socket;                                           // udp socket 

        IPAddress RemoteIP = IPAddress.Parse("127.0.0.1");          // Remote IP Address
        Int32 RemotePort = 50000;                                   // Remote UDP Port
        Int32 PacketSize = 1000;                                    // How many bytes to send
        Int32 PacketCount = 50;                                     // how many packets
        Int32 TimeOutSeconds = 60;                                  // How long to wait
        bool Stop = false;                                          // used to issue stop command if all is done
        Int32 PacketRXCounter = 0;                                  // how many packets have been received
        Int32 OutOfOrderCounter = 0;                                // how many were out of order
        bool DebugOutput = false;                                   // show debug output
        Int32 PacketToSend = 0;                                     // used to track which packet to send next
        Int64 LastPacketSentTicks = 0;                              // how long has it been since we have seen a packet
        object LastPacketSentTicksObject = new object();            // used to lock the variable to prevent access violation
        Int32 PacketNotReceivedSendNextms = 1000;                   // how long to wait before sending the next packet

        //----------------------------------------------------------------------
        // Open the socket and start a receiver.  Then send one packet.  Wait for that packet to be
        // received and send another.  A timing loop is also started and if we have not received a reply
        // in the time out period send another packet.  Try this for TimeOutSeconds and or PacketCount packets
        //-------------------------------------------------------------------------

        static void Main(string[] args)
        {
            Program myProgram = new Program();                      // create a new class instance of the application

            //myProgram.DebugOutput = true;

            myProgram.ProcessArguments(args);                       // process the arguments

            try
            {
                myProgram.socket = new UdpClient(myProgram.RemotePort);         // bind the port
            }
            catch (Exception e)
            {
                if (myProgram.DebugOutput)
                {
                    Console.WriteLine("Main:udpclient");
                    Console.WriteLine(e.Message);
                }
                myProgram.ErrorExit();
            }


            if (myProgram.DebugOutput)
            {
                Console.WriteLine("IP=" + myProgram.RemoteIP);
                Console.WriteLine("Port=" + myProgram.RemotePort);
                Console.WriteLine("Count=" + myProgram.PacketCount);
                Console.WriteLine("Size=" + myProgram.PacketSize);
                Console.WriteLine("TimeOut=" + myProgram.TimeOutSeconds);


            }
            try
            {
                myProgram.socket.BeginReceive(new AsyncCallback(myProgram.GetData), null);      // start receiving data
            }
            catch (Exception e)
            {
                if (myProgram.DebugOutput)
                {
                    Console.WriteLine("Main:BeginReceive");
                    Console.WriteLine(e.Message);
                }
                myProgram.ErrorExit();

            }


            // Send first packet
            myProgram.SendNextData();

            // Set up time to send the remaining blocks
            Timer _timer; // From System.Timers
            _timer = new Timer(1000);
            _timer.Elapsed += new ElapsedEventHandler(myProgram._timer_Elapsed);
            _timer.Start();




            Int64 TimeOutTicks = myProgram.TimeOutSeconds * TimeSpan.TicksPerSecond;            // Calculate time out to end application
            Int64 StartTick = DateTime.Now.Ticks;                                               // find out when to start


            while ((DateTime.Now.Ticks - StartTick) < TimeOutTicks)                             // loop until done
            {
                if (myProgram.Stop)
                    break;

            }


            // prepare variables for the calculating
            Int64 Average_Accumulator = 0;
            Int64 Average_Counter = 0;
            Int64 Jitter_Accumulator = 0;
            Int64 Average_ticks = -1;
            Int64 Jitter = -1;

            Int64 Low = 65536;
            Int64 High = -1;
            Int64 Lost = myProgram.PacketCount;     // Start with total packets sent
            Int64 Duplicate = 0;


            // Calculate the average
            foreach (PacketList pl in myProgram.Packets)
            {
                if (myProgram.DebugOutput)
                {
                    Console.WriteLine("PacketID=" + pl.PacketID.ToString() + " ms=" + (pl.TickDiff / TimeSpan.TicksPerMillisecond).ToString());

                }

                if (pl.RXCounter > 0)           // decrement for each one and if we got them all the end result is 0
                    Lost--;

                if (pl.RXCounter > 1)           // if we got more than one of this packet then add it to the duplicate counter
                    Duplicate += (pl.RXCounter - 1);

                if (pl.TickDiff != -1)          // if diff is not -1 calcualte the average for it
                {
                    if ((pl.TickDiff / TimeSpan.TicksPerMillisecond) > High)        // Check to see if this is a new highh
                        High = (pl.TickDiff / TimeSpan.TicksPerMillisecond);
                    if ((pl.TickDiff / TimeSpan.TicksPerMillisecond) < Low)         // check to see if this is a new low
                        Low = (pl.TickDiff / TimeSpan.TicksPerMillisecond);
                    Average_Accumulator += (pl.TickDiff / TimeSpan.TicksPerMillisecond);        // add to average accumultor
                    Average_Counter += 1;                                                       // increment counter (average divisor)
                }

            }
            if (Average_Accumulator > 0)                                    // we can't divide by zero
                Average_ticks = Average_Accumulator / Average_Counter;


            //////////////////////////////////////
            // Jitter, calculated is variance
            //////////////////////////////////////
            foreach (PacketList pl in myProgram.Packets)
            {
                if (pl.TickDiff != -1)
                {
                    Jitter_Accumulator += (long)Math.Pow(((pl.TickDiff / TimeSpan.TicksPerMillisecond) - Average_ticks), 2);  // add value in ms (squared) to accumulator

                }

            }
            if (Average_Counter > 0)
                Jitter = (long)(Jitter_Accumulator / (Average_Counter));


            if (myProgram.DebugOutput)
            {

                Console.WriteLine("Average Latency:  " + Average_ticks.ToString() + "ms");
                if (Low != 65536)
                    Console.WriteLine("Low:  " + Low.ToString() + "ms");
                else
                    Console.WriteLine("Low: -1ms");

                Console.WriteLine("High:  " + High.ToString() + "ms");
                Console.WriteLine("Jitter:  " + Jitter.ToString() + "ms");
                Console.WriteLine("Out Of Order:  " + myProgram.OutOfOrderCounter.ToString());
                Console.WriteLine("Duplicate:  " + Duplicate.ToString());
                Console.WriteLine("Lost:  " + Lost.ToString());


                Console.WriteLine("Press any key to continue");
                Console.ReadKey();

            }
            else
            {
                //Console.WriteLine("<?xml version=\"1.0\" encoding=\"Windows - 1252\" ?>");
                Console.WriteLine("<prtg>");
                Console.WriteLine("     <result>");
                Console.WriteLine("          <channel>Average Latency</channel>");
                Console.WriteLine("          <value>" + Average_ticks.ToString() + "</value>");
                Console.WriteLine("          <Unit>Custom</Unit>");
                Console.WriteLine("          <CustomUnit>ms</CustomUnit>");
                Console.WriteLine("     </result>");
                Console.WriteLine("     <result>");
                Console.WriteLine("          <channel>High</channel>");
                Console.WriteLine("          <value>" + High.ToString() + "</value>");
                Console.WriteLine("          <Unit>Custom</Unit>");
                Console.WriteLine("          <CustomUnit>ms</CustomUnit>");
                Console.WriteLine("     </result>");
                Console.WriteLine("     <result>");
                Console.WriteLine("          <channel>Low</channel>");
                Console.WriteLine("          <value>" + Low.ToString() + "</value>");
                Console.WriteLine("          <Unit>Custom</Unit>");
                Console.WriteLine("          <CustomUnit>ms</CustomUnit>");
                Console.WriteLine("     </result>");
                Console.WriteLine("     <result>");
                Console.WriteLine("          <channel>Jitter</channel>");
                Console.WriteLine("          <value>" + Jitter.ToString() + "</value>");
                Console.WriteLine("          <Unit>Custom</Unit>");
                Console.WriteLine("          <CustomUnit>ms</CustomUnit>");
                Console.WriteLine("     </result>");
                Console.WriteLine("     <result>");
                Console.WriteLine("          <channel>Out Of Order</channel>");
                Console.WriteLine("          <value>" + myProgram.OutOfOrderCounter.ToString() + "</value>");
                Console.WriteLine("          <Unit>Count</Unit>");
                Console.WriteLine("     </result>");
                Console.WriteLine("     <result>");
                Console.WriteLine("          <channel>Duplicates</channel>");
                Console.WriteLine("          <value>" + Duplicate.ToString() + "</value>");
                Console.WriteLine("          <Unit>Count</Unit>");
                Console.WriteLine("     </result>");
                Console.WriteLine("     <result>");
                Console.WriteLine("          <channel>Lost</channel>");
                Console.WriteLine("          <value>" + Lost.ToString() + "</value>");
                Console.WriteLine("          <Unit>Count</Unit>");
                Console.WriteLine("     </result>");
                Console.WriteLine("     <text>OK</text>");
                Console.WriteLine("</prtg>");

                Environment.Exit(0);

            }


        }

        void ErrorExit()
        {

            Console.WriteLine("<prtg>");
            Console.WriteLine("     <result>");
            Console.WriteLine("          <channel>Average Latency</channel>");
            Console.WriteLine("          <value>-1</value>");
            Console.WriteLine("          <Unit>Custom</Unit>");
            Console.WriteLine("          <CustomUnit>ms</CustomUnit>");
            Console.WriteLine("     </result>");
            Console.WriteLine("     <result>");
            Console.WriteLine("          <channel>High</channel>");
            Console.WriteLine("          <value>-1</value>");
            Console.WriteLine("          <Unit>Custom</Unit>");
            Console.WriteLine("          <CustomUnit>ms</CustomUnit>");
            Console.WriteLine("     </result>");
            Console.WriteLine("     <result>");
            Console.WriteLine("          <channel>Low</channel>");
            Console.WriteLine("          <value>-1</value>");
            Console.WriteLine("          <Unit>Custom</Unit>");
            Console.WriteLine("          <CustomUnit>ms</CustomUnit>");
            Console.WriteLine("     </result>");
            Console.WriteLine("     <result>");
            Console.WriteLine("          <channel>Jitter</channel>");
            Console.WriteLine("          <value>-1</value>");
            Console.WriteLine("          <Unit>Custom</Unit>");
            Console.WriteLine("          <CustomUnit>ms</CustomUnit>");
            Console.WriteLine("     </result>");
            Console.WriteLine("     <result>");
            Console.WriteLine("          <channel>Out Of Order</channel>");
            Console.WriteLine("          <value>-1</value>");
            Console.WriteLine("          <Unit>Count</Unit>");
            Console.WriteLine("     </result>");
            Console.WriteLine("     <result>");
            Console.WriteLine("          <channel>Duplicates</channel>");
            Console.WriteLine("          <value>-1</value>");
            Console.WriteLine("          <Unit>Count</Unit>");
            Console.WriteLine("     </result>");
            Console.WriteLine("     <result>");
            Console.WriteLine("          <channel>Lost</channel>");
            Console.WriteLine("          <value>-1</value>");
            Console.WriteLine("          <Unit>Count</Unit>");
            Console.WriteLine("     </result>");
            Console.WriteLine("     <text>OK</text>");
            Console.WriteLine("</prtg>");

            Environment.Exit(0);



        }
        //--------------------------------------------------------------
        // Once a packet is recevied update the List of packets sent with approprate information
        // it checks to make sure the data received is the same as data sent and if the packet is out of order
        // and if it has been received more than once
        //--------------------------------------------------------------------------------------
        void UpdatePacketList(IPAddress SourceAddress, byte[] data)
        {
            if (RemoteIP.ToString() == SourceAddress.ToString())               // make sure we received this packet from where we sent it
            {
                if (data.Length > 4)                                            // it has to be bigger than 4 bytes
                {

                    byte[] packet = new byte[data.Length - 4];                      // make a buffer for the data
                    Buffer.BlockCopy(data, 4, packet, 0, data.Length - 4);          // copy it to the a superate buffer
                    Int32 ID = BitConverter.ToInt32(data, 0);                       // get the packet id

                    if (DebugOutput)
                    {
                        Console.WriteLine("Received Packet:" + ID.ToString());
                    }


                    lock (PacketListLock)
                    {
                        for (int t = 0; t < Packets.Count; t++)                     // iterate the sent packets
                        {



                            if (Packets[t].PacketID == ID)                          // if the id matches a sent id
                            {
                                if (ByteArrayCompare(packet, Packets[t].Data))      // compre the buffers of random data
                                {
                                    Packets[t].TickDiff = DateTime.Now.Ticks - Packets[t].Tick; // get the amount of ticks between the sent and received
                                    Packets[t].RXCounter++;                                     // mark this packet as being recieved by 1

                                }
                                if (PacketRXCounter != ID)                                      // the PacketRX counter should match the ID meaning we got it in order
                                    OutOfOrderCounter++;                                        // if it didn't then we need to increment the out of order counter
                                PacketRXCounter++;                                              // since we got a packet we need to increase the counter
                                SendNextData();                                                 // Send the next block 

                            }
                        }
                    }
                }
            
            }

            if (Packets.Count == PacketCount)                                                   // see if we received as many as we are supposed to send
            {                                                                                   // if we did then we may be done
                Int32 count = 0;
                foreach(PacketList pl in Packets)                                               // iterate each packet and make sure they are all marked as received
                {
                    if (pl.RXCounter > 0)
                        count++;

                }
                if (count == PacketCount)                                                       // did we get them all?
                {

                    Stop = true;                                                                // set the stop
                    if (DebugOutput)
                        Console.WriteLine("All Packets Received.  Issuing Stop");
                }
            }
        }

        //----------------------------------------------------
        // Compare to byte arrays
        //------------------------------------------------------
        bool ByteArrayCompare(byte[] a1, byte[] a2)
        {
            if (a1.Length != a2.Length)
                return false;

            for (int i = 0; i < a1.Length; i++)
                if (a1[i] != a2[i])
                    return false;

            return true;
        }

        //-------------------------------------------------
        // called when a packet comes in
        //-------------------------------------------------
        private void GetData(IAsyncResult res)
        {
            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, RemotePort);          // default endpoint

            try
            {
                byte[] received = socket.EndReceive(res, ref RemoteIpEndPoint);         // receive the data
                UpdatePacketList(RemoteIpEndPoint.Address, received);                   // check the packet
            }
            catch (Exception e)
            {
                if (DebugOutput)
                {
                    Console.WriteLine("GetData:EndReceive");
                    Console.WriteLine(e.Message);
                }
            }

                
            if (!Stop)                                                                 // dont start receiving again if stop is set
            {
                try {
                    socket.BeginReceive(new AsyncCallback(GetData), null);             // start receiving again
                }
                catch (Exception e)
                {

                    if (DebugOutput)
                    {
                        Console.WriteLine("GetData:BeginReceive");

                        Console.WriteLine(e.Message);
                    }
                    ErrorExit();
                }

            }
        }


        //-------------------------------------
        // Send next packet
        //-------------------------------------
        void SendNextData()
        {

            IPEndPoint target = new IPEndPoint(RemoteIP, RemotePort);       // Set net endpoint
            Int32 t = PacketToSend;                                         // set packet number

            if (PacketToSend == PacketCount)                                // dont send another if we already sent enough
                return;

            lock (LastPacketSentTicksObject)                                 // lock the ticks
            {
                LastPacketSentTicks = DateTime.Now.Ticks;                   // set the last packet sent timer
            }
            try
            {
                byte[] data = RandomBytes(PacketSize);                      // fill with random data
                byte[] packetid = BitConverter.GetBytes(t);                 // get bytes for packet number
                byte[] buffer = Combine(packetid, data);                    // combine the two buffers


                try
                {
                    socket.Send(buffer, buffer.Length, target);             // send the packet
                    if (DebugOutput)
                    {
                        Console.WriteLine("Send Packet:" + PacketToSend.ToString());
                    }

                }
                catch (Exception e)
                {
                    if (DebugOutput)
                    {
                        Console.WriteLine("SendData:Send");
                        Console.WriteLine(e.Message);
                    }
                    ErrorExit();
                }


                PacketList pl = new PacketList();                           // make a new packetlist class
                pl.Data = data;                                             // set the data and id and time
                pl.PacketID = t;
                pl.Tick = DateTime.Now.Ticks;

                lock (PacketListLock)
                {
                    Packets.Add(pl);                                        // add to the list of sent packets
                }
                PacketToSend++;                                             // increment the packettosend counter

            }
            catch (Exception e)
            {
                if (DebugOutput)
                {
                    Console.WriteLine("SendData");
                    Console.WriteLine(e.Message);

                }

            }

        }
        //-------------------
        // combine two byte arrays
        //------------------------------------
        public static byte[] Combine(byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }

        //---------------------------
        // get argument value based on left hand side of the =
        //--------------------------------------
        String GetArgument(string[] args, string key)
        {
            key = key.ToLower();

            if (DebugOutput)
                Console.WriteLine("GetArgument Key=" + key);

            foreach (String arg in args)
            {
                
                String[] values = arg.ToLower().Split('=');
                if (DebugOutput)
                {
                    Console.WriteLine("GetArgument arg=:" + arg);
                    Console.WriteLine("GetArgument split count=" + values.Length);
                    for(int t=0;t<values.Length;t++)
                        Console.WriteLine("GetArgument split[" + t.ToString() + "]=" + values[t]);

                }

                if (values.Length == 2) {
                    if (values[0] == key)
                    {
                        if (DebugOutput)
                            Console.WriteLine("GetArgument ReturnValkue =" + values[1]);
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

        //-------------------------
        // generate random alphanumeric data
        //----------------------------------
        byte[]  RandomBytes(int length)
        {
            String chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            byte[] data = new byte[length];
            var random = new Random();

            for (int i = 0; i < length; i++)
            {
                data[i]= Convert.ToByte(chars[random.Next(chars.Length)]);
                
            }

            return data;
        }

        //------------------------------------
        // show help
        //-----------------------------------
        void PrintHelp()
        {
            Console.WriteLine("Example:  ip=172.0.0.1 port=50000 size=1000 count=50 timeout=10 debug");
            Console.WriteLine("Values above are the defaults if no parameters are listed.");
            Console.WriteLine("Timeout values are in seconds");
            Console.WriteLine("The debug command provides debugging output.");


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
            if (ArgumentExists(args, "debug"))
            {
                DebugOutput = true;
            }


            if (ArgumentExists(args, "ip"))
            {
                IPHostEntry host;
                host = Dns.GetHostEntry(GetArgument(args, "ip"));
                foreach (IPAddress ip in host.AddressList)
                {
                    RemoteIP = ip;
                }

            }

            if (ArgumentExists(args, "port"))
            {
                try {
                    RemotePort = Convert.ToInt32(GetArgument(args, "port"));
                }
                catch (Exception e)
                {
                    if (DebugOutput)
                    {
                        Console.WriteLine("RemotePort-ToInt32: " + e.Message);
                        Console.WriteLine("GetArgs=" + GetArgument(args, "port"));
                    }
                }

                if (RemotePort < 1 || RemotePort > 65535)
                    RemotePort = 50000;
            }
            if (ArgumentExists(args, "size"))
            {
                try { 
                PacketSize = Convert.ToInt32(GetArgument(args, "size"));
                }
                catch (Exception e)
                {
                    if (DebugOutput)
                    {
                        Console.WriteLine("size-ToInt32: " + e.Message);
                        Console.WriteLine("GetArgs=" + GetArgument(args, "size"));

                    }
                }

                if (PacketSize < 1 || PacketSize > 2000)
                        PacketSize = 1000;
            }
            if (ArgumentExists(args, "count"))
            {
                try {
                    PacketCount = Convert.ToInt32(GetArgument(args, "count"));
                }
                catch (Exception e)
                {
                    if (DebugOutput)
                    {
                        Console.WriteLine("count-ToInt32: " + e.Message);
                        Console.WriteLine("GetArgs=" + GetArgument(args, "count"));

                    }

                }
                if (PacketCount < 10 || PacketCount > 100)
                    PacketCount = 10;
            }
            if (ArgumentExists(args, "timeout"))
            {
                try {
                    TimeOutSeconds = Convert.ToInt32(GetArgument(args, "timeout"));
                }
                catch (Exception e)
                {
                    if (DebugOutput)
                    {
                        Console.WriteLine("timeout-ToInt32: " + e.Message);
                        Console.WriteLine("GetArgs=" + GetArgument(args, "timeout"));
                    }


                }
                if (TimeOutSeconds < 5 || TimeOutSeconds > 30)
                    TimeOutSeconds = 10;
            }


        }

        //-------------------------------------
        // timer, if we haven't seen a response in 1s send the next packet
        //---------------------------------------
        void  _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (LastPacketSentTicksObject)
            {
                Int64 ms = (DateTime.Now.Ticks - LastPacketSentTicks) / TimeSpan.TicksPerSecond;
                if (ms > PacketNotReceivedSendNextms)
                    SendNextData();
            }


            
        }



        
        

    }
    class PacketList
    {
        public Int32 PacketID;
        public Int64 Tick;
        public Byte[] Data;
        public Int64 TickDiff=-1;
        public Int32 RXCounter = 0;
    }

}
