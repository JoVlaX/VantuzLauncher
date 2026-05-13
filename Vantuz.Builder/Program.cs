using System; 
using System.IO; 
using System.Security.Cryptography; 
using System.Text.Json; 
using System.Text.Json.Nodes; 
 
if (args.Length < 3) 
{ 
    Console.WriteLine("Usage: Vantuz.Builder <templatePath> <pluginsDirPath> <outputPath>"); 
    return; 
} 
 
string templatePath = args[0]; 
string pluginsDir = args[1]; 
string outputPath = args[2]; 
 
if (!File.Exists(templatePath)) 
{ 
    Console.WriteLine($"Template not found: {templatePath}"); 
    return; 
} 
 
var jsonString = File.ReadAllText(templatePath); 
var node = JsonNode.Parse(jsonString); 
 
if (node?["plugins"] is JsonObject pluginsNode) 
{ 
    foreach (var plugin in pluginsNode.ToDictionary(k => k.Key, v => v.Value)) 
    { 
        string dllPath = Path.Combine(pluginsDir, plugin.Key); 
        if (File.Exists(dllPath)) 
        { 
            using var sha = SHA256.Create(); 
            using var fs = File.OpenRead(dllPath); 
            var hashBytes = sha.ComputeHash(fs); 
            var hashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant(); 
             
            // Обновляем хэш в JSON 
            pluginsNode[plugin.Key] = hashStr; 
            Console.WriteLine($"[Hash Pinning] {plugin.Key} -> {hashStr}"); 
        } 
        else 
        { 
            Console.WriteLine($"[WARNING] Plugin DLL not found: {dllPath}"); 
        } 
    } 
} 
 
File.WriteAllText(outputPath, node!.ToJsonString(new JsonSerializerOptions { WriteIndented = true })); 
Console.WriteLine($"Manifest generated: {outputPath}"); 
