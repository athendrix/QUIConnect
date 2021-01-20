using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace QUIConnect
{
    class Program
    {
        static void Main(string[] args)
        {
            bool RunningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER").ToLower() == "true";
            IPAddress other = null;
            while (other == null)
            {
                string REMOTE = Environment.GetEnvironmentVariable("REMOTE");
                other = HolePunching.GetIPInfoFromIPOrHostname(REMOTE)?.FirstOrDefault();
                if (other == null)
                {
                    if (!RunningInContainer)
                    {
                        Console.WriteLine("Enter hostname or IPAddress for other party.");
                        other = HolePunching.GetIPInfoFromIPOrHostname(Console.ReadLine())?.FirstOrDefault();
                    }
                    else
                    {
                        Console.WriteLine("A remote host must be specified via the \"REMOTE\" environment variable.");
                        Console.WriteLine("Please provide an IP address or a hostname.");
                        return;
                    }
                }
            }
            Console.WriteLine("Punching a hole to " + other.ToString() + ". This may take some time. Make sure the other party is punching from their side too.");
            Stopwatch sw = new Stopwatch();
            sw.Restart();
            Task<IPEndPoint> HolePunchTask = HolePunching.PunchHole(other);
            HolePunchTask.Wait();
            Console.WriteLine("Took " + sw.ElapsedMilliseconds / 1000.0 + " seconds to punch a hole.");
            IPEndPoint otherEndpoint = HolePunchTask.Result;
            using (UdpClient udpcon = new UdpClient(42069))
            {
                udpcon.Connect(otherEndpoint);
                Task<UdpReceiveResult> ReceiveResult = udpcon.ReceiveAsync();
                int count = 0;
                while (true)
                {
                    if (count++ % 10 == 0)
                    {
                        string toSend = Guid.NewGuid().ToString();
                        Console.WriteLine("Sending: " + toSend);
                        byte[] dgram = Encoding.UTF8.GetBytes(toSend);
                        udpcon.Send(dgram, dgram.Length);
                    }
                    while(ReceiveResult.IsCompleted)
                    {
                        if(ReceiveResult.IsCompletedSuccessfully)
                        {
                            Console.WriteLine("Received: " + Encoding.UTF8.GetString(ReceiveResult.Result.Buffer));
                        }
                        ReceiveResult = udpcon.ReceiveAsync();
                    }
                    Thread.Sleep(500);
                }
            }
        }
    }
}
