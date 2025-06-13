using MicroMerge.Operator;
using MicroMerge.Tables;

void PrintResult(OperatorResult result)
{
    Console.WriteLine("Operação concluída com sucesso!");
    Console.WriteLine("Result:");
    Console.WriteLine("\tNúmero de páginas geradas: " + result.NumberOfCreatedPages);
    Console.WriteLine("\tNúmero de registros gerados: " + result.NumberOfCreatedRecords);
    Console.WriteLine("\tNúmero de IO's: " + result.NumberOfIOOperations);
    Console.WriteLine("\tNome da tabela gerada: " + result.NameOfResultTable);
}

string GetBasePath()
{
    var currentDir = Directory.GetCurrentDirectory();
    var projectName = "MicroMerge";
    
    if (Directory.Exists(Path.Combine(currentDir, "Data")))
    {
        Console.WriteLine($"Executando do diretório do projeto: {currentDir}");
        return currentDir;
    }
    
    var projectPath = Path.Combine(currentDir, projectName);
    if (Directory.Exists(projectPath) && Directory.Exists(Path.Combine(projectPath, "Data")))
    {
        Console.WriteLine($"Executando do diretório da solução: {currentDir}");
        Console.WriteLine($"Usando diretório do projeto: {projectPath}");
        return projectPath;
    }
    
    var parentDir = currentDir;
    for (int i = 0; i < 3; i++) 
    {
        parentDir = Directory.GetParent(parentDir)?.FullName;
        if (parentDir == null) break;
        
        var candidatePath = Path.Combine(parentDir, projectName);
        if (Directory.Exists(candidatePath) && Directory.Exists(Path.Combine(candidatePath, "Data")))
        {
            Console.WriteLine($"Encontrado diretório do projeto em: {candidatePath}");
            return candidatePath;
        }
    }
    
    throw new DirectoryNotFoundException(
        $"Não foi possível encontrar o diretório do projeto '{projectName}' com a pasta 'Data'. " +
        $"Certifique-se de executar o programa do diretório correto.");
}

var basePath = GetBasePath();
var dataPath = Path.Combine(basePath, "Data");
var outputPath = Path.Combine(basePath, "output");

Directory.CreateDirectory(outputPath);

Console.WriteLine($"Diretório de dados: {dataPath}");
Console.WriteLine($"Diretório de saída: {outputPath}");
Console.WriteLine();

var countryTable = new CountryTable(Path.Combine(dataPath, "pais.csv"));
var grapeTable = new GrapeTable(Path.Combine(dataPath, "uva.csv"));
var wineTable = new WineTable(Path.Combine(dataPath, "vinho.csv"));

// Uncomment one of the operations below to test different joins:

// 1. Vinho ⋈ País (por país de produção)
// var wineTable = new WineTable(Path.Combine(dataPath, "vinho.csv"));
// using var op = new Operator(wineTable, countryTable, "pais_producao_id", "pais_id");

// 2. País ⋈ Vinho (por país de produção)
// var wineTable = new WineTable(Path.Combine(dataPath, "vinho.csv"));
// using var op = new Operator(countryTable, wineTable, "pais_id", "pais_producao_id");

// 3. Vinho ⋈ Uva (por tipo de uva)
// var wineTable = new WineTable(Path.Combine(dataPath, "vinho.csv"));
// using var op = new Operator(wineTable, grapeTable, "uva_id", "uva_id");

// 4. Uva ⋈ Vinho (por tipo de uva)
// var wineTable = new WineTable(Path.Combine(dataPath, "vinho.csv"));
// using var op = new Operator(grapeTable, wineTable, "uva_id", "uva_id");

// 5. Uva ⋈ País (por país de origem)
// using var op = new Operator(grapeTable, countryTable, "pais_origem_id", "pais_id");

// 6. País ⋈ Uva (por país de origem) - OPERAÇÃO PADRÃO ATIVA
using var op = new Operator(countryTable, grapeTable, "pais_id", "pais_origem_id");

var res = op.Execute();
PrintResult(res);

Console.WriteLine($"\nEscrevendo resultado em: {outputPath}");
op.WriteToCsv(outputPath);

Console.WriteLine($"Arquivo de saída criado: {Path.Combine(outputPath, res.NameOfResultTable + ".csv")}");
