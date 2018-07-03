using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MicroCoinGPUMiner
{
    class Program
    {

        static bool CanBeModifiedOnLastChunk(long MessageTotalLength, ref long StartBytePos)
        {
            StartBytePos = (((((MessageTotalLength) * 8) + 72) % 512) / 8) - (8 + 9);
            return (StartBytePos >= 0) && ((StartBytePos % 4) == 0) && (StartBytePos <= 48);
        }

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        static string d, p, f, h;
        static int b;
        static string stdate = Convert.ToString(DateTime.Now);
        static void consolwriter()
        {
            Console.CursorVisible = false;
            while (true)
            {
                try
                {
                    string stdate2 = Convert.ToString(DateTime.Now);
                    var jo = JObject.Parse(d).GetValue("params").Value<JArray>();
                    var n = jo[0].Value<JObject>();
                    Console.Clear();
                    string test = "Block: " + n.GetValue("block").Value<int>()+ "\n" +
                        "timestamp: "+ n.GetValue("timestamp").Value<string>()+"\n" +
                        "target_pow: "+ n.GetValue("target_pow").Value<string>()+"\n" +
                        "target: " + n.GetValue("target").Value<int>().ToString("X") + "\n" +
                        "Blocks Found: "+ Convert.ToString(b)+"\n" +
                        ""+"\n" +
                        Encoding.ASCII.GetString(StringToByteArray(p))+"\n"+
                        "Elapsed time: "+Convert.ToString(DateTime.Parse(stdate2) - DateTime.Parse(stdate)) +"\n" +
                        "Last block found: "+ f;
                    Console.Write(test);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                Thread.Sleep(999);
            }
        }

        static void Main(string[] args)
        {
            Console.WindowHeight = 10;
            Console.WindowWidth = 80;
            Console.SetBufferSize(80, 10);
            string minerName = "";
            bool startth = true;
            Console.Title = "CuDaMiner";
            
            try
            {
                if (args.Length < 2)
                {
                    minerName = "mcu.dll";

                    Process miner = Process.Start(new ProcessStartInfo(minerName)
                    {
                        UseShellExecute = false
                    });
                }
                else
                {
                    minerName = args[1];
                }
            }
            catch
            {
                Console.WriteLine("missing dll file");
                Console.ReadKey();
                Environment.Exit(0);
            }

            while (true)
            {
                File.WriteAllText(".\\datain00.txt", "$00000000\n$00000000");
                try {
                    using (TcpClient tcp = new TcpClient(AddressFamily.InterNetwork))
                    {
                        tcp.NoDelay = true;
                        tcp.ExclusiveAddressUse = true;
                        tcp.Connect("127.0.0.1", 4009);
                        while (tcp.Available < 1)
                        {
                            Thread.Sleep(1);
                        }

                        StreamReader reader = new StreamReader(tcp.GetStream());
                        string lastTimeStamp = "", lastNonce = "";
                        string part1 = "";
                        string payload = "";
                        string payload_start = "";
                        string part3 = "";
                        string header = "";
                        lastNonce = "00000000";
                        StreamWriter sw = new StreamWriter(tcp.GetStream(), Encoding.ASCII);
                        string ftime = "";
                        while (true)
                        {
                            if (!tcp.Connected) break;
                            if (tcp.Available > 0)
                            {
                                string data = reader.ReadLine();

                                try
                                {
                                    var jo = JObject.Parse(data).GetValue("params").Value<JArray>();
                                    var n = jo[0].Value<JObject>();
                                    
                                    if (startth == true)
                                    {
                                        Thread cw = new Thread(consolwriter);
                                        cw.Start();
                                        startth = false;
                                    }

                                    part1 = n.GetValue("part1").Value<string>();
                                    payload = n.GetValue("payload_start").Value<string>();
                                    part3 = n.GetValue("part3").Value<string>();
                                    header = part1 + payload + part3;
                                    long j = 0;
                                    payload_start = n.GetValue("payload_start").Value<string>();
                                    string u = n.GetValue("target_pow").Value<string>().Substring(8, 8);
                                    int intAgain = int.Parse(u, System.Globalization.NumberStyles.HexNumber)-1;
                                    header = part1 + payload + part3;
                                    while (!CanBeModifiedOnLastChunk(8 + (header.Length / 2), ref j))
                                    {
                                        payload = payload + BitConverter.ToString(new byte[] { (byte)'-' });
                                        header = part1 + payload + part3;
                                    }

                                    f = ftime;
                                    h = header;
                                    p = payload;
                                    d = data;
                                    for (int i = 0; i < 10; i++)
                                    {
                                        try
                                        {
                                            File.WriteAllText("./headerout00.txt", header);
                                            File.WriteAllText("./targetpow.tp", Convert.ToString(intAgain));
                                            break;
                                        }
                                        catch { }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.Message);
                                }
                            }
                            string[] str = null;
                            for (int i = 0; i < 10; i++)
                            {
                                try
                                {
                                    str = File.ReadAllLines(".\\datain00.txt");
                                    break;
                                }
                                catch { }
                            }
                            if (str == null) return;
                            str[0] = str[0].Replace("$", "");
                            str[1] = str[1].Replace("$", "");
                            if (str[0] != lastNonce)
                            {
                                lastNonce = str[0];
                                string toSend = "{\"method\":\"miner-submit\",\"params\":[{\"payload\":\"" + payload + "\",\"timestamp\":" +
                                    Convert.ToString(Convert.ToInt32(str[1], 16)) + ",\"nonce\":" + Convert.ToString(Convert.ToInt32(lastNonce, 16)) + "}]}";
                                sw.WriteLine(toSend);
                                sw.Flush();
                                while (tcp.Available < 1) Thread.Sleep(10);
                                StringBuilder sb = new StringBuilder();
                                while (tcp.Available > 0)
                                {
                                    sb.Append(reader.ReadLine());
                                }

                                JObject js = JObject.Parse(sb.ToString());
                                try
                                {
                                    if (js["error"] != null)
                                    {
                                        if (js.Value<string>("error").Length > 0)
                                        {
                                            Console.WriteLine(js.Value<string>("error"));
                                            continue;
                                        }
                                    }
                                }
                                catch
                                {

                                }
                                ftime = Convert.ToString(DateTime.Now);
                                //System.Media.SoundPlayer player = new System.Media.SoundPlayer("found.wav");
                                //player.Play();
                                b++;
                                string stdate2 = Convert.ToString(DateTime.Now);
                                var jo = JObject.Parse(d).GetValue("params").Value<JArray>();
                                var n = jo[0].Value<JObject>();
                                Console.Clear();
                                string test = "Block: " + n.GetValue("block").Value<int>() +"\n" +
                                    "timestamp: " + n.GetValue("timestamp").Value<string>() + "\n" +
                                    "target_pow: " + n.GetValue("target_pow").Value<string>() + "\n" +
                                    "target: " + n.GetValue("target").Value<int>().ToString("X") + "\n" +
                                    "Blocks Found: " + Convert.ToString(b) + "\n" +
                                    "" + "\n" +
                                    Encoding.ASCII.GetString(StringToByteArray(p)) + "\n" +
                                    "Elapsed time: " + Convert.ToString(DateTime.Parse(stdate2) - DateTime.Parse(stdate)) + "\n" +
                                    "Last block found: " + f;
                                Console.Write(test);

                            }

                            Thread.Sleep(200);
                        }
                    }
                }
                catch(Exception re)
                {
                    Console.WriteLine(re.Message);

                }
            }
        }
    }
}
