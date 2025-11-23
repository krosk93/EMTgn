
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EMTgn.Providers;
using InvertedTomato.IO;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EMTgn;

internal sealed class ProcessCardCommand : AsyncCommand<ProcessCardCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path of the binary file containing a bin dump of a ATMCT card")]
        [CommandArgument(0, "<path>")]
        public string Path { get; init; }

        [CommandOption("-p|--pretty")]
        [DefaultValue(false)]
        public bool Pretty { get; init; }
    }

    static bool CheckCRC(ArraySegment<byte> segment)
    {
        Crc crc = CrcAlgorithm.CreateCrc16Mcrf4Xx();
        crc.Append(segment.Take(0xf).ToArray());
        var checksum = crc.ToByteArray().Aggregate<byte>((arg1, arg2) => (byte)(arg1 ^ arg2));
        return checksum == segment.Last();
    }

    static ArraySegment<byte> GetLine(byte[] card, int address)
    {
        return new ArraySegment<byte>(card, address, 0x10);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.Pretty)
        {
            AnsiConsole.MarkupLine("[bold][white on orangered1] ATM[/] Camp de Tarragona[/]");
            AnsiConsole.MarkupLine("[grey85 on grey85]    [/] Autoritat Territorial de la Mobilitat");
            AnsiConsole.MarkupLine("[grey85 on grey85]    [/] [bold]Pwned![/]");
            AnsiConsole.WriteLine();
        }
        using BinaryReader reader = new(File.Open(settings.Path, FileMode.Open));
        byte[] card = reader.ReadBytes(1024);
        
        int curAddress = 0x0;
        {
            var byteSegment = GetLine(card, curAddress);
            var bytes = byteSegment.Reverse().ToArray();
            uint cardId = (uint)((bytes[12] << 24) + (bytes[13] << 16) + (bytes[14] << 8) + bytes[15]);
            string cardIdString = cardId.ToString().PadLeft(10, '0').PadRight(12, 'X');
            byteSegment = GetLine(card, curAddress + 0x10);
            bytes = byteSegment.ToArray();
            string cardId2 = $"{new string(Convert.ToString(bytes[1],16).Reverse().ToArray())}{new string(Convert.ToString(bytes[2],16).Reverse().ToArray())}{new string(Convert.ToString(bytes[3],16).Reverse().ToArray())}{new string(Convert.ToString(bytes[4],16).Reverse().ToArray())}".TrimStart('0');
            AnsiConsole.MarkupLine($"[bold]Targeta:[/] {cardIdString[..4]}-{cardIdString[4..8]}-{cardIdString[8..]}");
            AnsiConsole.WriteLine($"         {cardId2}");
        }
        
        if(!settings.Pretty) {
            for (int i = 0; i < 48; i++)
            {
                if (((curAddress + 0x10) % 0x40) == 0) curAddress += 0x10;
                StringBuilder sb = new();
                StringBuilder hex = new();
                bool[] found = new bool[16];
                var byteSegment = new ArraySegment<byte>(card, curAddress, 0x10);
                var bytes = byteSegment.Reverse().ToArray();
                curAddress += 0x10;
                bool verified = CheckCRC(byteSegment);
                for(int j = 0; j < 16; j++)
                {
                    hex.Append(Convert.ToString(bytes[j], 16).PadLeft(2, '0')).Append("      ");
                    sb.Append(Convert.ToString(bytes[j], 2).PadLeft(8, '0'));
                }
                sb.Replace("100001110101", "[red]100001110101[/]");
                AnsiConsole.MarkupLine(hex.ToString());
                AnsiConsole.MarkupLine(sb.ToString());
            }
        }

        curAddress = 0x80;
        StringBuilder owner = new();
        for (int i = 0; i < 3; i++){
            var byteSegment = new ArraySegment<byte>(card, curAddress, 0x10);
            bool verified = CheckCRC(byteSegment);
            var text = Encoding.Latin1.GetString(byteSegment.Take(0xf).ToArray()).Trim();
            if(!String.IsNullOrWhiteSpace(text))
            {
                owner.Append(text);
                owner.Append(" ");
            }
            curAddress += 0x10;
        }
        if(owner.Length > 0)
            AnsiConsole.MarkupLine($"[bold]Propietari:[/] {owner.ToString().Trim()}");
        else
            AnsiConsole.MarkupLine("[bold]Propietari:[/] Targeta anònima");

        curAddress = 0xc0;
        int startYear = 0;
        int expireYear = 0;
        {
            var byteSegment = GetLine(card, curAddress);
            byte[] bytes = byteSegment.Reverse().ToArray();
            bool verified = CheckCRC(byteSegment);
            int d = ((bytes[10] & 1) << 4) + (bytes[11] >> 4);
            int m = bytes[11] & 15;
            int sumYears = bytes[12] >> 4;
            startYear = ((bytes[12] & 15) << 4) + (bytes[13] >> 4) + 1900;
            expireYear = startYear + sumYears;
            AnsiConsole.MarkupLine($"[bold]Caducitat:[/] {d.ToString().PadLeft(2, '0')}/{m.ToString().PadLeft(2, '0')}/{expireYear}");
        }

        curAddress = 0xd0;
        {
            var byteSegment = GetLine(card, curAddress);
            byte[] bytes = byteSegment.Reverse().ToArray();
            bool verified = CheckCRC(byteSegment);
            int p = bytes[3] >> 2;
            int ea = ((bytes[3] & 3) << 6) + (bytes[4] >> 2);
            int m = ((bytes[4] & 3) << 8) + bytes[5];
            AnsiConsole.MarkupLine($"[bold]P:[/] {p} [bold]EA:[/] {ea} [bold]M:[/] {m.ToString().PadLeft(4, '0')}");
        }
        AnsiConsole.WriteLine();
        curAddress = 0x100;
        {
            var byteSegment = new ArraySegment<byte>(card, curAddress, 0x10);
            curAddress += 0x10;
            byte[] bytes = byteSegment.Reverse().ToArray();
            bool verified = CheckCRC(byteSegment);
            int minutes = bytes[1] >> 2;
            int hours = ((bytes[1] & 0x3) << 3) + (bytes[2] >> 5);
            int day = bytes[2] & 0b11111;
            int month = bytes[3] >> 4;
            int year = startYear + (bytes[3] & 15);
            int zone = bytes[4];
            int stop = (bytes[5] << 7) + (bytes[6] >> 1);
            int op = ((bytes[6] & 1) << 7) + (bytes[7] >> 1);
            int line = ((bytes[7] & 1) << 10) + (bytes[8] << 2) + (bytes[9] >> 6);
            string companyName = CompaniesProvider.GetName(op);
            string stopName = StopsProvider.GetName(stop);
            string lineName = LinesProvider.GetName(line);
            AnsiConsole.MarkupLine($"[bold]Última validació:[/] Feta el {day.ToString().PadLeft(2, '0')}/{month.ToString().PadLeft(2, '0')}/{year} a les {hours.ToString().PadLeft(2, '0')}:{minutes.ToString().PadLeft(2, '0')} amb l'operador {companyName} des de {stopName} ({lineName}) Zona: {zone}{((verified && !settings.Pretty) ? " - Checksum verificat" : String.Empty)}");
            if(!settings.Pretty)
            {
                AnsiConsole.WriteLine(string.Join("      ", bytes.Select(x => Convert.ToString(x, 16).PadLeft(2, '0'))));
                AnsiConsole.Markup($"[red]{Convert.ToString(bytes[0], 2).PadLeft(8, '0')}[/]");
                AnsiConsole.Markup($"[blue]{Convert.ToString(minutes, 2).PadLeft(6, '0')}[/]");
                AnsiConsole.Markup($"[yellow]{Convert.ToString(hours, 2).PadLeft(5, '0')}[/]");
                AnsiConsole.Markup($"[green]{Convert.ToString(day, 2).PadLeft(5, '0')}[/]");
                AnsiConsole.Markup($"[aqua]{Convert.ToString(month, 2).PadLeft(4, '0')}[/]");
                AnsiConsole.Markup($"[magenta3]{Convert.ToString(bytes[3] & 15, 2).PadLeft(4, '0')}[/]");
                AnsiConsole.Markup($"[springgreen2]{Convert.ToString(bytes[4], 2).PadLeft(8, '0')}[/]");
                AnsiConsole.Markup($"[fuchsia]{Convert.ToString(bytes[5], 2).PadLeft(8, '0')}[/]");
                AnsiConsole.Markup($"[fuchsia]{Convert.ToString(bytes[6] >> 1, 2).PadLeft(7, '0')}[/]");
                AnsiConsole.Markup($"[indianred]{Convert.ToString(bytes[6] & 1, 2)}[/]");
                AnsiConsole.Markup($"[indianred]{Convert.ToString(bytes[7] >> 1, 2).PadLeft(7, '0')}[/]");
                AnsiConsole.Markup($"[aquamarine1]{Convert.ToString(bytes[7] & 1, 2)}[/]");
                AnsiConsole.Markup($"[aquamarine1]{Convert.ToString(bytes[8], 2).PadLeft(8, '0')}[/]");
                AnsiConsole.Markup($"[aquamarine1]{Convert.ToString(bytes[9] >> 6, 2).PadLeft(2, '0')}[/]");
                AnsiConsole.Write(Convert.ToString(bytes[9] & 63, 2).PadLeft(6, '0'));
                AnsiConsole.Write(Convert.ToString(bytes[10], 2).PadLeft(8, '0'));
                AnsiConsole.Write(Convert.ToString(bytes[11], 2).PadLeft(8, '0'));
                AnsiConsole.Write(Convert.ToString(bytes[12], 2).PadLeft(8, '0'));
                AnsiConsole.Write(Convert.ToString(bytes[13], 2).PadLeft(8, '0'));
                AnsiConsole.Write(Convert.ToString(bytes[14], 2).PadLeft(8, '0'));
                AnsiConsole.Write(Convert.ToString(bytes[15], 2).PadLeft(8, '0'));
                AnsiConsole.WriteLine();

                AnsiConsole.Markup("[red]checksum[/]");
                AnsiConsole.Markup("[blue]mmmmmm[/]");
                AnsiConsole.Markup("[yellow]hhhhh[/]");
                AnsiConsole.Markup("[green]DDDDD[/]");
                AnsiConsole.Markup("[aqua]MMMM[/]");
                AnsiConsole.Markup("[magenta3]AAAA[/]");
                AnsiConsole.Markup("[springgreen2]zzzzzzzz[/]");
                AnsiConsole.Markup("[fuchsia]ppppppppppppppp[/]");
                AnsiConsole.Markup("[indianred]oooooooo[/]");
                AnsiConsole.Markup("[aquamarine1]lllllllllll[/]");
                AnsiConsole.Markup(" ");

                AnsiConsole.WriteLine();
            }
        }
        
        AnsiConsole.WriteLine();

        ArraySegment<byte>[] trips = new ArraySegment<byte>[10];
        curAddress = 0x150;
        {
            var byteSegment = new ArraySegment<byte>(card, curAddress, 0x10);
            curAddress += 0x10;
            byte[] bytes = byteSegment.Reverse().ToArray();
            bool verified = CheckCRC(byteSegment);
            int tripsLeft = bytes[7] >> 1;
            int day = ((bytes[12] & 1) << 4) + (bytes[13] >> 4);
            int month = bytes[13] & 15;
            string date = $"{(day != 0 ? $"{day.ToString().PadLeft(2, '0')}/{month.ToString().PadLeft(2, '0')}" : "Sense caducitat")}";
            if(!settings.Pretty) {
                AnsiConsole.WriteLine($"Estat Targeta:");
                AnsiConsole.WriteLine($"Viatges restants: {tripsLeft}");
                AnsiConsole.WriteLine($"Caducitat títol: {date}");
                AnsiConsole.WriteLine(string.Join("      ", bytes.Select(x => Convert.ToString(x, 16).PadLeft(2, '0'))));
                AnsiConsole.Markup($"[red]{Convert.ToString(bytes[0], 2).PadLeft(8, '0')}[/]");
                AnsiConsole.Markup($"{Convert.ToString(bytes[1], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[2], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[3], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[4], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[5], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[6], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"[chartreuse1]{Convert.ToString(bytes[7] >> 1, 2).PadLeft(7, '0')}[/]");
                AnsiConsole.Markup($"{Convert.ToString(bytes[7] & 1, 2)}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[8], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[9], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[10], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[11], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[12] >> 1, 2).PadLeft(7, '0')}");
                AnsiConsole.Markup($"[green]{Convert.ToString(bytes[12] & 1, 2)}[/]");
                AnsiConsole.Markup($"[green]{Convert.ToString(bytes[13] >> 4, 2).PadLeft(4, '0')}[/]");
                AnsiConsole.Markup($"[aqua]{Convert.ToString(bytes[13] & 15, 2).PadLeft(4, '0')}[/]");
                AnsiConsole.Markup($"{Convert.ToString(bytes[14], 2).PadLeft(8, '0')}");
                AnsiConsole.Markup($"{Convert.ToString(bytes[15], 2).PadLeft(8, '0')}");
                AnsiConsole.WriteLine();
                AnsiConsole.Markup("[red]checksum[/]");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("[chartreuse1]vvvvvvv[/]");
                AnsiConsole.Markup(" ");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("       ");
                AnsiConsole.Markup("[green]DDDDD[/]");
                AnsiConsole.Markup("[aqua]MMMM[/]");
                AnsiConsole.Markup("        ");
                AnsiConsole.Markup("        ");
            } 
            else
            {
                var statusTable = new Table();
                statusTable.Title = new TableTitle("[bold]Estat de la targeta[/]");
                statusTable.AddColumn("[bold]Viatges restants[/]");
                statusTable.AddColumn("[bold]Caducitat títol[/]");
                statusTable.AddRow(tripsLeft.ToString(), date);
                AnsiConsole.Write(statusTable);

            }
            AnsiConsole.WriteLine();
        }

        curAddress = 0x240;
        Table rechargeTable = null;
        if (settings.Pretty)
        {
            rechargeTable = new Table();
            rechargeTable.AddColumn("[bold]#[/]");
            rechargeTable.AddColumn("[bold]Data[/]");
            rechargeTable.AddColumn("[bold]Títol[/]");
            rechargeTable.Title = new TableTitle("[bold]Recàrregues[/]");
        }
        for (int i = 0; i < 3; i++)
        {
            if (((curAddress + 0x10) % 0x40) == 0) curAddress += 0x10;
            trips[i] = new ArraySegment<byte>(card, curAddress, 0x10);
            curAddress += 0x10;
            byte[] bytes = trips[i].Reverse().ToArray();
            bool verified = CheckCRC(trips[i]);
            int day = ((bytes[1] & 1) << 4) + (bytes[2] >> 4);
            int month = bytes[2] & 15;
            int year = startYear + (bytes[3] >> 4);
            int title = bytes[7];
            string titleName = TitlesProvider.GetName(title);
            string date = $"{day.ToString().PadLeft(2, '0')}/{month.ToString().PadLeft(2, '0')}/{year}";
            if (!settings.Pretty) AnsiConsole.WriteLine();
            if (day == 0 && month == 0)
            {
                if (!settings.Pretty)
                    AnsiConsole.WriteLine($"Recàrrega {i + 1} buida");
                else
                    rechargeTable.AddRow($"{i + 1}", "", "");
            } else {
                if (settings.Pretty)
                    rechargeTable.AddRow($"{i + 1}", date, titleName);
                else
                {
                    AnsiConsole.WriteLine($"Recàrrega {i + 1} de {titleName} feta el {date}{(verified ? " - Checksum verificat" : String.Empty)}:");
                    AnsiConsole.WriteLine(string.Join("      ", bytes.Select(x => Convert.ToString(x, 16).PadLeft(2, '0'))));
                    AnsiConsole.Markup($"[red]{Convert.ToString(bytes[0], 2).PadLeft(8, '0')}[/]");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[1] >> 1, 2).PadLeft(7, '0')}");
                    AnsiConsole.Markup($"[green]{Convert.ToString(bytes[1] & 1, 2)}[/]");
                    AnsiConsole.Markup($"[green]{Convert.ToString(bytes[2] >> 4, 2).PadLeft(4, '0')}[/]");
                    AnsiConsole.Markup($"[aqua]{Convert.ToString(bytes[2] & 15, 2).PadLeft(4, '0')}[/]");
                    AnsiConsole.Markup($"[magenta3]{Convert.ToString(bytes[3] >> 4, 2).PadLeft(4, '0')}[/]");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[3] & 15, 2).PadLeft(4, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[4], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[5], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[6], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[7], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[8], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[9], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[10], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[11], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[12], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[13], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[14], 2).PadLeft(8, '0')}");
                    AnsiConsole.Markup($"{Convert.ToString(bytes[15], 2).PadLeft(8, '0')}");
                    AnsiConsole.WriteLine();
                    AnsiConsole.Markup("[red]checksum[/]");
                    AnsiConsole.Markup("       ");
                    AnsiConsole.Markup("[green]DDDDD[/]");
                    AnsiConsole.Markup("[aqua]MMMM[/]");
                    AnsiConsole.Markup("[magenta3]AAAA[/]");
                    AnsiConsole.WriteLine();
                }
            }
        }

        if (settings.Pretty) AnsiConsole.Write(rechargeTable);

        curAddress = 0x2c0;
        Table tripsTable = new Table();
        if(settings.Pretty)
        {
            tripsTable
                .AddColumn("[bold]#[/]")
                .AddColumn("[bold]Operador[/]")
                .AddColumn("[bold]Data[/]")
                .AddColumn("[bold]Origen[/]")
                .AddColumn("[bold]Linia[/]")
                .AddColumn("[bold]Vehicle[/]")
                .AddColumn("[bold]Zona[/]")
                .AddColumn("[bold]És transbord?[/]")
                .Title = new TableTitle("[bold]Últimes 10 validacions[/]");
        }
        for (int i = 0; i < 10; i++)
        {
            if (((curAddress + 0x10) % 0x40) == 0) curAddress += 0x10;
            trips[i] = new ArraySegment<byte>(card, curAddress, 0x10);
            curAddress += 0x10;
            if (!trips[i].Take(0xf).All(x => x == 0x0))
            {
                if (!settings.Pretty) AnsiConsole.WriteLine();
                byte[] bytes = trips[i].Reverse().ToArray();
                bool verified = CheckCRC(trips[i]);

                int operatorCompany = ((bytes[8] & 127) << 1) + (bytes[9] >> 7);

                int minutes = bytes[1] >> 2;
                int hours = ((bytes[1] & 0x3) << 3) + (bytes[2] >> 5);
                int day = bytes[2] & 0b11111;
                int month = bytes[3] >> 4;
                int year = startYear + (bytes[3] & 15);
                int busNumber = (bytes[4] << 2) + (bytes[5] >> 6);
                int zone = ((bytes[5] & 63) << 2) + (bytes[6] >> 6);
                int busStop = ((bytes[6] & 63) << 9) + (bytes[7] << 1) + (bytes[8] >> 7);
                int line = ((bytes[9] & 127) << 4) + (bytes[10] >> 4);
                string date = $"{day.ToString().PadLeft(2, '0')}/{month.ToString().PadLeft(2, '0')}/{year}";
                string time = $"{hours.ToString().PadLeft(2, '0')}:{minutes.ToString().PadLeft(2, '0')}";
                string lineName = LinesProvider.GetName(line);
                string busStopName = StopsProvider.GetName(busStop);
                string companyName = CompaniesProvider.GetName(operatorCompany);
                bool transbord = (bytes[15] & 1) == 1;
                if (settings.Pretty)
                {
                    tripsTable.AddRow($"{i + 1}", companyName, $"{date} {time}", busStopName, lineName, $"{busNumber}", $"{zone}", transbord ? "Sí" : "No");
                } 
                else
                {
                    AnsiConsole.WriteLine($"Viatge amb {companyName} el {date} a les {time} amb el vehicle {busNumber} des de {busStopName} ({lineName}) Zona: {zone}{(verified ? " - Checksum verificat" : String.Empty)}");
                    AnsiConsole.WriteLine(string.Join("      ", bytes.Select(x => Convert.ToString(x, 16).PadLeft(2, '0'))));
                    AnsiConsole.Markup($"[red]{Convert.ToString(bytes[0], 2).PadLeft(8, '0')}[/]");
                    AnsiConsole.Markup($"[blue]{Convert.ToString(minutes, 2).PadLeft(6, '0')}[/]");
                    AnsiConsole.Markup($"[yellow]{Convert.ToString(hours, 2).PadLeft(5, '0')}[/]");
                    AnsiConsole.Markup($"[green]{Convert.ToString(day, 2).PadLeft(5, '0')}[/]");
                    AnsiConsole.Markup($"[aqua]{Convert.ToString(month, 2).PadLeft(4, '0')}[/]");
                    AnsiConsole.Markup($"[magenta3]{Convert.ToString(bytes[3] & 15, 2).PadLeft(4, '0')}[/]");
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
                    AnsiConsole.Markup("[magenta3]AAAA[/]");
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
        if(settings.Pretty) AnsiConsole.Write(tripsTable);
        return 0;
    }
}