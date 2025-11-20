using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using InvertedTomato.IO;
using Flurl.Http;
using System.Threading.Tasks;
using Spectre.Console;
using System.Text;

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
            

            StringBuilder sb = new();
            for (int i = card.Length - 1; i >= 0; i--)
            {
                sb.Append(Convert.ToString(card[i], 2).PadLeft(8, '0'));
            }
            sb.Replace("1101010111", "[red]1101010111[/]");
            AnsiConsole.MarkupLine(sb.ToString());

            AnsiConsole.WriteLine(string.Join(" ", sb.ToString().AllIndexesOf("1101010111").Select(x => Convert.ToString(((card.Length * 8) - x - 1) / 8, 16))));
            
            
            ArraySegment<byte>[] trips = new ArraySegment<byte>[10];
            int curAddress = 0x240;
            for (int i = 0; i < 3; i++)
            {
                if (((curAddress + 0x10) % 0x40) == 0) curAddress += 0x10;
                trips[i] = new ArraySegment<byte>(card, curAddress, 0x10);
                curAddress += 0x10;
                Crc crc = CrcAlgorithm.CreateCrc16Mcrf4Xx();
                crc.Append(trips[i].Take(0xf).ToArray());
                var checksum = crc.ToByteArray().Aggregate<byte>((arg1, arg2) => (byte)(arg1 ^ arg2));
                byte[] bytes = trips[i].Reverse().ToArray();
                bool verified = checksum == bytes[0];
                AnsiConsole.WriteLine($"Recàrrega {i + 1}:");
                AnsiConsole.WriteLine(string.Join("      ", bytes.Select(x => Convert.ToString(x, 16).PadLeft(2, '0'))));
                AnsiConsole.WriteLine(string.Join(string.Empty, bytes.Select(x => Convert.ToString(x, 2).PadLeft(8, '0'))));
            }

            curAddress = 0x2c0;
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

                    int operatorCompany = ((bytes[8] & 127) << 1) + (bytes[9] >> 7);

                    Dictionary<int, string> companies = new()
                    {
                        {219, "Reus Transports"},
                        {202, "EMT Tarragona"},
                        {8, "Renfe"},
                        {43, "Plana"}
                    };

                    int minutes = bytes[1] >> 2;
                    int hours = ((bytes[1] & 0x3) << 3) + (bytes[2] >> 5);
                    int day = bytes[2] & 0b11111;
                    int month = bytes[3] >> 4;
                    int busNumber = ((bytes[3] & 1) << 10) + (bytes[4] << 2) + (bytes[5] >> 6);
                    int zone = ((bytes[5] & 63) << 2) + (bytes[6] >> 6);
                    string busStop = (((bytes[6] & 63) << 9) + (bytes[7] << 1) + (bytes[8] >> 7)).ToString();
                    int line = ((bytes[9] & 127) << 4) + (bytes[10] >> 4);
                    Stop busStopObj = stops.FirstOrDefault(x => x.codigo == busStop);
                    string busStopName = busStopObj?.nombre;
                    string companyName = companies.FirstOrDefault(x => x.Key == operatorCompany).Value ?? $"No trobat ({operatorCompany})";
                    AnsiConsole.WriteLine($"Viatge amb {companyName} el {day.ToString().PadLeft(2, '0')}/{month.ToString().PadLeft(2, '0')} a les {hours.ToString().PadLeft(2, '0')}:{minutes.ToString().PadLeft(2, '0')} amb el vehicle {busNumber} des de {busStopName ?? busStop} ({line}) Zona: {zone}{(verified ? " - Checksum verificat" : String.Empty)}");
                    AnsiConsole.WriteLine(string.Join("      ", bytes.Select(x => Convert.ToString(x, 16).PadLeft(2, '0'))));
                    AnsiConsole.Markup($"[red]{Convert.ToString(checksum, 2).PadLeft(8, '0')}[/]");
                    AnsiConsole.Markup($"[blue]{Convert.ToString(minutes, 2).PadLeft(6, '0')}[/]");
                    AnsiConsole.Markup($"[yellow]{Convert.ToString(hours, 2).PadLeft(5, '0')}[/]");
                    AnsiConsole.Markup($"[green]{Convert.ToString(day, 2).PadLeft(5, '0')}[/]");
                    AnsiConsole.Markup($"[aqua]{Convert.ToString(month, 2).PadLeft(4, '0')}[/]");
                    AnsiConsole.Write(Convert.ToString(bytes[3] & 0b1111, 2).PadLeft(4, '0'));
                    AnsiConsole.Markup($"[maroon]{Convert.ToString(bytes[4], 2).PadLeft(8, '0')}[/]");
                    AnsiConsole.Markup($"[maroon]{Convert.ToString(bytes[5] >> 6, 2).PadLeft(2, '0')}[/]");
                    AnsiConsole.Markup($"[springgreen2]{Convert.ToString(bytes[5] & 63, 2).PadLeft(6, '0')}[/]");
                    AnsiConsole.Markup($"[springgreen2]{Convert.ToString(bytes[6] >> 6, 2).PadLeft(2, '0')}[/]");
                    AnsiConsole.Markup($"[fuchsia]{Convert.ToString(bytes[6] & 63, 2).PadLeft(6, '0')}[/]");
                    AnsiConsole.Markup($"[fuchsia]{Convert.ToString(bytes[7], 2).PadLeft(8, '0')}[/]");
                    AnsiConsole.Markup($"[fuchsia]{Convert.ToString(bytes[8] >> 7, 2)}[/]");
                    AnsiConsole.Markup($"[indianred]{Convert.ToString(bytes[8] & 127, 2).PadLeft(7, '0')}[/]");
                    AnsiConsole.Markup($"[indianred]{Convert.ToString(bytes[9] >> 7, 2)}[/]");
                    AnsiConsole.Markup($"[aquamarine1]{Convert.ToString(bytes[9] & 127, 2).PadLeft(7, '0')}[/]");
                    AnsiConsole.Markup($"[aquamarine1]{Convert.ToString(bytes[10] >> 4, 2).PadLeft(4, '0')}[/]");
                    AnsiConsole.Write(Convert.ToString((bytes[10] >> 2) & 3, 2).PadLeft(2, '0'));
                    AnsiConsole.Markup($"[lime]{Convert.ToString((bytes[10] & 2) >> 1, 2)}[/]");
                    AnsiConsole.Write(Convert.ToString(bytes[10] & 1, 2));
                    AnsiConsole.Write(Convert.ToString(bytes[11], 2).PadLeft(8, '0'));
                    AnsiConsole.Write(Convert.ToString(bytes[12], 2).PadLeft(8, '0'));
                    AnsiConsole.Write(Convert.ToString(bytes[13], 2).PadLeft(8, '0'));
                    AnsiConsole.Write(Convert.ToString(bytes[14], 2).PadLeft(8, '0'));
                    AnsiConsole.Write(Convert.ToString(bytes[15] >> 1, 2).PadLeft(7, '0'));
                    AnsiConsole.Markup($"[lime]{Convert.ToString(bytes[15] & 1, 2)}[/]");
                    AnsiConsole.WriteLine();

                    AnsiConsole.Markup("[red]checksum[/]");
                    AnsiConsole.Markup("[blue]mmmmmm[/]");
                    AnsiConsole.Markup("[yellow]hhhhh[/]");
                    AnsiConsole.Markup("[green]DDDDD[/]");
                    AnsiConsole.Markup("[aqua]MMMM[/]");
                    AnsiConsole.Markup("    ");
                    AnsiConsole.Markup("[maroon]bbbbbbbbbb[/]");
                    AnsiConsole.Markup("[springgreen2]zzzzzzzz[/]");
                    AnsiConsole.Markup("[fuchsia]ppppppppppppppp[/]");
                    AnsiConsole.Markup("[indianred]oooooooo[/]");
                    AnsiConsole.Markup("[aquamarine1]lllllllllll[/]");
                    AnsiConsole.Markup("  ");
                    AnsiConsole.Markup("[lime]t[/]");
                    AnsiConsole.Markup("                                        ");
                    AnsiConsole.Markup("[lime]t[/]");
                    AnsiConsole.WriteLine();
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
