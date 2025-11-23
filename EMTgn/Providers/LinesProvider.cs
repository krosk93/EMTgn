using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EMTgn.Models;

namespace EMTgn.Providers
{
    public static class LinesProvider
    {
        private static readonly Lazy<List<Line>> _parades = new Lazy<List<Line>>(Load);

        public static IReadOnlyList<Line> All => _parades.Value;

        private static List<Line> Load()
        {
            var assembly = typeof(CompaniesProvider).Assembly;
            var resourceName = "EMTgn.Data.lines.json"; // ← namespace + ruta relativa

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Recurs '{resourceName}' no trobat.");

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return JsonSerializer.Deserialize<List<Line>>(json) 
                ?? throw new InvalidOperationException("JSON buit o invàlid.");
        }

        public static string GetName(int id)
        {
            return _parades.Value.FirstOrDefault(c => c.lineId == id)?.lineName 
                ?? $"{id}";
        }
    }
}