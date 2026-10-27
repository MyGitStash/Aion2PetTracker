using System.Text.Json;

namespace Aion2PetTracker.Core.Pets;

public sealed record PetDefinition(int Id, string Name, string Genus);
public sealed class PetCatalog
{
    private readonly Dictionary<int, PetDefinition> _pets;
    public PetCatalog(IEnumerable<PetDefinition> pets) => _pets = pets.ToDictionary(p => p.Id);
    public static PetCatalog Load(string path) => Parse(File.ReadAllText(path));
    public static PetCatalog Parse(string json) => new(JsonSerializer.Deserialize<PetDefinition[]>(json) ?? []);
    public PetDefinition? Find(int id) => _pets.GetValueOrDefault(id);
    public int Count => _pets.Count;
}
