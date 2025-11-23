using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EMTgn.Models;

namespace EMTgn.Providers
{
    public static class StopsProvider
    {
        private static readonly Lazy<List<Stop>> _parades = new Lazy<List<Stop>>(Load);

        public static IReadOnlyList<Stop> All => _parades.Value;

        private static List<Stop> Load()
        {
            var assembly = typeof(CompaniesProvider).Assembly;
            var resourceName = "EMTgn.Data.stops.json"; // ← namespace + ruta relativa

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Recurs '{resourceName}' no trobat.");

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return JsonSerializer.Deserialize<List<Stop>>(json) 
                ?? throw new InvalidOperationException("JSON buit o invàlid.");
        }

        public static string GetName(int stopId)
        {
            return _parades.Value.FirstOrDefault(s => s.stopId == stopId)?.stopName 
                ?? $"No trobat ({stopId})";
        }
    }
}