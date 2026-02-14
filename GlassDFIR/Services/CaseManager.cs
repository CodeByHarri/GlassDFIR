using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GlassDFIR.Models;

namespace GlassDFIR.Services
{
    public class CaseManager : ICaseManager
    {
        private static readonly string[] Colors = {
            "Amber", "Amethyst", "Azure", "Banana", "Beige", "Bisque", "Black", "Blue", "Bronze", "Brown",
            "Burgundy", "Charcoal", "Cherry", "Chocolate", "Cobalt", "Copper", "Coral", "Crimson", "Cyan",
            "Emerald", "Forest", "Fuchsia", "Gold", "Gray", "Green", "Indigo", "Ivory", "Jade", "Lavender",
            "Lemon", "Lime", "Magenta", "Maroon", "Mint", "Navy", "Olive", "Orange", "Orchid", "Peach",
            "Pearl", "Periwinkle", "Pink", "Plum", "Purple", "Red", "Rose", "Ruby", "Sapphire", "Silver",
            "Slate", "Tan", "Teal", "Turquoise", "Violet", "White", "Yellow"
        };

        private static readonly string[] Animals = {
            "Albatross", "Alligator", "Alpaca", "Antelope", "Ape", "Armadillo", "Baboon", "Badger", "Bat", "Bear",
            "Beaver", "Bison", "Boar", "Buffalo", "Camel", "Capybara", "Cassowary", "Cat", "Cheetah", "Chicken",
            "Chimpanzee", "Chinchilla", "Chipmunk", "Cobra", "Cougar", "Cow", "Coyote", "Crab", "Crane", "Crocodile",
            "Crow", "Deer", "Dog", "Dolphin", "Donkey", "Duck", "Eagle", "Echidna", "Elephant", "Elk", "Emu",
            "Falcon", "Ferret", "Flamingo", "Fox", "Frog", "Gazelle", "Gecko", "Giraffe", "Goat", "Goose",
            "Gorilla", "Gopher", "Grasshopper", "Hamster", "Hare", "Hawk", "HedgeHog", "Hippo", "Horse", "Hyena",
            "Ibex", "Iguana", "Impala", "Jackal", "Jaguar", "Kangaroo", "Koala", "Komodo", "Kookaburra", "Lemur",
            "Leopard", "Lion", "Lizard", "Llama", "Lobster", "Lynx", "Macaw", "Magpie", "Mallard", "Manatee",
            "Mandrill", "Mantis", "Marmot", "Meerkat", "Mink", "Mole", "Monkey", "Moose", "Mouse", "Mule",
            "Narwhal", "Newt", "Nightingale", "Ocelot", "Octopus", "Okapi", "Opossum", "Orangutan", "Orca", "Ostrich",
            "Otter", "Owl", "Ox"
        };

        private readonly string _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlassDFIR_Output");
        private readonly List<CaseModel> _cases = new();
        private CaseModel? _currentCase;

        public CaseModel? CurrentCase => _currentCase;
        public event Action? CaseChanged;

        public Task InitializeAsync()
        {
            if (!Directory.Exists(Path.Combine(_basePath, "Cases")))
            {
                Directory.CreateDirectory(Path.Combine(_basePath, "Cases"));
                return Task.CompletedTask;
            }

            // Load existing cases by scanning directories
            var caseDirs = Directory.GetDirectories(Path.Combine(_basePath, "Cases"));
            foreach (var dir in caseDirs)
            {
                var dirName = Path.GetFileName(dir);
                var parts = dirName.Split('_');
                if (parts.Length >= 2)
                {
                    var id = parts[0];
                    var name = string.Join(" ", camelCaseToWords(parts[1]));
                    
                    _cases.Add(new CaseModel 
                    { 
                        Id = id, 
                        Name = name, 
                        BasePath = _basePath 
                    });
                }
            }

            CaseChanged?.Invoke();
            return Task.CompletedTask;
        }

        private IEnumerable<string> camelCaseToWords(string input)
        {
            return System.Text.RegularExpressions.Regex.Split(input, @"(?<!^)(?=[A-Z])");
        }

        public IEnumerable<CaseModel> GetCases() => _cases;

        public async Task<CaseModel> CreateCaseAsync()
        {
            var nextId = (_cases.Count + 1).ToString("D4");
            var random = new Random();
            var color = Colors[random.Next(Colors.Length)];
            var animal = Animals[random.Next(Animals.Length)];
            var name = $"{color} {animal}";

            var newCase = new CaseModel
            {
                Id = nextId,
                Name = name,
                BasePath = _basePath,
                IsActive = false
            };

            // Create folders
            Directory.CreateDirectory(newCase.CasePath);
            Directory.CreateDirectory(newCase.EvidencePath);
            Directory.CreateDirectory(newCase.OutputPath);

            _cases.Add(newCase);
            return await Task.FromResult(newCase);
        }

        public Task ActivateCaseAsync(CaseModel? @case)
        {
            foreach (var c in _cases) c.IsActive = false;
            
            if (@case != null)
            {
                @case.IsActive = true;
            }
            
            _currentCase = @case;
            CaseChanged?.Invoke();
            return Task.CompletedTask;
        }

        public async Task DeleteCaseAsync(CaseModel @case)
        {
            if (@case == null) return;

            // 1. Deactivate if it's the current case
            if (_currentCase == @case)
            {
                await ActivateCaseAsync(null);
            }

            // 2. Remove from list
            _cases.Remove(@case);

            // 3. Delete folders from disk (Run in background to prevent UI freeze)
            await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(@case.CasePath))
                    {
                        DeleteDirectoryRobust(@case.CasePath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete case folder: {ex.Message}");
                }
            });

            CaseChanged?.Invoke();
        }

        private void DeleteDirectoryRobust(string path)
        {
            if (!Directory.Exists(path)) return;

            // Delete files
            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal); // Remove ReadOnly
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete file {file}: {ex.Message}");
                }
            }

            // Delete subdirectories
            foreach (var dir in Directory.GetDirectories(path))
            {
                DeleteDirectoryRobust(dir);
            }

            // Delete the directory itself
            try
            {
                Directory.Delete(path, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete directory {path}: {ex.Message}");
            }
        }

        public string GetCurrentOutputPath()
        {
            if (_currentCase != null)
            {
                return _currentCase.OutputPath;
            }
            return GetGlobalOutputPath();
        }

        public string GetGlobalOutputPath()
        {
            return _basePath;
        }
    }
}
