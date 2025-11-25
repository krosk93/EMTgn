using System.Threading.Tasks;
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
