using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net.Sockets;

namespace QUIConnect
{
    public static class HolePunching
    {
        private static SHA256 hasher = SHA256.Create();
        public static IPAddress[] GetIPInfoFromIPOrHostname(string iporhostname)
        {
            if(iporhostname == null)
            {
                return null;
            }
            if(IPAddress.TryParse(iporhostname,out IPAddress parsedip))
            {
                return new IPAddress[] { parsedip };
            }
            try
            {
                IPHostEntry entry = Dns.GetHostEntry(iporhostname);
                return entry.AddressList;
            }
            catch (Exception)
            {
                return null;
            }
        }
        public static byte[] IPv4EndpointToDatagram(IPEndPoint endpoint)
        {
            if(endpoint.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Must be IPv4 Address");
            }
            if(endpoint.Port > ushort.MaxValue || endpoint.Port < ushort.MinValue)
            {
                throw new ArgumentException("Invalid Port!");
            }
            byte[] toReturn = new byte[38];//4 bytes for IP address, 2 bytes for port, and 32 for hash.
            Array.Copy(endpoint.Address.GetAddressBytes(), toReturn, 4);
            Array.Copy(BitConverter.GetBytes((ushort)endpoint.Port), 0, toReturn, 4, 2);
            byte[] hash = hasher.ComputeHash(toReturn, 0, 6);
            Array.Copy(hash, 0, toReturn, 6, 32);
            return toReturn;
        }
        public static IPEndPoint DatagramToIPv4Endpoint(Span<byte> datagram)
        {
            if(datagram.Length != 38)
            {
                throw new ArgumentException("Proper datagram must be 38 bytes long.");
            }
            Span<byte> hash = hasher.ComputeHash(datagram.Slice(0,6).ToArray());
            if(hash.SequenceEqual(datagram.Slice(6)))//Verify Hash
            {
                IPAddress address = new IPAddress(datagram.Slice(0, 4));
                int port = BitConverter.ToUInt16(datagram.Slice(4, 2));
                return new IPEndPoint(address, port);
            }
            return null;
        }
        public static async Task<IPEndPoint> PunchHole(IPAddress remoteHost)
        {
            using (UdpClient udptest = new UdpClient(42069))
            {
                if (OperatingSystem.IsWindows()) { udptest.AllowNatTraversal(true); }
                Task<UdpReceiveResult> receivedResponse = udptest.ReceiveAsync();
                int portcounter = 49152;

                IPEndPoint standardEndpoint = new IPEndPoint(remoteHost, 42069);
                byte[] standardDatagram = IPv4EndpointToDatagram(standardEndpoint);
                byte[] variableDatagram;
                while (receivedResponse.IsCompleted == false)
                {
                    int testport = portcounter++;
                    if (testport >= 65536)
                    {
                        portcounter = 1024;
                        testport = portcounter++;
                    }
                    if (testport % 100 == 52)
                    {
                        await udptest.SendAsync(standardDatagram, standardDatagram.Length, standardEndpoint);
                    }
                    IPEndPoint toSend = new IPEndPoint(remoteHost, testport);
                    variableDatagram = IPv4EndpointToDatagram(toSend);
                    udptest.Send(variableDatagram, variableDatagram.Length, toSend);
                    await Task.Delay(1);
                }
                UdpReceiveResult result = await receivedResponse;
                Console.WriteLine("Received Response from :" + result.RemoteEndPoint.Address.ToString() + " port " + result.RemoteEndPoint.Port);
                IPEndPoint externalSelf = DatagramToIPv4Endpoint(result.Buffer);
                Console.WriteLine("Remote Host reports that we are: " + externalSelf.Address.ToString() + " port " + externalSelf.Port);
                if (remoteHost.ToString() != result.RemoteEndPoint.Address.ToString())
                {
                    Console.WriteLine("WARNING! REMOTE ENDPOINT DOESN'T MATCH!");
                    Console.WriteLine("RemoteHost should be = " + remoteHost.ToString());
                }
                byte[] finalDatagram = IPv4EndpointToDatagram(result.RemoteEndPoint);
                for (int i = 0; i < 2; i++)//send it 2 times to help ensure delivery
                {
                    udptest.Send(finalDatagram, finalDatagram.Length, result.RemoteEndPoint);
                }
                return result.RemoteEndPoint;
            }
        }
    }
}
