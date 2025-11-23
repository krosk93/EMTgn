using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using InvertedTomato.IO;
using Flurl.Http;
using System.Threading.Tasks;
using Spectre.Console;
using System.Text;
using EMTgn.Providers;
using Spectre.Console.Cli;

namespace EMTgn
{
    class Program
    {
        

        static Task<int> Main(string[] args)
        {
            var app = new CommandApp<ProcessCardCommand>();
            return app.RunAsync(args);
            
        }
    }
}
