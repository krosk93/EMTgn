using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EMTgn.Models;

namespace EMTgn.Providers
{
    public static class TitlesProvider
    {
        private static readonly Lazy<List<Title>> _parades = new Lazy<List<Title>>(Load);

        public static IReadOnlyList<Title> All => _parades.Value;

        private static List<Title> Load()
        {
            var assembly = typeof(TitlesProvider).Assembly;
            var resourceName = "EMTgn.Data.titles.json"; // ← namespace + ruta relativa

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Recurs '{resourceName}' no trobat.");

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return JsonSerializer.Deserialize<List<Title>>(json) 
                ?? throw new InvalidOperationException("JSON buit o invàlid.");
        }

        public static string GetName(int titleId)
        {
            return _parades.Value.FirstOrDefault(t => t.titleId == titleId)?.titleName 
                ?? $"No trobat ({titleId})";
        }
    }
}