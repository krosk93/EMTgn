using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using EMTgn.Models;

namespace EMTgn.Providers
{
    public static class CompaniesProvider
    {
        private static readonly Lazy<List<Company>> _parades = new Lazy<List<Company>>(Load);

        public static IReadOnlyList<Company> All => _parades.Value;

        private static List<Company> Load()
        {
            var assembly = typeof(CompaniesProvider).Assembly;
            var resourceName = "EMTgn.Data.companies.json"; // ← namespace + ruta relativa

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Recurs '{resourceName}' no trobat.");

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return JsonSerializer.Deserialize<List<Company>>(json) 
                ?? throw new InvalidOperationException("JSON buit o invàlid.");
        }

        public static string GetName(int companyId)
        {
            return _parades.Value.FirstOrDefault(c => c.companyId == companyId)?.companyName 
                ?? $"No trobat ({companyId})";
        }
    }
}