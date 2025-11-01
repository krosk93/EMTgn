using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using InvertedTomato.IO;
using Flurl.Http;
using System.Threading.Tasks;

namespace EMTgn
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var stops = (await "http://emtanemambtu.cat/emtappjsonws.php?function=getStopDetail".GetJsonAsync<StopsList>()).parada;
            if (!File.Exists(args[0]))
                return;
            using BinaryReader reader = new(File.Open(args[0], FileMode.Open));
            byte[] card = reader.ReadBytes(1024);
            ArraySegment<byte>[] trips = new ArraySegment<byte>[10];
            int curAddress = 0x2c0;
            for (int i = 0; i < 10; i++)
            {
                if (((curAddress + 0x10) % 0x40) == 0) curAddress += 0x10;
                trips[i] = new ArraySegment<byte>(card, curAddress, 0x10);
                curAddress += 0x10;
                if (!trips[i].Take(0xf).All(x => x == 0x0))
                {
                    Crc crc = CrcAlgorithm.CreateCrc16Mcrf4Xx();
                    crc.Append(trips[i].Take(0xf).ToArray());
                    var checksum = crc.ToByteArray().Aggregate<byte>((arg1, arg2) => (byte)(arg1 ^ arg2));
                    byte[] bytes = trips[i].Reverse().ToArray();
                    bool verified = checksum == bytes[0];
                    int minutes = bytes[1] >> 2;
                    int hours = ((bytes[1] & 0x3) << 3) + (bytes[2] >> 5);
                    int day = bytes[2] & 0b11111;
                    int month = bytes[3] >> 4;
                    int busNumber = (bytes[4] << 2) + (bytes[5] >> 6);
                    string busStop = ((bytes[6] << 9) + (bytes[7] << 1) + (bytes[8] >> 7)).ToString();
                    Stop busStopObj = stops.FirstOrDefault(x => x.codigo == busStop);
                    string busStopName = busStopObj?.nombre;
                    Console.WriteLine($"Viatge el {day.ToString().PadLeft(2, '0')}/{month.ToString().PadLeft(2, '0')} a les {hours.ToString().PadLeft(2, '0')}:{minutes.ToString().PadLeft(2, '0')} amb el bus {busNumber} a la parada {busStopName ?? busStop} ({ busStopObj?.lineas ?? "" }){(verified ? " - Checksum verificat" : String.Empty)}");
                }
            }
        }
    }

    class StopsList
    {
        public IEnumerable<Stop> parada { get; set; }
    }

    class Stop
    {
        public string codigo { get; set; }
        public string nombre { get; set; }
        public string latt { get; set; }
        public string @long { get; set; }
        public string lineas { get; set; }
    }
}
